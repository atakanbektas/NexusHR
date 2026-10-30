using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NexusHR.Candidate.Application.CvAnalyses;
using NexusHR.Candidate.Domain.CvAnalyses;
using NexusHR.Candidate.Infrastructure.CvAnalyses;

namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class OcrOllamaWorkflowTests
{
    [OcrOllamaFact]
    public async Task ActualScannedPdfPassesOcrMessagingRealOllamaGroundingAndPersistence()
    {
        await using var ocr = new ContainerBuilder("nexushr-local-ocr:1").WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(x => x.ForPort(8080).ForPath("/health"))).Build();
        await ocr.StartAsync();
        var source = await ocr.ExecAsync(["python3", "/app/fixtures.py", "scan"]); Assert.Equal(0, source.ExitCode);
        var pdf = Convert.FromBase64String(source.Stdout.Trim());
        var fixture = new AiFixture {
            ExtraConfiguration = new Dictionary<string, string?> {
                ["LocalDocuments:Enabled"] = "true", ["LocalDocuments:BaseUrl"] = $"http://{ocr.Hostname}:{ocr.GetMappedPublicPort(8080)}",
                ["CvAnalysis:Provider"] = "Ollama", ["CvAnalysis:Model"] = "qwen3:4b", ["CvAnalysis:MaxOutputTokens"] = "4000",
                ["CvAnalysis:TimeoutSeconds"] = "240", ["CvAnalysis:MaxParallelCalls"] = "1",
                ["CvAnalysis:Ollama:BaseUrl"] = Environment.GetEnvironmentVariable("OLLAMA_TEST_URL") ?? "http://127.0.0.1:11435",
            },
            ExtraServices = services => services.Replace(ServiceDescriptor.Singleton<ICvAnalysisProvider>(sp => {
                var options = sp.GetRequiredService<CvAnalysisOptions>();
                return new OllamaCvAnalysisProvider(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(options.Ollama.BaseUrl), Timeout = TimeSpan.FromSeconds(245) }, options);
            }))
        };
        try
        {
            await fixture.InitializeAsync(); using var client = fixture.Client();
            var created = await client.PostAsJsonAsync("/api/candidates", new { firstName = "Synthetic", lastName = "Person", email = $"ocr-{Guid.NewGuid():N}@example.test", phoneNumber = "05555555555" });
            created.EnsureSuccessStatusCode(); var candidate = (await created.Content.ReadFromJsonAsync<Created>())!.Id;
            using var upload = new MultipartFormDataContent(); var content = new ByteArrayContent(pdf); content.Headers.ContentType = new("application/pdf"); upload.Add(content, "file", "synthetic-scanned.pdf");
            (await client.PostAsync($"/api/candidates/{candidate}/documents/cv", upload)).EnsureSuccessStatusCode();
            var path = $"/api/candidates/{candidate}/cv-analyses"; var history = (await client.GetFromJsonAsync<AnalysisHistory>(path))!;
            client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var request = new RequestAnalysisBodyForTest(history.DocumentId!.Value);
            var queued = await client.PostAsJsonAsync(path, request); queued.EnsureSuccessStatusCode(); var run = (await queued.Content.ReadFromJsonAsync<AnalysisView>())!;
            await fixture.WaitAsync(db => db.Set<CvAnalysis>().AnyAsync(x => x.Id == run.Id && x.Delivered));
            Assert.True(await CvAnalysisWorker.ProcessNextAsync(fixture.Scopes, fixture.Options, default));
            var result = (await client.GetFromJsonAsync<AnalysisView>($"{path}/{run.Id}"))!;
            Assert.Equal("Succeeded", result.Status); Assert.NotNull(result.Result);
            Assert.All(result.Result.Facts, fact => { Assert.StartsWith("tesseract", fact.Source); Assert.NotEmpty(fact.Regions); });
            foreach (var category in new[] { "skill", "employment", "education" }) Assert.Contains(result.Result.Facts, f => f.Category == category);
            Assert.Contains(result.Result.Facts, f => f.Fields?.Count > 0);
            Assert.Equal(0, Assert.Single(result.Attempts).UsageCostUsd);
            var replay = await client.PostAsJsonAsync(path, request); replay.EnsureSuccessStatusCode(); Assert.Equal(run.Id, (await replay.Content.ReadFromJsonAsync<AnalysisView>())!.Id);
            await using var scope = fixture.Scopes.CreateAsyncScope();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.CandidateDbContext>().Set<CvAnalysisAttempt>().CountAsync(x => x.AnalysisId == run.Id));
        }
        finally { await fixture.DisposeAsync(); }
    }
    private sealed record Created(Guid Id);
    private sealed record RequestAnalysisBodyForTest(Guid DocumentId);
}
internal sealed class OcrOllamaFactAttribute : FactAttribute
{
    public OcrOllamaFactAttribute() { if (Environment.GetEnvironmentVariable("RUN_OCR_OLLAMA_TESTS") != "1") Skip = "Opt-in actual OCR + actual Ollama + PostgreSQL/RabbitMQ/MinIO workflow."; }
}
