using StudyHub.API.Authorization;
using StudyHub.API.BackgroundJobs;
using StudyHub.API.Common;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.DependencyInjection;
using StudyHub.Infrastructure.DependencyInjection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using StudyHub.Infrastructure.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// The identity source, which needs access to the current request
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// The same settings that issued the token. The length check stays in
// AddInfrastructureServices, so a short key stops startup even if it reaches this far
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // The names arrive as they were issued, sub and role, with no mapping to long URIs
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),

            // Explicit rather than default: the default is five minutes, which keeps an
            // expired token acceptable for that long (§9.1)
            ClockSkew = TimeSpan.FromSeconds(30),

            RoleClaimType = "role",
            NameClaimType = "sub"
        };
    });

// Protected by default: every endpoint needs a token unless it declares [AllowAnonymous].
// Without this, an anonymous request reaches the handler and comes back as 403 instead of
// 401, and a 404 then reveals which ids exist
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPermissionPolicies();
});
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Deletes refresh tokens that have been expired longer than the retention window (ADR-46)
builder.Services.AddHostedService<RefreshTokenCleanupService>();

builder.Services.AddStudyHubRateLimiting(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

await app.SeedAdministratorAsync();

// Which provider answers the AI endpoints is a configuration decision taken at startup,
// so it is stated at startup — by class name, never by key (§14.3)
using (var aiScope = app.Services.CreateScope())
{
    var aiService = aiScope.ServiceProvider.GetRequiredService<IAiService>();
    app.Logger.LogInformation("AI provider in use: {Provider}.", aiService.GetType().Name);
}

app.UseExceptionHandler();

// The demo page in wwwroot (ADR-48). Before authentication on purpose: the page itself is
// public, and every API call it makes carries its own token. Placed after UseAuthorization,
// the fallback policy would demand a token for the page too.
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// After authentication on purpose: before it there is no "sub" claim, so every AI caller
// would share one anonymous partition and one user could spend everybody's budget (ADR-45)
app.UseRateLimiter();

app.MapControllers();

app.Run();

/// <summary>
/// The entry point, made public so that the integration tests can host this API in-process:
/// WebApplicationFactory&lt;Program&gt; needs a public Program, and top-level statements generate
/// an internal one that the test project cannot see (ADR-44).
/// </summary>
public partial class Program;