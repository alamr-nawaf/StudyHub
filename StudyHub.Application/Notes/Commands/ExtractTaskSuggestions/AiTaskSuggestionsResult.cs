namespace StudyHub.Application.Notes.Commands.ExtractTaskSuggestions;

/// <summary>
/// The tasks an AI provider suggested from one note, and what the call cost in tokens.
/// Nothing is created: a suggestion becomes a task only through POST /api/tasks (ADR-28).
/// </summary>
public record AiTaskSuggestionsResult(IReadOnlyList<TaskSuggestion> Suggestions, int TokensUsed);
