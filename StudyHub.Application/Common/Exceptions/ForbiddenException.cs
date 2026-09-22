namespace StudyHub.Application.Common.Exceptions;

/// <summary>
/// Thrown when the entity exists but does not belong to the caller. The difference from
/// <see cref="NotFoundException"/> is deliberate: existence is not hidden, only access is
/// refused (ADR-33). Translated to 403.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}