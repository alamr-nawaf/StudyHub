using StudyHub.Application.Notes.Commands.ExtractTaskSuggestions;
using StudyHub.Application.Notes.Commands.SummarizeNote;

namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// The AI operations this application needs, stated as use cases rather than as
/// "a completion": the provider, its URL, its prompts, its JSON shape and its token
/// count are Infrastructure knowledge (ADR-35). A new operation is a new method here.
/// </summary>
public interface IAiService
{
    Task<AiSummaryResult> SummarizeAsync(string title, string? content, CancellationToken cancellationToken);

    Task<AiTaskSuggestionsResult> ExtractTasksAsync(string title, string? content, CancellationToken cancellationToken);
}
