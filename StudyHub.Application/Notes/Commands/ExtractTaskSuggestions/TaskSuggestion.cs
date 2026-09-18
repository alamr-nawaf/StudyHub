namespace StudyHub.Application.Notes.Commands.ExtractTaskSuggestions;

/// <summary>
/// One task an AI provider suggested from a note. It carries no due date: "by Friday"
/// cannot become a UTC moment without the user's timezone, which the API never asks
/// for (Requirements §14.1). The user sets the date while approving the suggestion.
/// </summary>
public record TaskSuggestion(string Title, string? Content);
