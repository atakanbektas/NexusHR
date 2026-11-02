using System.Security.Cryptography;
using System.Text;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using NexusHR.Candidate.Infrastructure.Persistence;
using NexusHR.Candidate.Domain.Candidates;
using NexusHR.Hiring.Domain.HiringProcesses;
using NexusHR.Hiring.Domain.EligibleCandidates;
using NexusHR.Hiring.Infrastructure.Persistence;
using NexusHR.Onboarding.Domain.EmployeeOnboardings;
using NexusHR.Onboarding.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using CandidateEntity = NexusHR.Candidate.Domain.Candidates.Candidate;

namespace NexusHR.Onboarding.Tests;

public sealed class CrossDatabaseRecoveryTests
{
    [Fact]
    public async Task QuiescedThreeDatabaseSnapshotRestoresBusinessLinksTasksAndCvHashIntoNewTargets()
    {
        await using var source = new PostgreSqlBuilder("postgres:16-alpine").WithPassword(Guid.NewGuid().ToString("N")).Build();
        await using var target = new PostgreSqlBuilder("postgres:16-alpine").WithPassword(Guid.NewGuid().ToString("N")).Build();
        var password = Guid.NewGuid().ToString("N");
        await using var store = new ContainerBuilder("nexushr-minio:2025-10-15").WithEnvironment("MINIO_ROOT_USER", "restore-fixture")
            .WithEnvironment("MINIO_ROOT_PASSWORD", password).WithCommand("server", "/data").WithPortBinding(9000, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(x => x.ForPort(9000).ForPath("/minio/health/live"))).Build();
        await Task.WhenAll(source.StartAsync(), target.StartAsync(), store.StartAsync());
        string[] databases = ["candidate_dr", "hiring_dr", "onboarding_dr"];
        foreach (var db in databases) Assert.Equal(0, (await source.ExecAsync(["createdb", "-U", "postgres", db])).ExitCode);
        static string Connection(PostgreSqlContainer pg, string db) => new Npgsql.NpgsqlConnectionStringBuilder(pg.GetConnectionString()) { Database = db }.ConnectionString;
        await using var candidates = new CandidateDbContext(new DbContextOptionsBuilder<CandidateDbContext>().UseNpgsql(Connection(source, databases[0])).Options);
        await using var hiring = new HiringDbContext(new DbContextOptionsBuilder<HiringDbContext>().UseNpgsql(Connection(source, databases[1])).Options);
        await using var onboarding = new OnboardingDbContext(new DbContextOptionsBuilder<OnboardingDbContext>().UseNpgsql(Connection(source, databases[2])).Options);
        await candidates.Database.MigrateAsync(); await hiring.Database.MigrateAsync(); await onboarding.Database.MigrateAsync();
        var person = new CandidateEntity(Guid.NewGuid(), "Synthetic", "Recovery", "recovery@example.test", "0000000000");
        person.MarkDocumentsPending(); person.MarkReadyForHiring();
        var pdf = Pdf(); var hash = Convert.ToHexStringLower(SHA256.HashData(pdf));
        var document = new CandidateDocument(Guid.NewGuid(), person.Id, CandidateDocumentType.Cv, "synthetic.pdf", "synthetic/cv.pdf", "application/pdf", pdf.Length);
        document.RecordContentHash(hash); candidates.AddRange(person, document); await candidates.SaveChangesAsync();
        var now = DateTime.UtcNow; var start = DateOnly.FromDateTime(now.AddDays(10));
        var process = new HiringProcess(Guid.NewGuid(), person.Id, "Engineer", "IT", EmploymentType.FullTime);
        process.PrepareOffer(1000, "TRY", start, now.AddDays(2)); process.SendOffer(new string('a', 64)); process.AcceptOffer(DateTime.UtcNow);
        var employee = new EmployeeOnboarding(Guid.NewGuid(), process.Id, person.Id, person.FirstName, person.LastName, person.Email, process.PositionTitle, process.Department, start, DateTime.UtcNow);
        foreach (var task in employee.Tasks) { employee.UpdateTask(task.Id, OnboardingTaskStatus.InProgress, DateTime.UtcNow); employee.UpdateTask(task.Id, OnboardingTaskStatus.Completed, DateTime.UtcNow); }
        employee.Complete(DateTime.UtcNow); process.Complete(employee.Id);
        hiring.AddRange(process, new EligibleCandidate(person.Id, person.FirstName, person.LastName, person.Email, now));
        onboarding.Add(employee); await hiring.SaveChangesAsync(); await onboarding.SaveChangesAsync();
        using var s3 = new MinioClient().WithEndpoint($"{store.Hostname}:{store.GetMappedPublicPort(9000)}").WithCredentials("restore-fixture", password).Build();
        await s3.MakeBucketAsync(new MakeBucketArgs().WithBucket("source-cvs")); await s3.MakeBucketAsync(new MakeBucketArgs().WithBucket("restored-cvs"));
        using (var body = new MemoryStream(pdf)) await s3.PutObjectAsync(new PutObjectArgs().WithBucket("source-cvs").WithObject(document.ObjectName).WithStreamData(body).WithObjectSize(pdf.Length));
        // All fixture writers are quiescent before the first dump. No live business volume is mounted.
        var dumps = new Dictionary<string, byte[]>();
        foreach (var db in databases) { var dump = await source.ExecAsync(["pg_dump", "-U", "postgres", "-d", db, "--no-owner", "--no-acl"]); Assert.Equal(0, dump.ExitCode); dumps[db] = Encoding.UTF8.GetBytes(dump.Stdout); }
        foreach (var db in databases) {
            Assert.Equal(0, (await target.ExecAsync(["createdb", "-U", "postgres", db])).ExitCode);
            await target.CopyAsync(dumps[db], "/tmp/restore.sql");
            Assert.Equal(0, (await target.ExecAsync(["psql", "-U", "postgres", "-d", db, "-v", "ON_ERROR_STOP=on", "-f", "/tmp/restore.sql"])).ExitCode);
        }
        await s3.CopyObjectAsync(new CopyObjectArgs().WithBucket("restored-cvs").WithObject(document.ObjectName)
            .WithCopyObjectSource(new CopySourceObjectArgs().WithBucket("source-cvs").WithObject(document.ObjectName)));
        await using var restoredCandidates = new CandidateDbContext(new DbContextOptionsBuilder<CandidateDbContext>().UseNpgsql(Connection(target, databases[0])).Options);
        await using var restoredHiring = new HiringDbContext(new DbContextOptionsBuilder<HiringDbContext>().UseNpgsql(Connection(target, databases[1])).Options);
        await using var restoredOnboarding = new OnboardingDbContext(new DbContextOptionsBuilder<OnboardingDbContext>().UseNpgsql(Connection(target, databases[2])).Options);
        var restoredPerson = await restoredCandidates.Candidates.SingleAsync(); var restoredOffer = await restoredHiring.HiringProcesses.SingleAsync();
        var restoredEmployee = await restoredOnboarding.EmployeeOnboardings.Include(x => x.Tasks).SingleAsync();
        Assert.Equal(CandidateStatus.ReadyForHiring, restoredPerson.Status); Assert.Equal(restoredPerson.Id, restoredOffer.CandidateId);
        Assert.Equal(restoredOffer.Id, restoredEmployee.HiringProcessId); Assert.Equal(restoredEmployee.Id, restoredOffer.EmployeeId);
        Assert.Equal(HiringProcessStatus.Completed, restoredOffer.Status); Assert.Equal(EmployeeOnboardingStatus.Completed, restoredEmployee.Status);
        Assert.Equal(3, restoredEmployee.Tasks.Count); Assert.All(restoredEmployee.Tasks, t => Assert.Equal(OnboardingTaskStatus.Completed, t.Status));
        using var downloaded = new MemoryStream();
        await s3.GetObjectAsync(new GetObjectArgs().WithBucket("restored-cvs").WithObject(document.ObjectName).WithCallbackStream(s => s.CopyTo(downloaded)));
        Assert.Equal((await restoredCandidates.CandidateDocuments.SingleAsync()).ContentHash, Convert.ToHexStringLower(SHA256.HashData(downloaded.ToArray())));
        using var parsed = UglyToad.PdfPig.PdfDocument.Open(downloaded.ToArray()); Assert.Contains("C#", parsed.GetPage(1).Text);
        Assert.False(restoredCandidates.Database.HasPendingModelChanges()); Assert.False(restoredHiring.Database.HasPendingModelChanges()); Assert.False(restoredOnboarding.Database.HasPendingModelChanges());
    }
    private static byte[] Pdf()
    {
        const string content = "BT /F1 12 Tf 50 750 Td (Synthetic recovery C# CV) Tj ET";
        string[] objects = ["<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", $"<< /Length {content.Length} >>\nstream\n{content}\nendstream"];
        var text = "%PDF-1.4\n"; var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++) { offsets.Add(text.Length); text += $"{i + 1} 0 obj\n{objects[i]}\nendobj\n"; }
        var xref = text.Length; text += "xref\n0 6\n0000000000 65535 f \n" + string.Concat(offsets.Select(x => $"{x:0000000000} 00000 n \n"));
        return Encoding.ASCII.GetBytes(text + $"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
    }
}
