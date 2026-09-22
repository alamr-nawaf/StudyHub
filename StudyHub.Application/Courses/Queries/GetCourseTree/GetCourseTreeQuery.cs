using MediatR;
using StudyHub.Application.Items;

namespace StudyHub.Application.Courses.Queries.GetCourseTree;

/// <summary>
/// Requests every note and task of a course, at every depth, as a flat list (ADR-23).
/// </summary>
public record GetCourseTreeQuery(Guid Id) : IRequest<IReadOnlyList<ItemDto>>;
