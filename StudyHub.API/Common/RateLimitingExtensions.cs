using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace StudyHub.API.Common;

/// <summary>
/// Wires the two rate-limiting policies of ADR-45 and the rejection they share. The
/// framework's own middleware is used: no package, and nothing to keep in step with it.
/// </summary>
public static class RateLimitingExtensions
{
    public static IServiceCollection AddStudyHubRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var settings = ReadSettings(configuration);
        services.AddSingleton(settings);

        services.AddRateLimiter(options =>
        {
            // The default rejection is 503, which tells the client the server is broken
            // rather than that it asked too often
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Partitioned by caller address: the anonymous credential endpoints have no
            // user yet, and the address is the only key they have (§9.4)
            options.AddPolicy(RateLimitingSettings.AuthPolicy, context =>
                Window(
                    // The in-process test server gives no remote address at all, so the
                    // fallback is load-bearing, not decoration
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    settings.Auth));

            // Partitioned by user: one account must not be able to spend the provider
            // budget everyone shares. "sub" literally, the claim CurrentUserService reads,
            // because MapInboundClaims is off
            options.AddPolicy(RateLimitingSettings.AiPolicy, context =>
                Window(
                    context.User.FindFirst("sub")?.Value ?? "anonymous",
                    settings.Ai));

            options.OnRejected = async (context, cancellationToken) =>
            {
                // Retry-After comes from the lease, so the client is told the truth rather
                // than the window length, which has usually already partly elapsed
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests.",
                    // Deliberately different from the monthly quota's 429, so a client can
                    // tell "slow down" from "you have spent your month" (§8)
                    Detail = "Too many requests. Please retry later.",
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.29"
                };

                var problemDetailsService = context.HttpContext.RequestServices
                    .GetRequiredService<IProblemDetailsService>();

                await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = problemDetails
                });
            };
        });

        return services;
    }

    private static RateLimitPartition<string> Window(string partitionKey, RateLimitWindowSettings window) =>
        RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = window.PermitLimit,
            Window = TimeSpan.FromSeconds(window.WindowSeconds),
            // Reject at once: a queued request holds a connection while the caller waits
            // for a budget it was told it does not have
            QueueLimit = 0
        });

    // Read once at startup like every other setting: a limit of zero would lock every
    // caller out of login, and a missing section would silently mean no limit at all
    private static RateLimitingSettings ReadSettings(IConfiguration configuration)
    {
        var section = configuration.GetSection(RateLimitingSettings.SectionName);

        return new RateLimitingSettings(
            ReadWindow(section.GetSection("Auth")),
            ReadWindow(section.GetSection("Ai")));
    }

    private static RateLimitWindowSettings ReadWindow(IConfigurationSection section)
    {
        var permitLimit = section.GetValue<int?>("PermitLimit");
        var windowSeconds = section.GetValue<int?>("WindowSeconds");

        if (permitLimit is null or < 1)
            throw new InvalidOperationException(
                $"RateLimiting:{section.Key}:PermitLimit must be configured with a number of at least 1.");

        if (windowSeconds is null or < 1)
            throw new InvalidOperationException(
                $"RateLimiting:{section.Key}:WindowSeconds must be configured with a number of at least 1.");

        return new RateLimitWindowSettings(permitLimit.Value, windowSeconds.Value);
    }
}
