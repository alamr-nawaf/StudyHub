namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// Who the caller is. Application asks; the API answers, from the validated token — which is
/// how Application stays free of any knowledge of HTTP.
/// </summary>
public interface ICurrentUserService
{
    Guid UserId { get; }
}