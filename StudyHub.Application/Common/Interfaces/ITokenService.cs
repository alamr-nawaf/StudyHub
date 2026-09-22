using StudyHub.Domain.Enums;

namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// Application asks for tokens and hashes; Infrastructure knows how they are made. The role
/// is passed in because the role claim is part of the token (Requirements §9.1, ADR-21).
/// </summary>
public interface ITokenService
{
    AccessToken GenerateAccessToken(Guid userId, UserRole role, DateTime utcNow);

    // The raw token, its hash and its expiry from one call; only the raw one is handed to
    // the client, and it is never stored
    RefreshTokenResult GenerateRefreshToken(DateTime utcNow);

    // SHA-256 in hex: 64 characters, which is exactly the column length declared in
    // RefreshTokenConfiguration
    string HashRefreshToken(string rawToken);
}