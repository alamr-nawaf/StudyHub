namespace StudyHub.Application.Common.Exceptions;

/// <summary>
/// Thrown when a service outside this process failed or answered with something
/// unusable. Translated to 502, so nothing from outside reaches the client as a 500
/// (Requirements §15.3). The message is deliberately generic: it is written into the
/// response body, so it must never carry provider detail.
/// </summary>
public class ExternalServiceException : Exception
{
    /// <summary>
    /// Tokens the provider has already charged for this call — zero when the call
    /// produced no response at all. A billed call is recorded even though its result
    /// is unusable (Requirements §15.3).
    /// </summary>
    public int TokensBilled { get; }

    public ExternalServiceException(string message, int tokensBilled = 0) : base(message)
        => TokensBilled = tokensBilled;
}
