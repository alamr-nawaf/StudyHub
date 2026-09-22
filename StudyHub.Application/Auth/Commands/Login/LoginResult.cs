namespace StudyHub.Application.Auth.Commands.Login;

/// <summary>
/// The shape both login and refresh return (§8). ExpiresIn is in seconds, so a client never
/// has to parse the JWT to know when to refresh.
/// </summary>
public record LoginResult(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn);