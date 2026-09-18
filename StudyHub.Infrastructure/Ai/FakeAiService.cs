using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Settings;
using StudyHub.Application.Notes.Commands.ExtractTaskSuggestions;
using StudyHub.Application.Notes.Commands.SummarizeNote;

namespace StudyHub.Infrastructure.Ai;

/// <summary>
/// The AI provider used while no API key is configured (ADR-35). It answers from the note
/// itself, deterministically, so the endpoints, the quota arithmetic and the failure paths
/// can be exercised with no key, no network and no cost. It is chosen once at startup and
/// is never a fallback after a failed real call — a silent downgrade would make a broken
/// provider look like a working one.
/// </summary>
public sealed class FakeAiService : IAiService
{
    private const int MaxTitleLength = 250;
    private const int MaxSentences = 3;
    private const int MaxFakeSuggestions = 3;

    private static readonly char[] SentenceEnd = ['.', '!', '?', '\n'];

    private readonly AiSettings _settings;
    private readonly bool _failEveryCall;

    public FakeAiService(AiSettings settings, bool failEveryCall)
    {
        _settings = settings;
        _failEveryCall = failEveryCall;
    }

    public Task<AiSummaryResult> SummarizeAsync(
        string title, string? content, CancellationToken cancellationToken)
    {
        FailIfConfiguredTo();

        var source = string.IsNullOrWhiteSpace(content) ? title : content;
        var summary = string.Join(" ", FirstSentences(source, MaxSentences));

        return Task.FromResult(new AiSummaryResult(summary, _settings.EstimateTokens(title, content)));
    }

    public Task<AiTaskSuggestionsResult> ExtractTasksAsync(
        string title, string? content, CancellationToken cancellationToken)
    {
        FailIfConfiguredTo();

        var lines = (content ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(MaxFakeSuggestions)
            .ToList();

        // A note with no body still has to produce something, or the empty case would look
        // like a provider failure rather than like a note with nothing to do in it.
        if (lines.Count == 0)
            lines.Add(title);

        var suggestions = lines
            .Select(line => new TaskSuggestion(
                Clamp($"Follow up: {FirstSentences(line, 1).FirstOrDefault() ?? line}", MaxTitleLength),
                $"Suggested from the note \"{title}\"."))
            .Take(_settings.MaxSuggestions)
            .ToList();

        return Task.FromResult(new AiTaskSuggestionsResult(
            suggestions, _settings.EstimateTokens(title, content)));
    }

    /// <summary>
    /// Ai:FakeFailure turns every call into the failure of §15.3 that produced no response,
    /// which is what makes the 502 path provable without a provider. Nothing is billed,
    /// so the handler must record nothing.
    /// </summary>
    private void FailIfConfiguredTo()
    {
        if (_failEveryCall)
            throw new ExternalServiceException("The AI provider is unavailable.");
    }

    private static IEnumerable<string> FirstSentences(string text, int count)
        => text.Split(SentenceEnd, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(count)
            .Select(sentence => sentence + ".");

    private static string Clamp(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength].TrimEnd();
}
