namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// The raw refresh token, its hash and its expiry from one call, so a hash that does not
/// belong to the returned raw token cannot be stored by accident.
/// </summary>
public sealed record RefreshTokenResult(string RawToken, string TokenHash, DateTime ExpiresAt);