using FluentValidation;
using MediatR;

namespace StudyHub.Application.Common.Behaviors;

/// <summary>
/// Runs every validator registered for a request before its handler, once, for every
/// request that passes through MediatR.
/// <para>
/// The constraint is <c>notnull</c> and not <c>IRequest&lt;TResponse&gt;</c>: since MediatR 12
/// <c>IRequest</c> no longer derives from <c>IRequest&lt;Unit&gt;</c>, and the older constraint
/// made the container skip this behaviour silently for every command without a result.
/// </para>
/// </summary>
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

        // Every validator runs in parallel, and each one is handed the cancellation token
        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results.SelectMany(r => r.Errors).ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next(cancellationToken);
    }
}