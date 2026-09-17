using FluentValidation;
using MediatR;

namespace StudyHub.Application.Common.Behaviors;

// notnull لا IRequest<TResponse>: في MediatR 12+ لا يرث IRequest النوعَ IRequest<Unit>،
// فالقيد القديم جعل الحاوية تتخطّى هذا السلوك بصمت لكل أمر بلا نتيجة
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
    TRequest request,
    RequestHandlerDelegate<TResponse> next,
    CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);

        // تشغيل كل المدققات على التوازي، مع تمرير رمز الإلغاء
        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results.SelectMany(r => r.Errors).ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next(cancellationToken);
    }
}