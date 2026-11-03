using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NexusHR.BuildingBlocks.Hosting;
using NexusHR.Gateway.Engineering;
using Npgsql;
using Testcontainers.PostgreSql;

namespace NexusHR.Platform.Tests;

public sealed class EngineeringFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:16-alpine").WithDatabase("nexushr_engineering").Build();
    public WebApplication App { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public EngineeringStore Store => App.Services.GetRequiredService<EngineeringStore>();
    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" }); builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> { ["Engineering:Enabled"]="true", ["Engineering:ExecutionEnabled"]="true", ["Engineering:InitializeDatabase"]="true", ["Engineering:Database"]=postgres.GetConnectionString(), ["Engineering:AgentKey"]=AgentKey });
        builder.AddApiDefaults(); builder.AddEngineering(); builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, FixtureAuth>("Fixture",_=>{});
        App=builder.Build();App.UseApiDefaults(deferRateLimiter:true);App.UseAuthentication();App.UseAuthorization();App.UseRateLimiter();await App.MapEngineeringAsync();await App.StartAsync();Client=App.GetTestClient();
    }
    public const string AgentKey="test-assembly-only-engineering-agent-credential";
    public string Connection => postgres.GetConnectionString();
    public async Task DisposeAsync() { Client.Dispose();await App.DisposeAsync();await postgres.DisposeAsync(); }
    private sealed class FixtureAuth(IOptionsMonitor<AuthenticationSchemeOptions> o, ILoggerFactory l, UrlEncoder e) : AuthenticationHandler<AuthenticationSchemeOptions>(o,l,e)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role=Request.Headers["X-Fixture-Role"].ToString();if(role.Length==0)return Task.FromResult(AuthenticateResult.NoResult());
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity([new("sub","synthetic-engineering-test"),new(ClaimTypes.Role,role)],Scheme.Name)),Scheme.Name)));
        }
    }
}

