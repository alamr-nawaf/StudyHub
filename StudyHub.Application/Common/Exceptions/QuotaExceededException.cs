namespace StudyHub.Application.Common.Exceptions;

/// <summary>
/// Thrown before an AI call when the estimated cost does not fit in what is left of the
/// user's monthly quota (ADR-24). Translated to 429 in GlobalExceptionHandler.
/// </summary>
public class QuotaExceededException : Exception
{
    public QuotaExceededException(string message) : base(message) { }
}
