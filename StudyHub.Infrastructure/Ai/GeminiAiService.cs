using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Settings;
using StudyHub.Application.Notes.Commands.ExtractTaskSuggestions;
using StudyHub.Application.Notes.Commands.SummarizeNote;

namespace StudyHub.Infrastructure.Ai;

/// <summary>
/// Calls Google Gemini over its REST API. Everything the provider knows about — the
/// route, the prompts, the request and response JSON and the usage field — lives here
/// and nowhere else, so a second provider is a second class and one DI line (ADR-35).
/// </summary>
public sealed class GeminiAiService : IAiService
{
    // POST /api/tasks rejects a longer title, so a suggestion carrying one could never
    // be approved; the summary cap keeps an unbounded response out of the response body.
    private const int MaxTitleLength = 250;
    private const int MaxSummaryLength = 2000;

    // Enough of a refusal to name its cause, without pouring a whole response into the log.
    private const int MaxLoggedBodyLength = 500;

    private const string SummaryPrompt =
        "Summarize the study note below in at most three sentences, written in the same " +
        "language as the note. Reply with JSON only, no markdown and no explanation, in " +
        "exactly this shape:\n" +
        "{\"summary\":\"<the summary>\"}";

    private const string ExtractPrompt =
        "Read the study note below and propose the concrete tasks its author still has to " +
        "do. Reply with JSON only, no markdown and no explanation, in exactly this shape:\n" +
        "{\"tasks\":[{\"title\":\"<short title>\",\"content\":\"<one line of detail, or null>\"}]}\n" +
        "Rules: at most {0} tasks, each title at most 250 characters and written in the " +
        "same language as the note; never invent a date, a deadline or a time.";

    private readonly HttpClient _httpClient;
    private readonly AiSettings _settings;
    private readonly ILogger<GeminiAiService> _logger;

