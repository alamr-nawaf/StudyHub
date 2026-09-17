namespace StudyHub.Application.Common.Pagination;

/// <summary>
/// The pagination limits shared by every paginated endpoint (Requirements §14.2, ADR-22).
/// </summary>
public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
