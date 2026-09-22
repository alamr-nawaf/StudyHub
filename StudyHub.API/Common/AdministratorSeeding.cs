using MediatR;
using StudyHub.Application.Users.Commands.SeedAdministrator;

namespace StudyHub.API.Common;

/// <summary>
/// Creates or promotes the first administrator at startup, from the AdminSeed configuration.
/// </summary>
public static class AdministratorSeeding
{
    private const string SectionName = "AdminSeed";

    // The section lives in user secrets, not in appsettings: it holds a real password
    // (CODING_STANDARDS §11). Without AdminSeed:Email nothing happens at all, so an ordinary
    // local run does not need a database at startup
    public static async Task SeedAdministratorAsync(this WebApplication app)
    {
        var section = app.Configuration.GetSection(SectionName);
        var email = section["Email"];

        if (string.IsNullOrWhiteSpace(email))
            return;

        var password = section["Password"];

        using var scope = app.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // It goes through the validator: malformed configuration stops startup with a
        // ValidationException instead of creating an administrator with a weak password
        var result = await mediator.Send(new SeedAdministratorCommand(email, section["FullName"], password));

        // The id, never the e-mail, in the log (§14.3)
        if (result.Created)
            app.Logger.LogInformation("Administrator {UserId} created from configuration.", result.UserId);
        else
            app.Logger.LogInformation("Administrator {UserId} ensured on an existing account.", result.UserId);

        if (password is not null)
            app.Logger.LogWarning(result.Created
                ? "Administrator created. Remove AdminSeed:Password from user-secrets; it is no longer needed."
                : "AdminSeed:Password was ignored because the account already exists. Remove it from user-secrets.");

        if (!result.IsActive)
            app.Logger.LogWarning("Administrator {UserId} is deactivated and cannot log in.", result.UserId);
    }
}
