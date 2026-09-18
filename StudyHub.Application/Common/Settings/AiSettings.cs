namespace StudyHub.Application.Common.Settings;

/// <summary>
/// The non-secret configuration of the AI provider, under the "Ai" section. The API key
/// is not here: it is a user secret read where the provider is wired (ADR-35).
/// <c>CharsPerToken</c> and <c>ResponseReserveTokens</c> drive the pre-call estimate of
/// ADR-36 — an approximation, not a tokenizer.
/// <para>
/// <c>ThinkingBudget</c> and <c>ThinkingLevel</c> are optional, and both being unset is the
/// normal case: the provider is then asked for no thinking configuration at all. They are
/// passed straight through because how much a model may think is the provider's own
/// vocabulary, and a guessed value is refused by the models that do not know the field.
/// </para>
/// </summary>
public record AiSettings(
    string BaseUrl,
    string Model,
    int TimeoutSeconds,
    int MaxOutputTokens,
    int CharsPerToken,
    int ResponseReserveTokens,
    int MaxSuggestions,
    int? ThinkingBudget = null,
    string? ThinkingLevel = null)
{
    public const string SectionName = "Ai";

    /// <summary>
    /// The pre-call cost estimate for a note, in tokens (ADR-36).
    /// </summary>
    public int EstimateTokens(string title, string? content)
        => (title.Length + (content?.Length ?? 0)) / CharsPerToken + ResponseReserveTokens;
}
