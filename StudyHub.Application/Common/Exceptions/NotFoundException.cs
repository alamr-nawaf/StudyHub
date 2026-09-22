namespace StudyHub.Application.Common.Exceptions;

/// <summary>
/// Thrown when the caller asks for an entity that is not there: never created, or
/// soft-deleted and therefore filtered out by the query filter. Translated to 404.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}