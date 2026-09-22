using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using StudyHub.Application.Common.Behaviors;

namespace StudyHub.Application.DependencyInjection;

/// <summary>
/// Registers everything the Application layer owns: MediatR, the validators, and the
/// validation behaviour that runs them.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // FluentValidation's default messages are translated according to the machine's
        // culture, so the response body would change with the server it runs on. The contract
        // in §8 has no language: it is pinned to English here
        ValidatorOptions.Global.LanguageManager.Enabled = false;
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly));

        services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}