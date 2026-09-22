namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// Hashing and verification of passwords. The algorithm and its work factor are
/// Infrastructure's business; Application only asks.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);

    // A structurally valid hash with the same work factor, to compare against when no user
    // was found, so that an unknown e-mail costs the same time as a wrong password
    // (§9.2, rule 3)
    string DummyHash { get; }
}