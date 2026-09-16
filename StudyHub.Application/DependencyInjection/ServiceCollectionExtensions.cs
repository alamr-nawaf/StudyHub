using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using StudyHub.Application.Common.Behaviors;

namespace StudyHub.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // رسائل FluentValidation الافتراضية تُترجَم حسب ثقافة الجهاز، فيتغيّر جسم الرد
        // بتغيّر الخادم. العقد في §8 لا يعرف لغة — نثبّته على الإنجليزية
        ValidatorOptions.Global.LanguageManager.Enabled = false;
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly));

        services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}