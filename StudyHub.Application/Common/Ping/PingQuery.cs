using FluentValidation;
using MediatR;

namespace StudyHub.Application.Common.Ping;

public record PingQuery(string Name) : IRequest<string>;

public class PingQueryValidator : AbstractValidator<PingQuery>
{
    public PingQueryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
    }
}

public class PingQueryHandler : IRequestHandler<PingQuery, string>
{
    public Task<string> Handle(PingQuery request, CancellationToken cancellationToken)
        => Task.FromResult($"Pong, {request.Name}!");
}