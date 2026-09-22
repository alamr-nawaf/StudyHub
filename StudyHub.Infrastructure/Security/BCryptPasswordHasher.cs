using BC = BCrypt.Net.BCrypt;
using SaltParseException = BCrypt.Net.SaltParseException;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Infrastructure.Security;
/// <summary>
/// Hashes and verifies passwords with BCrypt. A malformed stored hash is a rejected
/// credential, never a server error: one corrupt row must not turn a login into a 500.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{


    private const int WorkFactor = 12;

    // The Enhanced variant pre-hashes the input, which removes BCrypt's 72-byte truncation
    public string Hash(string password) => BC.EnhancedHashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        // The hash comes from the database, not from the user: any invalid shape in it is a
        // rejected credential rather than a server fault
        if (string.IsNullOrWhiteSpace(hash))
            return false;

        try
        {
            return BC.EnhancedVerify(password, hash);
        }
        catch (Exception ex) when (ex is SaltParseException or ArgumentException)
        {
            // ArgumentOutOfRangeException derives from ArgumentException: a truncated hash
            // passes the version check and then breaks a Substring inside the library
            return false;
        }
    }
    // static: computed once per process rather than once per request — a single BCrypt
    // operation costs about a quarter of a second
    private static readonly string Dummy = BC.EnhancedHashPassword("no-such-user", WorkFactor);

    public string DummyHash => Dummy;
}
