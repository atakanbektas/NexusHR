using System.Net;
using System.Net.Http.Json;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusHR.Candidate.Application.Abstractions.Documents;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Domain.CvAnalyses;
using NexusHR.Candidate.Infrastructure.CvAnalyses;
using NexusHR.Candidate.Infrastructure.Persistence;
using NexusHR.Contracts.Candidates;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class CvAnalysisIntegrationTests(AiFixture f) : IClassFixture<AiFixture>
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NativePartialEvidenceIsPersistedButAllRejectedEvidenceIsNoFindings(bool includeValid)
    {
        var (candidate, document) = await f.SeedAsync(); using var client = f.Client();
        var key = Guid.NewGuid().ToString(); var run = await Create(client, candidate, document, key);
        var original = f.Provider.Override;
        f.Provider.Override = async (pages, ct) =>
        {
            var facts = new List<object> { new { category = "skill", value = "Invented", page = 1, quote = "Unsupported phrase" } };
            if (includeValid) facts.Add(new { category = "skill", value = "C#", page = 1, quote = "C#" });
            using var http = new HttpClient(new OllamaEvidenceRegressionTests.NativeReply(System.Text.Json.JsonSerializer.Serialize(new { facts, warnings = Array.Empty<string>() })))
                { BaseAddress = new Uri("http://ollama:11434") };
            return await new OllamaCvAnalysisProvider(http, new() { Enabled = true, Provider = "Ollama", Model = "qwen3:4b" }).AnalyzeAsync(pages, "qwen3:4b", ct);
        };
        try { await Deliver(run.Id); Assert.True(await CvAnalysisWorker.ProcessNextAsync(f.Scopes, f.Options, default)); }
        finally { f.Provider.Override = original; }
        var result = await Get(client, candidate, run.Id);
        Assert.Equal(includeValid ? "Succeeded" : "NoFindings", result.Status);
        if (includeValid)
        {
            Assert.Equal("C#", Assert.Single(result.Result!.Facts).Value);
            Assert.Contains(result.Result.Warnings, w => w.Contains("1 bulgu", StringComparison.Ordinal));
        }
        else { Assert.Null(result.Result); Assert.Equal("AI.NoSupportedFacts", result.ErrorCode); }
        var before = f.Provider.Calls;
        Assert.Equal(run.Id, (await Create(client, candidate, document, key)).Id);
        Assert.Equal(before, f.Provider.Calls); Assert.Single(result.Attempts);
    }
    [Fact]
    public async Task OutboxInboxFakeProviderEvidenceAndHumanReviewCompleteWithoutChangingCandidate()
    {
        var (candidate, document) = await f.SeedAsync(); using var client = f.Client();
        var key = Guid.NewGuid().ToString(); var run = await Create(client, candidate, document, key);
        Assert.Equal(run.Id, (await Create(client, candidate, document, key)).Id);
        await Deliver(run.Id);
        var messageId = Guid.NewGuid(); var bus = f.App.Services.GetRequiredService<IBus>();
        await bus.Publish(new CvAnalysisRequested(run.Id), x => x.MessageId = messageId);
        await bus.Publish(new CvAnalysisRequested(run.Id), x => x.MessageId = messageId);
        await f.WaitAsync(db => db.Set<InboxState>().AnyAsync(x => x.MessageId == messageId && x.Consumed != null));
        var before = f.Provider.Calls;
        var claims = await Task.WhenAll(CvAnalysisWorker.ProcessNextAsync(f.Scopes, f.Options, default), CvAnalysisWorker.ProcessNextAsync(f.Scopes, f.Options, default));
        Assert.Single(claims, x => x); Assert.Equal(before + 1, f.Provider.Calls);
        run = await Get(client, candidate, run.Id); Assert.Equal("Succeeded", run.Status);
        Assert.Equal("C#", Assert.Single(run.Result!.Facts).Value); Assert.NotNull(run.ContentHash);
        Assert.Equal(.0002m, Assert.Single(run.Attempts).UsageCostUsd);
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", $"\"{run.Version}\"");
        var reviewed = await client.PutAsJsonAsync($"{Path(candidate)}/{run.Id}/review", new { decision = "correct", notes = "HR confirmed C#; scope limited to documented evidence." });
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        Assert.Equal("correct", Assert.Single((await reviewed.Content.ReadFromJsonAsync<AnalysisView>())!.Reviews).Decision);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"{Path(candidate)}/{run.Id}/review", new { decision = "approve" })).StatusCode);
        await using var scope = f.Scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>();
        Assert.Equal(Domain.Candidates.CandidateStatus.Draft, (await db.Candidates.SingleAsync(x => x.Id == candidate)).Status);
        Assert.Equal(run.ContentHash, (await db.CandidateDocuments.SingleAsync(x => x.Id == document)).ContentHash);
    }

    [Fact]
    public async Task IdempotencyAndActiveUniquenessAreSerializedUnderPostgresRaces()
    {
        var (candidate, document) = await f.SeedAsync(); var key = Guid.NewGuid().ToString();
        using var one = f.Client(); using var two = f.Client();
        var results = await Task.WhenAll(Create(one, candidate, document, key), Create(two, candidate, document, key));
        Assert.Equal(results[0].Id, results[1].Id);
        var conflict = await one.PostAsJsonAsync(Path(candidate), new { documentId = document });
        // Missing idempotency header is rejected by the API contract.
        Assert.Equal(HttpStatusCode.BadRequest, conflict.StatusCode);
        one.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await one.PostAsJsonAsync(Path(candidate), new { documentId = document })).StatusCode);
        await Cancel(one, candidate, await Get(one, candidate, results[0].Id));
    }

    [Fact]
    public async Task PerFactHumanCorrectionIsAuditedSeparatelyWithVersionAndRoleChecks()
    {
        var (candidate, document) = await f.SeedAsync(); using var client = f.Client();
        var run = await Create(client, candidate, document, Guid.NewGuid().ToString()); await Deliver(run.Id);
        await CvAnalysisWorker.ProcessNextAsync(f.Scopes, f.Options, default);
        run = await Get(client, candidate, run.Id);
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", $"\"{run.Version}\"");
        var response = await client.PutAsJsonAsync($"{Path(candidate)}/{run.Id}/review", new { decision = "correct", factIndex = 0, correctedValue = "Human annotation" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reviewed = (await response.Content.ReadFromJsonAsync<AnalysisView>())!;
        Assert.Equal("C#", reviewed.Result!.Facts[0].Value);
        Assert.Equal("Human annotation", Assert.Single(reviewed.Reviews).CorrectedValue);
        Assert.Equal(0, reviewed.Reviews[0].FactIndex);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"{Path(candidate)}/{run.Id}/review", new { decision = "reject", factIndex = 0 })).StatusCode);
        using var auditor = f.Client("Auditor"); auditor.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", $"\"{reviewed.Version}\"");
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PutAsJsonAsync($"{Path(candidate)}/{run.Id}/review", new { decision = "approve", factIndex = 0 })).StatusCode);
        using var admin = f.Client("Admin");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/candidates/{candidate}/documents/cv/versions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client("DepartmentManager").GetAsync($"/api/candidates/{candidate}/documents/cv/versions/{document}")).StatusCode);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("DepartmentManager", 403)]
    [InlineData("Admin", 403)]
    [InlineData("ItSpecialist", 403)]
    [InlineData("HrManager", 200)]
    [InlineData("Auditor", 200)]
    public async Task AnalysisHasExactlyCvReaderPermissions(string? role, int status)
    {
        var (candidate, _) = await f.SeedAsync(); using var client = f.Client(role);
        Assert.Equal(status, (int)(await client.GetAsync(Path(candidate))).StatusCode);
        Assert.Equal(role is null ? 401 : 403, (int)(await client.PostAsJsonAsync(Path(candidate), new { documentId = Guid.NewGuid() })).StatusCode);
    }

    [Fact]
    public async Task FeatureOffAndExternalConsentGateDoNotInvokeProvider()
    {
        var (candidate, document) = await f.SeedAsync(); using var client = f.Client();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var before = f.Provider.Calls;
        f.Options.Enabled = false;
        try { Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync(Path(candidate), new { documentId = document })).StatusCode); }
        finally { f.Options.Enabled = true; }
        f.Options.AllowExternalProcessing = true;
        try { Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Path(candidate), new { documentId = document })).StatusCode); }
        finally { f.Options.AllowExternalProcessing = false; }
        Assert.Equal(before, f.Provider.Calls);
    }

    [Fact]
    public async Task BudgetAndUserQuotaReservationsCannotOverspendUnderConcurrentRequests()
    {
        var a = await f.SeedAsync(); var b = await f.SeedAsync();
        using var one = f.Client(user: Guid.NewGuid().ToString()); using var two = f.Client(user: Guid.NewGuid().ToString());
        decimal committed;
        await using (var scope = f.Scopes.CreateAsyncScope()) committed = await scope.ServiceProvider.GetRequiredService<CandidateDbContext>()
            .Set<AiDailyBudget>().Where(x => x.Day == DateOnly.FromDateTime(DateTime.UtcNow)).Select(x => x.CommittedUsd).SingleOrDefaultAsync();
        var previous = f.Options.DailyBudgetUsd; f.Options.DailyBudgetUsd = committed + f.Options.Reservation;
        try
        {
            one.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString()); two.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var replies = await Task.WhenAll(one.PostAsJsonAsync(Path(a.Candidate), new { documentId = a.Document }), two.PostAsJsonAsync(Path(b.Candidate), new { documentId = b.Document }));
            Assert.Single(replies, x => x.StatusCode == HttpStatusCode.Accepted);
            Assert.Single(replies, x => (int)x.StatusCode == 429);
            var index = replies[0].StatusCode == HttpStatusCode.Accepted ? 0 : 1;
            var winner = (await replies[index].Content.ReadFromJsonAsync<AnalysisView>())!;
            await Cancel(index == 0 ? one : two, index == 0 ? a.Candidate : b.Candidate, winner);
        }
        finally { f.Options.DailyBudgetUsd = previous; }
        var user = Guid.NewGuid().ToString(); using var quota = f.Client(user: user);
        var limit = f.Options.RequestsPerUserPerDay; f.Options.RequestsPerUserPerDay = 1;
        try
        {
            var run = await Create(quota, a.Candidate, a.Document);
            await Cancel(quota, a.Candidate, run);
            quota.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
            Assert.Equal(429, (int)(await quota.PostAsJsonAsync(Path(b.Candidate), new { documentId = b.Document })).StatusCode);
        }
        finally { f.Options.RequestsPerUserPerDay = limit; }
    }

    [Fact]
    public async Task UnknownOutcomeIsNotAutomaticallyRetriedOrRefundedAndRequiresExplicitAcknowledgement()
    {
        var (candidate, document) = await f.SeedAsync(); using var client = f.Client();
        var before = f.Provider.Calls;
        f.Provider.Override = (_, _) => throw new CvProviderException("AI.OutcomeUnknown");
        try
        {
            var run = await Create(client, candidate, document); await Deliver(run.Id); await Process();
            run = await Get(client, candidate, run.Id); Assert.Equal("OutcomeUnknown", run.Status);
            Assert.Null(Assert.Single(run.Attempts).UsageCostUsd); Assert.Equal(before + 1, f.Provider.Calls);
            Assert.False(await CvAnalysisWorker.ProcessNextAsync(f.Scopes, f.Options, default));
            client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", $"\"{run.Version}\"");
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Path(candidate)}/{run.Id}/retry", new { acknowledgePossibleDuplicateCharge = false })).StatusCode);
            var retried = await client.PostAsJsonAsync($"{Path(candidate)}/{run.Id}/retry", new { acknowledgePossibleDuplicateCharge = true });
            Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
            await Cancel(client, candidate, (await retried.Content.ReadFromJsonAsync<AnalysisView>())!);
        }
        finally { f.Provider.Override = null; }
    }

    [Fact]
    public async Task ExpiredDispatchedLeaseBecomesUnknownWithoutSecondProviderCall()
    {
        var (candidate, document) = await f.SeedAsync(); using var client = f.Client(); var run = await Create(client, candidate, document);
        await Deliver(run.Id);
        await using (var scope = f.Scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>(); var entity = await db.Set<CvAnalysis>().SingleAsync(x => x.Id == run.Id);
            entity.Claim(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            db.Entry(entity).Property(x => x.LeaseExpiresAtUtc).CurrentValue = DateTime.UtcNow.AddMinutes(-5);
            db.Add(new CvAnalysisAttempt { AnalysisId = run.Id, Number = 1, StartedAtUtc = DateTime.UtcNow.AddMinutes(-5), DispatchedAtUtc = DateTime.UtcNow.AddMinutes(-5), ReservedUsd = entity.ReservedUsd });
            await db.SaveChangesAsync();
        }
        var calls = f.Provider.Calls; await CvAnalysisWorker.RecoverAsync(f.Scopes, default);
        Assert.Equal("OutcomeUnknown", (await Get(client, candidate, run.Id)).Status); Assert.Equal(calls, f.Provider.Calls);
    }

    [Fact]
    public async Task StaleCancellationAndCrossCandidateAccessAreRejected()
    {
        var (candidate, document) = await f.SeedAsync(); using var client = f.Client(); var run = await Create(client, candidate, document);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Path(Guid.NewGuid())}/{run.Id}")).StatusCode);
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", "\"0\"");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"{Path(candidate)}/{run.Id}/cancel", null)).StatusCode);
        await Cancel(client, candidate, await Get(client, candidate, run.Id));
        var calls = f.Provider.Calls;
        Assert.False(await CvAnalysisWorker.ProcessNextAsync(f.Scopes, f.Options, default)); Assert.Equal(calls, f.Provider.Calls);
    }

    [Fact]
    public async Task ExtractionPreservesContactBehaviourAndAddsStableEvidencePages()
    {
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            await using var scope = f.Scopes.CreateAsyncScope();
            var extractor = scope.ServiceProvider.GetRequiredService<ICvInformationExtractor>();
            var draft = await extractor.ExtractAsync(AiFixture.Pdf, default);
            var pages = await scope.ServiceProvider.GetRequiredService<ICvTextExtractor>().ExtractPagesAsync(AiFixture.Pdf, default);
            Assert.True(draft.Email == "synthetic@example.test", pages[0].Text);
            Assert.Equal(1, Assert.Single(pages).Number); Assert.Contains("C# PostgreSQL", pages[0].Text);
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            Assert.Equal(draft, await extractor.ExtractAsync(AiFixture.Pdf, default));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = originalCulture; }
    }

    [Fact]
    public async Task OutboxRetainsRequestDuringBrokerOutageAndDeliversAfterRecovery()
    {
        var (candidate, document) = await f.SeedAsync(); using var client = f.Client();
        Assert.Equal(0, (await f.Broker.ExecAsync(["rabbitmqctl", "stop_app"])).ExitCode);
        AnalysisView run;
        try
        {
            run = await Create(client, candidate, document);
            await using var scope = f.Scopes.CreateAsyncScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<CandidateDbContext>().Set<OutboxMessage>().AnyAsync());
        }
        finally { Assert.Equal(0, (await f.Broker.ExecAsync(["rabbitmqctl", "start_app"])).ExitCode); }
        await Deliver(run.Id); await Process(); Assert.Equal("Succeeded", (await Get(client, candidate, run.Id)).Status);
    }

    [Fact]
    public async Task RunningCancellationRetainsMeasuredCostAndDiscardsResult()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider.Override = async (pages, ct) =>
        {
            entered.SetResult(); await release.Task.WaitAsync(ct);
            var page = pages[0]; return new(new([new("skill", "C#", 1, page.Text.IndexOf("C#", StringComparison.Ordinal), "C#")], []), "cancelled-test", 100, 50);
        };
        try
        {
            var (candidate, document) = await f.SeedAsync(); using var client = f.Client(); var run = await Create(client, candidate, document);
            await Deliver(run.Id); var process = CvAnalysisWorker.ProcessNextAsync(f.Scopes, f.Options, default);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Cancel(client, candidate, await Get(client, candidate, run.Id)); release.SetResult(); await process;
            run = await Get(client, candidate, run.Id); Assert.Equal("Cancelled", run.Status); Assert.Null(run.Result);
            Assert.Equal(.0002m, Assert.Single(run.Attempts).UsageCostUsd);
        }
        finally { release.TrySetResult(); f.Provider.Override = null; }
    }

    [Fact]
    public async Task RateLimitedCallsRetryWithinAttemptCapAndPersistBothUsageRecords()
    {
        var calls = 0;
        f.Provider.Override = (pages, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1) throw new CvProviderException("AI.RateLimited", true, TimeSpan.FromMilliseconds(50));
            var page = pages[0]; return Task.FromResult(new CvProviderResponse(new([new("skill", "C#", 1,
                page.Text.IndexOf("C#", StringComparison.Ordinal), "C#")], []), "retry-test", 100, 50));
        };
        try
        {
            var (candidate, document) = await f.SeedAsync(); using var client = f.Client(); var run = await Create(client, candidate, document);
            await Deliver(run.Id); await Process(); run = await Get(client, candidate, run.Id);
            Assert.Equal("Queued", run.Status); Assert.Equal(0m, Assert.Single(run.Attempts).UsageCostUsd);
            await Task.Delay(100); await Process(); run = await Get(client, candidate, run.Id);
            Assert.Equal("Succeeded", run.Status); Assert.Equal(2, run.Attempts.Count); Assert.Equal(2, calls);
        }
        finally { f.Provider.Override = null; }
    }

    [Fact]
    public async Task UnsupportedEvidenceIsRejectedAndBilledUsageIsStillAccounted()
    {
        f.Provider.Override = (_, _) => Task.FromResult(new CvProviderResponse(new([new("skill", "Invented", 1, 0, "Invented")], []), "invalid-test", 100, 50));
        try
        {
            var (candidate, document) = await f.SeedAsync(); using var client = f.Client(); var run = await Create(client, candidate, document);
            await Deliver(run.Id); await Process(); run = await Get(client, candidate, run.Id);
            Assert.Equal("Failed", run.Status); Assert.Equal("AI.InvalidInputOrEvidence", run.ErrorCode);
            Assert.Null(run.Result); Assert.Equal(.0002m, Assert.Single(run.Attempts).UsageCostUsd);
        }
        finally { f.Provider.Override = null; }
    }

    [Fact]
    public async Task DatabaseConstraintsAndXminProtectAgainstWritersBypassingPrechecks()
    {
        var (candidate, document) = await f.SeedAsync(); using var client = f.Client(); var run = await Create(client, candidate, document);
        await Deliver(run.Id);
        await using (var duplicateScope = f.Scopes.CreateAsyncScope())
        {
            var db = duplicateScope.ServiceProvider.GetRequiredService<CandidateDbContext>();
            db.Add(new CvAnalysis(candidate, document, "other-hr", Guid.NewGuid().ToString(), "test", "test", .1m, DateTime.UtcNow));
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal("23505", Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
        }
        await using var staleScope = f.Scopes.CreateAsyncScope(); var staleDb = staleScope.ServiceProvider.GetRequiredService<CandidateDbContext>();
        var stale = await staleDb.Set<CvAnalysis>().SingleAsync(x => x.Id == run.Id);
        await Cancel(client, candidate, await Get(client, candidate, run.Id));
        stale.RequestCancellation(DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleDb.SaveChangesAsync());
    }

    [Fact]
    public async Task OvernightQueueCannotSpendAnOldReservationWithoutReservingTodaysBudget()
    {
        var (candidate, document) = await f.SeedAsync(); using var client = f.Client();
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var entity = new CvAnalysis(candidate, document, "test-hr", Guid.NewGuid().ToString(), f.Options.Model,
            "OpenAI", f.Options.Reservation, yesterday); entity.MarkDelivered();
        await using (var scope = f.Scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>();
            db.Add(entity); db.Add(new AiDailyBudget { Day = DateOnly.FromDateTime(yesterday), CommittedUsd = f.Options.Reservation });
            await db.SaveChangesAsync();
        }
        var before = f.Provider.Calls; await Process(); var run = await Get(client, candidate, entity.Id);
        Assert.Equal("Failed", run.Status); Assert.Equal("AI.ReservationExpiredRetryRequired", run.ErrorCode);
        Assert.Equal(before, f.Provider.Calls); Assert.Equal(0m, Assert.Single(run.Attempts).UsageCostUsd);
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", $"\"{run.Version}\"");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{Path(candidate)}/{run.Id}/retry", new { acknowledgePossibleDuplicateCharge = false })).StatusCode);
        await Process(); run = await Get(client, candidate, run.Id); Assert.Equal("Succeeded", run.Status);
        await using var verification = f.Scopes.CreateAsyncScope(); var context = verification.ServiceProvider.GetRequiredService<CandidateDbContext>();
        Assert.Equal(0m, (await context.Set<AiDailyBudget>().SingleAsync(x => x.Day == DateOnly.FromDateTime(yesterday))).CommittedUsd);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), (await context.Set<CvAnalysis>().SingleAsync(x => x.Id == run.Id)).ReservationDay);
    }

    [Fact]
    public async Task LocalProviderHasZeroPaidReservationButStillEnforcesUserQuotaAndReadiness()
    {
        var provider = f.Options.Provider; var limit = f.Options.RequestsPerUserPerDay;
        f.Options.Provider = "Ollama"; f.Options.RequestsPerUserPerDay = 1;
        try
        {
            var (candidate, document) = await f.SeedAsync(); using var client = f.Client(user: Guid.NewGuid().ToString());
            f.Provider.UnavailableCode = "AI.ModelNotReady";
            client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync(Path(candidate), new { documentId = document })).StatusCode);
            client.DefaultRequestHeaders.Remove("Idempotency-Key"); f.Provider.UnavailableCode = null;
            decimal before;
            await using (var scope = f.Scopes.CreateAsyncScope()) before = await scope.ServiceProvider.GetRequiredService<CandidateDbContext>()
                .Set<AiDailyBudget>().SumAsync(x => x.CommittedUsd);
            var run = await Create(client, candidate, document); Assert.Equal("Ollama", run.Provider);
            await Deliver(run.Id); await Process(); run = await Get(client, candidate, run.Id);
            Assert.Equal("Succeeded", run.Status); var attempt = Assert.Single(run.Attempts);
            Assert.Equal(0, attempt.ReservedUsd); Assert.Equal(0m, attempt.UsageCostUsd);
            client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
            Assert.Equal(429, (int)(await client.PostAsJsonAsync(Path(candidate), new { documentId = document })).StatusCode);
            await using var verification = f.Scopes.CreateAsyncScope();
            Assert.Equal(before, await verification.ServiceProvider.GetRequiredService<CandidateDbContext>().Set<AiDailyBudget>().SumAsync(x => x.CommittedUsd));
        }
        finally { f.Options.Provider = provider; f.Options.RequestsPerUserPerDay = limit; f.Provider.UnavailableCode = null; }
    }

    [Fact]
    public async Task LocalRunningCancellationStopsTheCallAndPersistsZeroCost()
    {
        var provider = f.Options.Provider; f.Options.Provider = "Ollama";
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Provider.Override = async (_, ct) => { entered.SetResult(); await Task.Delay(Timeout.Infinite, ct); throw new InvalidOperationException(); };
        try
        {
            var (candidate, document) = await f.SeedAsync(); using var client = f.Client(); var run = await Create(client, candidate, document);
            await Deliver(run.Id); var running = CvAnalysisWorker.ProcessNextAsync(f.Scopes, f.Options, default);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); await Cancel(client, candidate, await Get(client, candidate, run.Id));
            await running.WaitAsync(TimeSpan.FromSeconds(10)); run = await Get(client, candidate, run.Id);
            Assert.Equal("Cancelled", run.Status); Assert.Equal(0m, Assert.Single(run.Attempts).UsageCostUsd);
        }
        finally { f.Options.Provider = provider; f.Provider.Override = null; }
    }

    [Fact]
    public async Task RestartedLocalWorkerRecoversDispatchedJobsAsRetryableFailuresWithoutUnknownCharges()
    {
        var provider = f.Options.Provider; f.Options.Provider = "Ollama";
        try
        {
            var (candidate, document) = await f.SeedAsync(); using var client = f.Client(); var run = await Create(client, candidate, document);
            await Deliver(run.Id);
            await using (var scope = f.Scopes.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>(); var entity = await db.Set<CvAnalysis>().SingleAsync(x => x.Id == run.Id);
                entity.Claim(DateTime.UtcNow, TimeSpan.FromSeconds(1)); db.Entry(entity).Property(x => x.LeaseExpiresAtUtc).CurrentValue = DateTime.UtcNow.AddMinutes(-1);
                db.Add(new CvAnalysisAttempt { AnalysisId = run.Id, Number = 1, StartedAtUtc = DateTime.UtcNow.AddMinutes(-1), DispatchedAtUtc = DateTime.UtcNow.AddMinutes(-1) });
                await db.SaveChangesAsync();
            }
            await CvAnalysisWorker.RecoverAsync(f.Scopes, default); run = await Get(client, candidate, run.Id);
            Assert.Equal("Failed", run.Status); Assert.Equal(0m, Assert.Single(run.Attempts).UsageCostUsd);
            client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", $"\"{run.Version}\"");
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{Path(candidate)}/{run.Id}/retry", new { acknowledgePossibleDuplicateCharge = false })).StatusCode);
            await Process(); Assert.Equal("Succeeded", (await Get(client, candidate, run.Id)).Status);
        }
        finally { f.Options.Provider = provider; }
    }

    [Fact]
    public async Task ContactOnlyPdfStopsBeforeModelCallAndIsNotSuccessful()
    {
        var provider = f.Options.Provider; f.Options.Provider = "Ollama";
        try
        {
            var (candidate, document) = await f.SeedAsync(AiFixture.ContactOnlyPdf, "Ada", "Lovelace");
            using var client = f.Client(); var before = f.Provider.Calls; var run = await Create(client, candidate, document);
            await Deliver(run.Id); await Process(); run = await Get(client, candidate, run.Id);
            Assert.Equal("NoFindings", run.Status); Assert.Equal("AI.NoAnalyzableText", run.ErrorCode);
            Assert.Equal(before, f.Provider.Calls); Assert.Null(run.Result); Assert.Equal(0m, Assert.Single(run.Attempts).UsageCostUsd);
            await using var scope = f.Scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>();
            Assert.Null((await db.Set<CvAnalysisAttempt>().SingleAsync(x => x.AnalysisId == run.Id)).DispatchedAtUtc);
        }
        finally { f.Options.Provider = provider; }
    }

    [Fact]
    public async Task ReadableProfessionalCvWithEmptyModelFactsIsNoFindingsAndRetainsUsageAndRetryGuards()
    {
        f.Provider.Override = (_, _) => Task.FromResult(new CvProviderResponse(new([], ["No content provided for PAGE 1", "No facts found for summary, skill, employment, education, language, certification"]), "empty-test", 100, 50));
        try
        {
            var (candidate, document) = await f.SeedAsync(); using var client = f.Client(); var key = Guid.NewGuid().ToString();
            var run = await Create(client, candidate, document, key); await Deliver(run.Id); await Process(); run = await Get(client, candidate, run.Id);
            Assert.Equal("NoFindings", run.Status); Assert.Equal("AI.NoSupportedFacts", run.ErrorCode); Assert.Null(run.Result);
            Assert.Equal(.0002m, Assert.Single(run.Attempts).UsageCostUsd); Assert.Equal("NoFindings", run.Attempts[0].Outcome);
            Assert.Equal(run.Id, (await Create(client, candidate, document, key)).Id);
            client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", $"\"{run.Version}\"");
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"{Path(candidate)}/{run.Id}/review", new { decision = "approve" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{Path(candidate)}/{run.Id}/retry", new { acknowledgePossibleDuplicateCharge = false })).StatusCode);
            f.Provider.Override = null; await Process(); Assert.Equal("Succeeded", (await Get(client, candidate, run.Id)).Status);
        }
        finally { f.Provider.Override = null; }
    }

    [Fact]
    public async Task ImageOnlyPdfFailsWithOcrMessageWithoutCallingTheModel()
    {
        var (candidate, document) = await f.SeedAsync(AiFixture.ImageOnlyPdf); using var client = f.Client();
        var before = f.Provider.Calls; var run = await Create(client, candidate, document); await Deliver(run.Id); await Process();
        run = await Get(client, candidate, run.Id); Assert.Equal("Failed", run.Status); Assert.Equal("AI.NoReadableText", run.ErrorCode);
        Assert.Equal(before, f.Provider.Calls); Assert.Equal(0m, Assert.Single(run.Attempts).UsageCostUsd);
    }

    [Fact]
    public async Task LegacyEmptySuccessCannotBeApprovedAndIsNotRewritten()
    {
        var (candidate, document) = await f.SeedAsync(); using var client = f.Client();
        var legacy = new CvAnalysis(candidate, document, "test-hr", Guid.NewGuid().ToString(), f.Options.Model, "OpenAI", 0, DateTime.UtcNow);
        var lease = legacy.Claim(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        legacy.Finish(lease, CvAnalysisStatus.Succeeded, "{\"facts\":[],\"warnings\":[]}", null, DateTime.UtcNow);
        await using (var scope = f.Scopes.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<CandidateDbContext>(); db.Add(legacy); await db.SaveChangesAsync(); }
        var run = await Get(client, candidate, legacy.Id); client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", $"\"{run.Version}\"");
        var review = await client.PutAsJsonAsync($"{Path(candidate)}/{run.Id}/review", new { decision = "approve" });
        Assert.Equal(HttpStatusCode.Conflict, review.StatusCode); Assert.Contains("AI.EmptyResultCannotBeReviewed", await review.Content.ReadAsStringAsync());
        Assert.Equal("Succeeded", (await Get(client, candidate, run.Id)).Status); // Preserve the historical audit record.
    }

    private async Task Process() => Assert.True(await CvAnalysisWorker.ProcessNextAsync(f.Scopes, f.Options, default));
    private Task Deliver(Guid id) => f.WaitAsync(db => db.Set<CvAnalysis>().AnyAsync(x => x.Id == id && x.Delivered));
    private static string Path(Guid candidate) => $"/api/candidates/{candidate}/cv-analyses";
    private static async Task<AnalysisView> Get(HttpClient client, Guid candidate, Guid id) => (await client.GetFromJsonAsync<AnalysisView>($"{Path(candidate)}/{id}"))!;
    private static async Task<AnalysisView> Create(HttpClient client, Guid candidate, Guid document, string? key = null)
    {
        client.DefaultRequestHeaders.Remove("Idempotency-Key"); client.DefaultRequestHeaders.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        var response = await client.PostAsJsonAsync(Path(candidate), new { documentId = document });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode); client.DefaultRequestHeaders.Remove("Idempotency-Key");
        return (await response.Content.ReadFromJsonAsync<AnalysisView>())!;
    }
    private async Task Cancel(HttpClient client, Guid candidate, AnalysisView run)
    {
        await Deliver(run.Id);
        run = await Get(client, candidate, run.Id);
        client.DefaultRequestHeaders.Remove("If-Match"); client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", $"\"{run.Version}\"");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{Path(candidate)}/{run.Id}/cancel", null)).StatusCode);
        client.DefaultRequestHeaders.Remove("If-Match");
    }
}
