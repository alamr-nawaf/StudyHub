using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using StudyHub.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace StudyHub.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in-process against the throwaway container (ADR-44). Everything the
/// application would otherwise take from user secrets is supplied here, so the suite can
/// never reach a real provider or the developer's database.
/// </summary>
public sealed class StudyHubApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _jwtKey;
    private readonly int _rateLimit;
    private readonly SqlStatementCounter _statementCounter;

    public StudyHubApiFactory(
        string connectionString, string jwtKey, SqlStatementCounter statementCounter, int rateLimit)
    {
        _connectionString = connectionString;
        _jwtKey = jwtKey;
        _statementCounter = statementCounter;
        _rateLimit = rateLimit;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Testing, never Development: Development loads the owner's user secrets, which
        // hold the real AI key, and the suite would then call — and pay — the real provider
        builder.UseEnvironment("Testing");

        // UseSetting, not ConfigureAppConfiguration: with the minimal hosting model
        // Program.cs reads builder.Configuration while the host is being created, before a
        // ConfigureAppConfiguration callback would run, so the values have to be there from
        // the start or AddInfrastructureServices sees none of them
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _connectionString,

            // One key per run, shared by both hosts: a token minted on the ordinary host
            // must still be accepted by the low-limit one, which is how the AI-limit test
            // spends its auth requests where they are not counted
            ["Jwt:Key"] = _jwtKey,

            // Empty on purpose: the fake provider answers, deterministically and free
            ["Ai:ApiKey"] = string.Empty,
            ["Ai:Model"] = string.Empty,
            ["Ai:FakeFailure"] = "false",

            // No administrator is seeded into a test database
            ["AdminSeed:Email"] = string.Empty,
            ["AdminSeed:Password"] = string.Empty,
            ["AdminSeed:FullName"] = string.Empty,

            ["BusinessTime:TimeZoneId"] = "Asia/Riyadh",

            // The cleanup job would otherwise delete a test's seeded tokens mid-test
            ["RefreshTokenCleanup:Enabled"] = "false",

            ["RateLimiting:Auth:PermitLimit"] = _rateLimit.ToString(),
            ["RateLimiting:Auth:WindowSeconds"] = "60",
            ["RateLimiting:Ai:PermitLimit"] = _rateLimit.ToString(),
            ["RateLimiting:Ai:WindowSeconds"] = "60"
        };

        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            // Added to the DbContext's own options, not registered as IInterceptor in DI:
            // DI discovery covers singleton interceptors only, and a command interceptor is
            // not one — registered that way it is never called, and the counter reads zero
            services.ConfigureDbContext<StudyHubDbContext>(options =>
                options.AddInterceptors(_statementCounter));
        });
    }
}