public sealed class EngineeringTests(EngineeringFixture fixture) : IClassFixture<EngineeringFixture>
{
    private static ExperimentRequest Request => new("load","overview","constant",5,15,Confirmation:"SYNTHETIC LAB ONLY");
    [Theory]
    [InlineData(null,401)] [InlineData("Admin",403)] [InlineData("HrManager",403)] [InlineData("HrSpecialist",403)] [InlineData("EngineeringReader",200)] [InlineData("EngineeringOperator",200)]
    public async Task EngineeringRequiresAnExplicitTechnicalRole(string? role,int expected)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,"/api/engineering/capabilities");if(role is not null)request.Headers.Add("X-Fixture-Role",role);
        using var response=await fixture.Client.SendAsync(request);Assert.Equal(expected,(int)response.StatusCode);
    }
    [Fact]
    public async Task ReaderCannotExecuteAndHumanCannotImpersonateAgent()
    {
        using var read=new HttpRequestMessage(HttpMethod.Post,"/api/engineering/runs") {Content=JsonContent.Create(Request)};read.Headers.Add("X-Fixture-Role","EngineeringReader");
        Assert.Equal(HttpStatusCode.Forbidden,(await fixture.Client.SendAsync(read)).StatusCode);
        using var human=new HttpRequestMessage(HttpMethod.Post,"/api/engineering-agent/claim");human.Headers.Add("X-Fixture-Role","EngineeringOperator");
        Assert.Equal(HttpStatusCode.Unauthorized,(await fixture.Client.SendAsync(human)).StatusCode);
    }
    [Fact]
    public async Task PostgreSqlSerializesIdempotencyAndFencesExpiredLeases()
    {
        var key=Guid.NewGuid();var actor=Guid.NewGuid().ToString();
        var ids=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>fixture.Store.CreateAsync(actor,key,Request,default)));
        Assert.Single(ids.Distinct());Assert.Single(await fixture.Store.ListAsync(default),x=>x.Id==ids[0]);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>fixture.Store.CreateAsync(actor,key,Request with{Vus=6},default));
        var first=await fixture.Store.ClaimAsync(default);Assert.NotNull(first);
        Assert.Null(await fixture.Store.ClaimAsync(default));
        await using(var connection=new NpgsqlConnection(fixture.Connection)) { await connection.OpenAsync();await using var cmd=new NpgsqlCommand("UPDATE engineering_runs SET lease_until=now()-interval '1 minute' WHERE id=@id",connection);cmd.Parameters.AddWithValue("id",first.Run.Id);await cmd.ExecuteNonQueryAsync(); }
        var replacement=await fixture.Store.ClaimAsync(default);Assert.NotNull(replacement);Assert.True(replacement.RecoveryRequired);Assert.NotEqual(first.Lease,replacement.Lease);
        var report=new RunUpdate("Succeeded",100,"Completed",[],[],new(){["cleanupVerified"]=true});
        Assert.Null(await fixture.Store.ReportAsync(first.Run.Id,first.Lease,report,default));
        Assert.NotNull(await fixture.Store.ReportAsync(replacement.Run.Id,replacement.Lease,report,default));
        Assert.Equal("Succeeded",(await fixture.Store.GetAsync(replacement.Run.Id,default))!.Status);
    }
    [Fact]
    public async Task CancellationPersistsAndIsVisibleToTheActiveLease()
    {
        var id=await fixture.Store.CreateAsync(Guid.NewGuid().ToString(),Guid.NewGuid(),Request,default);
        var claim=await fixture.Store.ClaimAsync(default);Assert.NotNull(claim);Assert.Equal(id,claim.Run.Id);
        Assert.True(await fixture.Store.CancelAsync(id,"synthetic-operator",default));
        Assert.True(await fixture.Store.ReportAsync(id,claim.Lease,new("Running",10,"Running",[],[],[]),default));
        Assert.True(await fixture.Store.ReportAsync(id,claim.Lease,new("Cancelled",100,"Cancelled",[],[],[]),default));
        Assert.False(await fixture.Store.CancelAsync(id,"synthetic-operator",default));
    }
    [Theory]
    [InlineData("http://production.example/api",5,15)] [InlineData("overview",101,15)] [InlineData("overview",5,121)]
    public void TargetsAndResourceBudgetsCannotBeBypassed(string target,int vus,int seconds) => Assert.NotNull((Request with{Target=target,Vus=vus,DurationSeconds=seconds}).Validate(100));
    [Fact]
    public void InvalidThresholdsUnorderedCapacityAndMissingApprovalAreRejected()
    {
        Assert.NotNull((Request with{MaxP95Ms=double.NaN}).Validate(100));Assert.NotNull((Request with{Kind="capacity",Levels=[10,1]}).Validate(100));
        Assert.NotNull((Request with{Confirmation=""}).Validate(100));Assert.NotNull((Request with{Kind="chaos",Scenario="constant"}).Validate(100));
        Assert.False(new RunUpdate("Succeeded",100,"secret payload",[],[],[]).IsValid());
        Assert.False(new RunUpdate("Succeeded",100,"Completed",null!,[],[]).IsValid());
    }
    [Fact]
    public void MalformedAgentReportsAreRejectedWithoutDereferencingNullElements()
    {
        var report = new RunUpdate("Succeeded",100,"Completed",[],[],[]);
        Assert.False((report with { Samples = [null!] }).IsValid());
        Assert.False((report with { Steps = [null!] }).IsValid());
        Assert.False((report with { Benchmarks = [null!] }).IsValid());
        Assert.False(new InventoryUpdate([null!]).IsValid());
        var benchmark = new BenchmarkResult("qwen3:4b","bilingual-v1","Succeeded",null,10,1,1,4,20,double.NaN,100,null);
        Assert.False((report with { Benchmarks = [benchmark] }).IsValid());
        Assert.False((report with { Benchmarks = [benchmark with { PeakCpuPercent = 1, PeakMemoryBytes = -1 }] }).IsValid());
    }
}
