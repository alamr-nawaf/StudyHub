using MediatR;
using StudyHub.Application.Users.Commands.SeedAdministrator;

namespace StudyHub.API.Common;

public static class AdministratorSeeding
{
    private const string SectionName = "AdminSeed";

    // القسم في user-secrets لا appsettings: فيه كلمة مرور حقيقية (CODING_STANDARDS §11).
    // بلا AdminSeed:Email لا يحدث شيء، فالتشغيل المحلي العادي لا يحتاج قاعدة بيانات عند الإقلاع
    public static async Task SeedAdministratorAsync(this WebApplication app)
    {
        var section = app.Configuration.GetSection(SectionName);
        var email = section["Email"];

        if (string.IsNullOrWhiteSpace(email))
            return;

        var password = section["Password"];

        using var scope = app.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // يمرّ بالمدقّق: إعدادات مشوّهة توقف الإقلاع بـ ValidationException بدل مسؤول بكلمة مرور ضعيفة
        var result = await mediator.Send(new SeedAdministratorCommand(email, section["FullName"], password));

        // المعرّف لا الإيميل في السجل (§14.3)
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
