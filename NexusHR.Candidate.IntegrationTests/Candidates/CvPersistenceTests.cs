using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nTest storage payload\n%%EOF");

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
    private static UploadCandidateCvCommand Command(Guid id) => new(id, "cv.pdf", "application/pdf", Pdf);
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
