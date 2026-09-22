namespace StudyHub.Application.Users.Commands.SeedAdministrator;

/// <summary>
/// What the seeding did, for the startup log alone: Created warns about a password left in
/// configuration, and IsActive about an administrator who cannot log in.
/// </summary>
public record SeedAdministratorResult(Guid UserId, bool Created, bool IsActive);
