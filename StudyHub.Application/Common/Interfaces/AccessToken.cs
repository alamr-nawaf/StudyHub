namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// An access token together with the instant it expires; expiresIn in the login and refresh
/// responses is computed from it (Requirements §8).
/// </summary>
public sealed record AccessToken(string Value, DateTime ExpiresAt);