    public GeminiAiService(HttpClient httpClient, AiSettings settings, ILogger<GeminiAiService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<AiSummaryResult> SummarizeAsync(
        string title, string? content, CancellationToken cancellationToken)
    {
        var (payload, tokensUsed) = await CallAsync(SummaryPrompt, title, content, cancellationToken);

        var summary = ReadString(payload, "summary", tokensUsed);

        return new AiSummaryResult(Clamp(summary, MaxSummaryLength), tokensUsed);
    }

    public async Task<AiTaskSuggestionsResult> ExtractTasksAsync(
        string title, string? content, CancellationToken cancellationToken)
    {
        var prompt = ExtractPrompt.Replace("{0}", _settings.MaxSuggestions.ToString());

        var (payload, tokensUsed) = await CallAsync(prompt, title, content, cancellationToken);

        if (!payload.TryGetProperty("tasks", out var tasks) || tasks.ValueKind != JsonValueKind.Array)
            throw Unreadable(tokensUsed);

        var suggestions = new List<TaskSuggestion>();

        foreach (var task in tasks.EnumerateArray())
        {
            // The prompt asks for a limit; the provider is under no obligation to honour it.
            if (suggestions.Count == _settings.MaxSuggestions)
                break;

            var suggestedTitle = task.ValueKind == JsonValueKind.Object
                && task.TryGetProperty("title", out var titleProperty)
                && titleProperty.ValueKind == JsonValueKind.String
                    ? titleProperty.GetString()!.Trim()
                    : string.Empty;

            // A suggestion with no title cannot be approved, so it is dropped rather than
            // returned: the user would have nothing to click.
            if (suggestedTitle.Length == 0)
                continue;

            var suggestedContent = task.TryGetProperty("content", out var contentProperty)
                && contentProperty.ValueKind == JsonValueKind.String
                    ? contentProperty.GetString()!.Trim()
                    : null;

            suggestions.Add(new TaskSuggestion(
                Clamp(suggestedTitle, MaxTitleLength),
                string.IsNullOrEmpty(suggestedContent) ? null : suggestedContent));
        }

        return new AiTaskSuggestionsResult(suggestions, tokensUsed);
    }

    /// <summary>
    /// Sends one prompt and returns the JSON the model answered with, plus what the call
    /// cost. Every failure leaves through <see cref="ExternalServiceException"/>: the
    /// provider's own exception types never cross into Application (§15.3).
    /// </summary>
    private async Task<(JsonElement Payload, int TokensUsed)> CallAsync(
        string instruction, string title, string? content, CancellationToken cancellationToken)
    {
        // Built as a dictionary rather than an anonymous type because thinkingConfig is
        // present only when it was configured, and a property cannot be left out of one.
        var generationConfig = new Dictionary<string, object>
        {
            ["maxOutputTokens"] = _settings.MaxOutputTokens,
            ["responseMimeType"] = "application/json"
        };

        var thinkingConfig = new Dictionary<string, object>();

        if (_settings.ThinkingBudget is { } thinkingBudget)
            thinkingConfig["thinkingBudget"] = thinkingBudget;

        if (!string.IsNullOrWhiteSpace(_settings.ThinkingLevel))
            thinkingConfig["thinkingLevel"] = _settings.ThinkingLevel;

        // With both unset the field is absent entirely, which every model family accepts.
        // Neither value is invented here: a model that does not know the field refuses the
        // whole request, so the owner names what their own model understands (ADR-35).
        if (thinkingConfig.Count > 0)
            generationConfig["thinkingConfig"] = thinkingConfig;

        var request = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = $"{instruction}\n\nTitle: {title}\n\nNote:\n{content ?? string.Empty}" }
                    }
                }
            },
            generationConfig
        };

        HttpResponseMessage response;
        string body;

        try
        {
            response = await _httpClient.PostAsJsonAsync(
                $"models/{_settings.Model}:generateContent", request, cancellationToken);

            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            // Nothing arrived, so nothing was billed. The provider's own message stays in
            // the log; the client gets a generic 502.
            _logger.LogWarning(exception, "The AI provider could not be reached.");
            throw Unreachable();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The client is still waiting, so this cancellation is our own timeout (§15.3).
            // A caller-cancelled request rethrows instead and becomes 499, not 502.
            _logger.LogWarning("The AI provider did not answer within {Seconds}s.", _settings.TimeoutSeconds);
            throw Unreachable();
        }

        if (!response.IsSuccessStatusCode)
        {
            // The refusal body names the real cause - an unknown model, a disabled key, a
            // generationConfig field this model does not know - and without it the first
            // real call is a 502 with nothing to go on. It is logged and never returned:
            // the response body must not say which provider is behind the endpoint (15.3).
            _logger.LogWarning(
                "The AI provider answered {Status}: {Body}",
                (int)response.StatusCode,
                Truncate(body, MaxLoggedBodyLength));
            throw Unreachable();
        }

        // From here on a response exists, so it has been paid for even if it is unusable.
        var tokensUsed = ReadTokenCount(body, title, content);

        // Why the model stopped is the one field worth having when the answer is unusable:
        // MAX_TOKENS means the budget ran out, which on a thinking model can happen before
        // a single character of the answer has been written.
        var finishReason = ReadFinishReason(body);

        string answerText;

        try
        {
            answerText = ReadAnswerText(body);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            _logger.LogWarning(
                exception,
                "The AI provider answered in a shape this code cannot read. finishReason: {FinishReason}.",
                finishReason ?? "(none)");
            throw Unreadable(tokensUsed);
        }

        if (string.IsNullOrWhiteSpace(answerText))
        {
            // A thinking model that spends the whole budget thinking returns a candidate
            // with no answer parts at all, and the tokens have still been charged.
            if (string.Equals(finishReason, "MAX_TOKENS", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "The AI provider stopped at the output limit before answering. Raise Ai:MaxOutputTokens, "
                    + "or cap the model's thinking with Ai:ThinkingBudget or Ai:ThinkingLevel.");
            }
            else
            {
                _logger.LogWarning(
                    "The AI provider returned no answer text. finishReason: {FinishReason}.",
                    finishReason ?? "(none)");
            }

            throw Unreadable(tokensUsed);
        }

        JsonElement payload;

        try
        {
            // The model's answer is a JSON string inside the envelope. Clone it, or the
            // element dies with the document it was parsed from.
            using var answer = JsonDocument.Parse(StripCodeFence(answerText));
            payload = answer.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "The AI provider's answer was not the JSON it was asked for. finishReason: {FinishReason}.",
                finishReason ?? "(none)");
            throw Unreadable(tokensUsed);
        }

        return (payload, tokensUsed);
    }

    /// <summary>
    /// The answer text of the first candidate, with the model's own thinking left out.
    /// <para>
    /// A thinking model returns its reasoning as extra parts marked <c>thought</c>, and when
    /// it exhausts the output budget while thinking it returns a candidate with no parts at
    /// all - sometimes with no content object either. Reading <c>parts[0].text</c> would
    /// hand the thoughts back as if they were the answer in the first case, and throw in the
    /// second. Every step here is therefore optional, and thought parts are skipped.
    /// </para>
    /// </summary>
    private static string ReadAnswerText(string body)
    {
        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array
            || candidates.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        if (!candidates[0].TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts)
            || parts.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var answer = new StringBuilder();

        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind != JsonValueKind.Object)
                continue;

            if (part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True)
                continue;

            if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                answer.Append(text.GetString());
        }

        return answer.ToString();
    }

    /// <summary>
    /// Why the model stopped, when it said so. Read on its own and never allowed to throw,
    /// because its only job is to make the failure that follows diagnosable.
    /// </summary>
    private static string? ReadFinishReason(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty("candidates", out var candidates)
                && candidates.ValueKind == JsonValueKind.Array
                && candidates.GetArrayLength() > 0
                && candidates[0].TryGetProperty("finishReason", out var reason)
                && reason.ValueKind == JsonValueKind.String)
            {
                return reason.GetString();
            }
        }
        catch (JsonException)
        {
            // An unreadable envelope has no finish reason to report; the caller says so.
        }

        return null;
    }

    /// <summary>
    /// Removes a markdown code fence the model wrapped its JSON in despite being asked not
    /// to. A reasoning model is the likeliest to add one, and a fence is the difference
    /// between a usable answer and a 502 the user has already paid for.
    /// </summary>
    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return trimmed;

        var firstLineEnd = trimmed.IndexOf('\n');

        if (firstLineEnd < 0)
            return trimmed;

        var inner = trimmed[(firstLineEnd + 1)..];
        var closingFence = inner.LastIndexOf("```", StringComparison.Ordinal);

        return (closingFence < 0 ? inner : inner[..closingFence]).Trim();
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    /// <summary>
    /// What the provider says the call cost. When the usage field is missing, the pre-call
    /// estimate is recorded instead (ADR-36): a paid call with no figure attached must
    /// still be counted, and the estimate is the only number available.
    /// </summary>
    private int ReadTokenCount(string body, string title, string? content)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.TryGetProperty("usageMetadata", out var usage)
                && usage.TryGetProperty("totalTokenCount", out var total)
                && total.TryGetInt32(out var tokens)
                && tokens > 0)
            {
                return tokens;
            }
        }
        catch (JsonException)
        {
            // The envelope itself is unreadable; the estimate below is all that is left.
        }

        _logger.LogWarning("The AI provider reported no token count; the estimate was recorded instead.");
        return _settings.EstimateTokens(title, content);
    }

    private static string ReadString(JsonElement payload, string propertyName, int tokensUsed)
    {
        if (!payload.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw Unreadable(tokensUsed);
        }

        return property.GetString()!.Trim();
    }

    private static string Clamp(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength].TrimEnd();

    // Nothing arrived: no token count exists, and that loss is accepted (§15.3).
    private static ExternalServiceException Unreachable()
        => new("The AI provider is unavailable.");

    // A response arrived, so the tokens it reported travel with the exception, and the
    // handler records them before the client sees the 502 (§15.3).
    private static ExternalServiceException Unreadable(int tokensBilled)
        => new("The AI provider returned a response that could not be used.", tokensBilled);
}
