namespace StudyHub.Application.Common.Exceptions;

/// <summary>
/// Thrown when a well-formed request conflicts with the state of the data: a duplicate
/// e-mail, a parent at maximum depth, or a lost concurrency race. Translated to 409.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}