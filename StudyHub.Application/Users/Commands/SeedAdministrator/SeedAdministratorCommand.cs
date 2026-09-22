using MediatR;

namespace StudyHub.Application.Users.Commands.SeedAdministrator;

/// <summary>
/// Sent once at startup from the AdminSeed configuration, never from an endpoint. FullName
/// and Password are needed only when no account with that e-mail exists yet.
/// </summary>
public record SeedAdministratorCommand(
    string Email,
    string? FullName,
    string? Password) : IRequest<SeedAdministratorResult>;
