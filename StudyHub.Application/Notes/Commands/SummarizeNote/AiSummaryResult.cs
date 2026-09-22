namespace StudyHub.Application.Notes.Commands.SummarizeNote;

/// <summary>
/// The summary an AI provider produced for one note, and what it cost in tokens.
/// Nothing is stored: keeping the text is the client's choice (Requirements §15).
/// </summary>
public record AiSummaryResult(string Summary, int TokensUsed);
