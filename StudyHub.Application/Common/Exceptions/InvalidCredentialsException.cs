namespace StudyHub.Application.Common.Exceptions;

/// <summary>
/// Thrown by the login and refresh handlers alone, with one message for every cause: an
/// unknown e-mail, a wrong password, a deactivated account, an expired token (§9.2, §9.3).
/// Translated to 401.
/// </summary>
public class InvalidCredentialsException : Exception
{
    private const string SingleMessage = "Invalid credentials.";

    // No constructor takes a message: any distinguishable text would reopen the account
    // enumeration hole the single message exists to close
    public InvalidCredentialsException() : base(SingleMessage) { }
}