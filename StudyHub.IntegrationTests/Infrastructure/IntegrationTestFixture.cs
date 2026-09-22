using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyHub.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace StudyHub.IntegrationTests.Infrastructure;

/// <summary>
/// One throwaway PostgreSQL container for the whole run, migrated once from an empty
/// schema — which also proves the migration history applies from zero (ADR-44). It owns
/// the two API hosts: the ordinary one, and a second whose rate limits are low enough to
/// be reached in a test.
/// </summary>
public sealed class IntegrationTestFixture : IAsyncLifetime
{
    /// <summary>The permit limit of the low-limit host: the fourth request is refused.</summary>
    public const int LowRateLimit = 3;

    private const int NormalRateLimit = 1000;

    // The image is given to the builder rather than through WithImage: the parameterless
    // constructor is obsolete in Testcontainers 4.15
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:15-alpine")
        .WithDatabase("studyhub_tests")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready"))
        .Build();

    public SqlStatementCounter StatementCounter { get; } = new();

    public StudyHubApiFactory Factory { get; private set; } = null!;

    public StudyHubApiFactory LowRateLimitFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "The integration tests need Docker to be running: they start a throwaway " +
                "PostgreSQL container (ADR-44).", exception);
        }

        var connectionString = _container.GetConnectionString();

        // Generated once per run: nothing signed here outlives the test process, no key is
        // read from or written to a file, and both hosts accept each other's tokens
        var jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

        Factory = new StudyHubApiFactory(connectionString, jwtKey, StatementCounter, NormalRateLimit);
        LowRateLimitFactory = new StudyHubApiFactory(connectionString, jwtKey, StatementCounter, LowRateLimit);

        // Migrated through the application's own DbContext, so the schema under test is the
        // one the migrations produce, not one the tests built
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StudyHubDbContext>();
        await context.Database.MigrateAsync();

        // Migrating executes statements of its own; no test should see them
        StatementCounter.Reset();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await LowRateLimitFactory.DisposeAsync();
        await _container.DisposeAsync();
    }
}

/// <summary>
/// One collection for every integration test: the statement counter is shared state, and
/// two requests running at once would mix their counts.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "StudyHub integration tests";
}
