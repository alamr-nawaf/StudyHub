using MediatR;

namespace StudyHub.Application.Auth.Commands.PurgeExpiredRefreshTokens;

/// <summary>
/// Deletes refresh tokens that have been expired longer than the retention window
/// (ADR-46). It carries nothing: the background job is only a trigger, and the rule
/// lives in the handler, where it is tested. Returns the number of rows deleted.
/// </summary>
public record PurgeExpiredRefreshTokensCommand : IRequest<int>;
