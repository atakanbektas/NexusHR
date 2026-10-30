
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Minio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NexusHR.Candidate.Application;
using NexusHR.Candidate.Application.Abstractions.Storage;
using NexusHR.Candidate.Application.Candidates.UploadCandidateCv;
using NexusHR.Candidate.Domain.Candidates;
using NexusHR.Candidate.Infrastructure;
using NexusHR.Candidate.Infrastructure.Persistence;
using NexusHR.Candidate.Infrastructure.Storage;
using Testcontainers.PostgreSql;
using CandidateEntity = NexusHR.Candidate.Domain.Candidates.Candidate;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class CvPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly IContainer minio = new ContainerBuilder("nexushr-minio:2025-10-15")
        .WithEnvironment("MINIO_ROOT_USER", "test-access")
        .WithEnvironment("MINIO_ROOT_PASSWORD", "test-storage-password")
        .WithCommand("server", "/data")
        .WithPortBinding(9000, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
            request.ForPort(9000).ForPath("/minio/health/live"))).Build();
    private ServiceProvider services = null!;
    private static readonly byte[] Pdf = AiFixture.Pdf;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(postgres.StartAsync(), minio.StartAsync());
        services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CandidateDbContext>().Database.MigrateAsync();
    }

    private ServiceProvider CreateServices(ICandidateDocumentStorage? replacement = null)
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:CandidateDatabase"] = postgres.GetConnectionString(),
            ["Minio:Endpoint"] = $"{minio.Hostname}:{minio.GetMappedPublicPort(9000)}",
            ["Minio:AccessKey"] = "test-access", ["Minio:SecretKey"] = "test-storage-password",
            ["Minio:BucketName"] = "test-cvs", ["RabbitMq:Host"] = "localhost",
            ["RabbitMq:Username"] = "unused", ["RabbitMq:Password"] = "unused"
        }).Build();
        var registrations = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(settings);
        if (replacement is not null) registrations.Replace(ServiceDescriptor.Singleton(replacement));
        return registrations.BuildServiceProvider();
    }

    [Fact]
    public async Task UploadCommitsMetadataAndStatusAndStoresDownloadableObject()
    {
        var id = await SeedAsync();
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(Command(id));
        Assert.True(result.IsSuccess);
        var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>();
        db.ChangeTracker.Clear();
        Assert.Equal(CandidateStatus.DocumentsPending, (await db.Candidates.FindAsync(id))!.Status);
        var document = await db.CandidateDocuments.SingleAsync(doc => doc.CandidateId == id);
        Assert.Equal(Pdf, await scope.ServiceProvider.GetRequiredService<ICandidateDocumentStorage>()
            .DownloadAsync(document.ObjectName, default));
        Assert.False(await db.Set<CvUploadIntent>().AnyAsync(item => item.Id == document.Id));
    }

    [Fact]
    public async Task ConcurrentUploadsHaveOneWinnerAndCompensateTheLosingObject()
    {
        var id = await SeedAsync();
        await using var first = services.CreateAsyncScope();
        await using var second = services.CreateAsyncScope();
        var a = first.ServiceProvider.GetRequiredService<CandidateDbContext>();
        var b = second.ServiceProvider.GetRequiredService<CandidateDbContext>();
        var firstCandidate = await a.Candidates.SingleAsync(item => item.Id == id);
        var secondCandidate = await b.Candidates.SingleAsync(item => item.Id == id);
        var firstDocument = Document(id);
        var secondDocument = Document(id);
        await first.ServiceProvider.GetRequiredService<ICandidateCvUpload>().SaveAsync(firstCandidate, firstDocument, Pdf, default);
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => second.ServiceProvider.GetRequiredService<ICandidateCvUpload>()
            .SaveAsync(secondCandidate, secondDocument, Pdf, default));
        await using var verification = services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<CandidateDbContext>();
        Assert.Equal(1, await db.CandidateDocuments.CountAsync(doc => doc.CandidateId == id));
        Assert.False(await db.Set<CvUploadIntent>().AnyAsync(item => item.Id == secondDocument.Id));
        await Assert.ThrowsAnyAsync<Exception>(() => verification.ServiceProvider.GetRequiredService<ICandidateDocumentStorage>()
            .DownloadAsync(secondDocument.ObjectName, default));
        Assert.Equal(Pdf, await verification.ServiceProvider.GetRequiredService<ICandidateDocumentStorage>()
            .DownloadAsync(firstDocument.ObjectName, default));
    }

    [Fact]
    public async Task FailedCompensationRetainsIntentAndReconciliationRecoversIt()
    {
        var id = await SeedAsync();
        await using var failing = CreateServices(new FailingStorage());
        await using var scope = failing.CreateAsyncScope();
        await Assert.ThrowsAsync<IOException>(() => scope.ServiceProvider.GetRequiredService<ISender>().Send(Command(id)));
        await using var verification = services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<CandidateDbContext>();
        var intent = await db.Set<CvUploadIntent>().SingleAsync();
        Assert.Equal(CandidateStatus.Draft, (await db.Candidates.FindAsync(id))!.Status);
        Assert.False(await db.CandidateDocuments.AnyAsync(doc => doc.CandidateId == id));
        await CvUploadReconciler.CleanupAsync(services.GetRequiredService<IServiceScopeFactory>(), intent.Id, default);
        db.ChangeTracker.Clear();
        Assert.False(await db.Set<CvUploadIntent>().AnyAsync());
    }

    [Fact]
    public async Task UniqueEmailsAndStaleUpdatesAreRejectedByPostgres()
    {
        var id = await SeedAsync();
        await using var first = services.CreateAsyncScope();
        await using var second = services.CreateAsyncScope();
        var a = first.ServiceProvider.GetRequiredService<CandidateDbContext>();
        var b = second.ServiceProvider.GetRequiredService<CandidateDbContext>();
        var one = await a.Candidates.SingleAsync(item => item.Id == id);
        var two = await b.Candidates.SingleAsync(item => item.Id == id);
        one.UpdateInformation("Updated", one.LastName, one.Email, one.PhoneNumber);
        await a.SaveChangesAsync();
        two.Archive();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
        b.ChangeTracker.Clear();
        b.Candidates.Add(new CandidateEntity(Guid.NewGuid(), "Duplicate", "Email", one.Email, "05555555555"));
        await Assert.ThrowsAsync<DbUpdateException>(() => b.SaveChangesAsync());
    }

    private async Task<Guid> SeedAsync()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>();
        var candidate = new CandidateEntity(Guid.NewGuid(), "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", "05555555555");
        db.Add(candidate); await db.SaveChangesAsync(); return candidate.Id;
    }

    [Fact]
    public async Task ReplacementKeepsHistoryAndRejectsStaleVersionWithoutDeletingOldObject()
    {
        var id = await SeedAsync();
        Guid originalId;
        await using (var scope = services.CreateAsyncScope())
            originalId = (await scope.ServiceProvider.GetRequiredService<ISender>().Send(Command(id))).DocumentId!.Value;
        await using var first = services.CreateAsyncScope();
        await using var stale = services.CreateAsyncScope();
        var a = first.ServiceProvider.GetRequiredService<CandidateDbContext>();
        var b = stale.ServiceProvider.GetRequiredService<CandidateDbContext>();
        var one = await a.Candidates.SingleAsync(x => x.Id == id);
        var two = await b.Candidates.SingleAsync(x => x.Id == id);
        var next = Document(id); var losing = Document(id);
        await first.ServiceProvider.GetRequiredService<ICandidateCvUpload>().ReplaceAsync(one, next, originalId, Pdf, default);
        await Assert.ThrowsAsync<Application.CvAnalyses.CvAnalysisException>(() => stale.ServiceProvider.GetRequiredService<ICandidateCvUpload>()
            .ReplaceAsync(two, losing, originalId, Pdf, default));
        a.ChangeTracker.Clear();
        var docs = await a.CandidateDocuments.Where(x => x.CandidateId == id).OrderBy(x => x.Revision).ToListAsync();
        Assert.Equal(2, docs.Count); Assert.False(docs[0].IsCurrent); Assert.True(docs[1].IsCurrent);
        Assert.Equal(originalId, docs[1].PreviousDocumentId); Assert.Equal(2, docs[1].Revision);
        var storage = first.ServiceProvider.GetRequiredService<ICandidateDocumentStorage>();
        Assert.Equal(Pdf, await storage.DownloadAsync(docs[0].ObjectName, default));
        Assert.Equal(Pdf, await storage.DownloadAsync(docs[1].ObjectName, default));
        Assert.False(await a.Set<CvUploadIntent>().AnyAsync(x => x.Id == losing.Id));
    }

    [Fact]
    public async Task VerifiedCandidateCannotReplaceCv()
    {
        var id = await SeedAsync();
        await using var scope = services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var original = await sender.Send(Command(id));
        var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>();
        (await db.Candidates.FindAsync(id))!.MarkReadyForHiring(); await db.SaveChangesAsync();
        var replacement = await sender.Send(Command(id) with { ReplacesDocumentId = original.DocumentId });
        Assert.False(replacement.IsSuccess); Assert.Equal("Candidate.InvalidStatus", replacement.ErrorCode);
        Assert.Equal(1, await db.CandidateDocuments.CountAsync(x => x.CandidateId == id));
    }
    [Fact]
    public async Task SimultaneousVersionUpdatesHaveOneWinnerAndNoOrphanedObject()
    {
        var id = await SeedAsync(); Guid previous;
        await using (var seed = services.CreateAsyncScope())
            previous = (await seed.ServiceProvider.GetRequiredService<ISender>().Send(Command(id))).DocumentId!.Value;
        await using var a = services.CreateAsyncScope(); await using var b = services.CreateAsyncScope();
        var one = await a.ServiceProvider.GetRequiredService<CandidateDbContext>().Candidates.SingleAsync(x => x.Id == id);
        var two = await b.ServiceProvider.GetRequiredService<CandidateDbContext>().Candidates.SingleAsync(x => x.Id == id);
        var first = Document(id); var second = Document(id);
        static async Task<Exception?> Capture(Func<Task> action) { try { await action(); return null; } catch (Exception ex) { return ex; } }
        var results = await Task.WhenAll(
            Capture(() => a.ServiceProvider.GetRequiredService<ICandidateCvUpload>().ReplaceAsync(one, first, previous, Pdf, default)),
            Capture(() => b.ServiceProvider.GetRequiredService<ICandidateCvUpload>().ReplaceAsync(two, second, previous, Pdf, default)));
        Assert.Single(results, x => x is null);
        Assert.Single(results, x => x is DbUpdateException or Application.CvAnalyses.CvAnalysisException);
        await using var check = services.CreateAsyncScope(); var db = check.ServiceProvider.GetRequiredService<CandidateDbContext>();
        var documents = await db.CandidateDocuments.Where(x => x.CandidateId == id).ToListAsync();
        Assert.Equal(2, documents.Count); Assert.Single(documents, d => d.IsCurrent);
        Assert.False(await db.Set<CvUploadIntent>().AnyAsync(x => x.Id == first.Id || x.Id == second.Id));
        var losing = documents.Any(x => x.Id == first.Id) ? second : first;
        await Assert.ThrowsAnyAsync<Exception>(() => check.ServiceProvider.GetRequiredService<ICandidateDocumentStorage>().DownloadAsync(losing.ObjectName, default));
    }
    private static UploadCandidateCvCommand Command(Guid id) => new(id, "cv.pdf", "application/pdf", Pdf);
    [Fact]
    public async Task DatabaseDumpAndS3RestorePreserveVersionsAnalysesReviewsAndContentHashes()
    {
        var id = await SeedAsync();
        await using (var seed = services.CreateAsyncScope())
        {
            var sender = seed.ServiceProvider.GetRequiredService<ISender>(); var first = await sender.Send(Command(id));
            var second = await sender.Send(Command(id) with { ReplacesDocumentId = first.DocumentId }); Assert.True(second.IsSuccess);
            var db = seed.ServiceProvider.GetRequiredService<CandidateDbContext>();
            var pages = await new Infrastructure.Documents.PdfCvTextExtractor().ExtractPagesAsync(Pdf, default);
            var fact = new Application.CvAnalyses.CvFact("skill", "C#", 1, pages[0].Text.IndexOf("C#", StringComparison.Ordinal), "C#");
            var run = new Domain.CvAnalyses.CvAnalysis(id, second.DocumentId!.Value, "restore-test-hr", Guid.NewGuid().ToString(), "qwen3:4b", "Ollama", 0, DateTime.UtcNow);
            var lease = run.Claim(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            run.Finish(lease, Domain.CvAnalyses.CvAnalysisStatus.Succeeded, System.Text.Json.JsonSerializer.Serialize(new Application.CvAnalyses.CvAnalysisResult([fact], []), Infrastructure.CvAnalyses.CvAnalysisStore.Json), null, DateTime.UtcNow);
            db.Add(run); db.Add(new Domain.CvAnalyses.CvAnalysisReview { AnalysisId = run.Id, Reviewer = "restore-test-hr", Decision = "correct", FactIndex = 0, CorrectedValue = "C# reviewed", Revision = 1, CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        // Rehearsal targets are isolated disposable test containers, never business volumes.
        await using var targetDb = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await using var targetS3 = new ContainerBuilder("nexushr-minio:2025-10-15")
            .WithEnvironment("MINIO_ROOT_USER", "test-access").WithEnvironment("MINIO_ROOT_PASSWORD", "test-storage-password")
            .WithCommand("server", "/data").WithPortBinding(9000, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(9000).ForPath("/minio/health/live"))).Build();
        await Task.WhenAll(targetDb.StartAsync(), targetS3.StartAsync());
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(postgres.GetConnectionString());
        var dump = await postgres.ExecAsync(["pg_dump", "-U", connection.Username!, "-d", connection.Database!, "--no-owner", "--no-acl"]); Assert.Equal(0, dump.ExitCode);
        await targetDb.CopyAsync(System.Text.Encoding.UTF8.GetBytes(dump.Stdout), "/tmp/restore.sql");
        var targetConnection = new Npgsql.NpgsqlConnectionStringBuilder(targetDb.GetConnectionString());
        var restored = await targetDb.ExecAsync(["psql", "-U", targetConnection.Username!, "-d", targetConnection.Database!, "-v", "ON_ERROR_STOP=on", "-f", "/tmp/restore.sql"]); Assert.Equal(0, restored.ExitCode);
        using var targetClient = new Minio.MinioClient().WithEndpoint($"{targetS3.Hostname}:{targetS3.GetMappedPublicPort(9000)}").WithCredentials("test-access", "test-storage-password").Build();
        var targetStorage = new MinioCandidateDocumentStorage(targetClient, new() { BucketName = "restore-test-cvs" }, new Infrastructure.Documents.ClamAvDocumentScanner(new()));
        await using var sourceScope = services.CreateAsyncScope(); var sourceDb = sourceScope.ServiceProvider.GetRequiredService<CandidateDbContext>();
        var originalDocs = await sourceDb.CandidateDocuments.Where(d => d.CandidateId == id).ToListAsync();
        var originalStorage = sourceScope.ServiceProvider.GetRequiredService<ICandidateDocumentStorage>();
        foreach (var doc in originalDocs) await targetStorage.UploadAsync(doc.ObjectName, await originalStorage.DownloadAsync(doc.ObjectName, default), doc.ContentType, default);
        await using var restoredDb = new CandidateDbContext(new DbContextOptionsBuilder<CandidateDbContext>().UseNpgsql(targetDb.GetConnectionString()).Options);
        Assert.Equal(CandidateStatus.DocumentsPending, (await restoredDb.Candidates.FindAsync(id))!.Status);
        var documents = await restoredDb.CandidateDocuments.Where(d => d.CandidateId == id).ToListAsync(); Assert.Equal(2, documents.Count); Assert.Single(documents, d => d.IsCurrent);
        foreach (var doc in documents) Assert.Equal(doc.ContentHash, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(await targetStorage.DownloadAsync(doc.ObjectName, default))));
        var analysis = await restoredDb.Set<Domain.CvAnalyses.CvAnalysis>().SingleAsync(a => a.CandidateId == id); Assert.Equal(Domain.CvAnalyses.CvAnalysisStatus.Succeeded, analysis.Status);
        Assert.Equal("C# reviewed", (await restoredDb.Set<Domain.CvAnalyses.CvAnalysisReview>().SingleAsync(r => r.AnalysisId == analysis.Id)).CorrectedValue);
        Assert.Contains("C#", analysis.ResultJson!); Assert.False(restoredDb.Database.HasPendingModelChanges());
    }
    private static CandidateDocument Document(Guid id)
    {
        var documentId = Guid.NewGuid();
        return new(documentId, id, CandidateDocumentType.Cv, "cv.pdf", $"candidates/{id:N}/cv/{documentId:N}.pdf", "application/pdf", Pdf.Length);
    }
    private sealed class FailingStorage : ICandidateDocumentStorage
    {
        public Task UploadAsync(string name, byte[] content, string type, CancellationToken ct) => throw new IOException("Upload unavailable");
        public Task DeleteAsync(string name, CancellationToken ct) => throw new IOException("Delete unavailable");
        public Task<byte[]> DownloadAsync(string name, CancellationToken ct) => throw new IOException("Download unavailable");
    }
    public async Task DisposeAsync()
    {
        if (services is not null) await services.DisposeAsync();
        await minio.DisposeAsync(); await postgres.DisposeAsync();
    }
}
