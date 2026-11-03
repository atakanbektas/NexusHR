using System.Net;
using System.Net.Http.Json;
using NexusHR.Candidate.Application.CvAnalyses;
namespace NexusHR.Candidate.IntegrationTests.Candidates;

public sealed class EngineeringAiAuthorizationTests(AiFixture fixture) : IClassFixture<AiFixture>
{
    [Theory]
    [InlineData("Admin",403)] [InlineData("HrSpecialist",403)] [InlineData("EngineeringReader",200)] [InlineData("EngineeringOperator",200)]
    public async Task AggregateTelemetryDoesNotGrantCvOrCandidateAccess(string role,int status)
    {
        using var client=fixture.Client(role,"synthetic-technical-user");
        using var response=await client.GetAsync("/api/engineering-ai");Assert.Equal(status,(int)response.StatusCode);
        if(status==200) {
            var view=await response.Content.ReadFromJsonAsync<AiDiagnostics>();Assert.NotNull(view);Assert.Equal("OpenAI",view.Provider);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/candidates?page=1&pageSize=20")).StatusCode);
            var body=await response.Content.ReadAsStringAsync();Assert.DoesNotContain("@example",body);Assert.DoesNotContain("quote",body,StringComparison.OrdinalIgnoreCase);
        }
    }
}
