using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Settings;
using StudyHub.Infrastructure.Ai;

namespace StudyHub.Infrastructure.Tests.Ai;

public class GeminiAiServiceTests
{
    private static readonly AiSettings Settings = new(
        BaseUrl: "https://example.invalid/",
        Model: "test-model",
        TimeoutSeconds: 30,
        MaxOutputTokens: 2048,
        CharsPerToken: 4,
        ResponseReserveTokens: 400,
        MaxSuggestions: 5);

    // What a reasoning model answers with: its thinking comes back as extra parts marked
    // "thought", before the part that actually carries the answer.
    private const string ThinkingSummaryResponse =
        """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  { "text": "The note lists three chores. I will compress them.", "thought": true },
                  { "text": "{\"summary\":\"Three chores before Monday.\"}" }
                ]
              },
              "finishReason": "STOP"
            }
          ],
          "usageMetadata": { "promptTokenCount": 30, "thoughtsTokenCount": 150, "totalTokenCount": 200 }
        }
        """;

    // The same model when the output budget ran out while it was still thinking: a candidate
    // with no content at all, and tokens already charged for.
    private const string BudgetSpentThinkingResponse =
        """
        {
          "candidates": [ { "finishReason": "MAX_TOKENS", "index": 0 } ],
          "usageMetadata": { "promptTokenCount": 30, "thoughtsTokenCount": 870, "totalTokenCount": 900 }
        }
        """;

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;

        public string? CapturedRequestBody { get; private set; }

        public StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                CapturedRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
        }
    }

    private static GeminiAiService CreateSut(StubHandler handler, AiSettings? settings = null)
        => new(
            new HttpClient(handler) { BaseAddress = new Uri(Settings.BaseUrl) },
            settings ?? Settings,
            NullLogger<GeminiAiService>.Instance);

    private static JsonElement GenerationConfigOf(StubHandler handler)
        => JsonDocument.Parse(handler.CapturedRequestBody!).RootElement.GetProperty("generationConfig").Clone();

    [Fact]
    public async Task SummarizeAsync_WhenTheModelReturnsThoughtParts_ShouldReadTheAnswerAndNotTheThoughts()
    {
        var sut = CreateSut(new StubHandler(ThinkingSummaryResponse));

        var result = await sut.SummarizeAsync("A note", "Some content", CancellationToken.None);

        result.Summary.Should().Be("Three chores before Monday.");
        result.Summary.Should().NotContain("I will compress them");
        result.TokensUsed.Should().Be(200);
    }

    [Fact]
    public async Task ExtractTasksAsync_WhenTheModelReturnsThoughtParts_ShouldReadTheSuggestions()
    {
        const string response =
            """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      { "text": "Listing what is still undone.", "thought": true },
                      { "text": "{\"tasks\":[{\"title\":\"Read chapter four\",\"content\":null}]}" }
                    ]
                  },
                  "finishReason": "STOP"
                }
              ],
              "usageMetadata": { "totalTokenCount": 180 }
            }
            """;
        var sut = CreateSut(new StubHandler(response));

        var result = await sut.ExtractTasksAsync("A note", "Some content", CancellationToken.None);

        result.Suggestions.Should().ContainSingle().Which.Title.Should().Be("Read chapter four");
        result.TokensUsed.Should().Be(180);
    }

    [Fact]
    public async Task SummarizeAsync_WhenTheAnswerIsSplitInsideAStringValue_ShouldJoinThePartsWithNothingBetween()
    {
        // The split falls mid-word inside the summary string: a newline between the parts
        // would make the JSON invalid, and a space would change the summary
        const string response =
            """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      { "text": "{\"summary\":\"Three cho" },
                      { "text": "res before Monday.\"}" }
                    ]
                  },
                  "finishReason": "STOP"
                }
              ],
              "usageMetadata": { "totalTokenCount": 120 }
            }
            """;
        var sut = CreateSut(new StubHandler(response));

        var result = await sut.SummarizeAsync("A note", "Some content", CancellationToken.None);

        result.Summary.Should().Be("Three chores before Monday.");
    }

    [Fact]
    public async Task SummarizeAsync_WhenTheAnswerPartCarriesAThoughtSignature_ShouldReadItAsTheAnswer()
    {
        // The shape the live provider returned: the answer part carries a thoughtSignature
        // but no "thought" flag. Only "thought": true marks a part as thinking
        const string response =
            """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      { "text": "{\"summary\":\"Signed but still the answer.\"}", "thoughtSignature": "c2lnbmF0dXJl" }
                    ],
                    "role": "model"
                  },
                  "finishReason": "STOP"
                }
              ],
              "usageMetadata": { "totalTokenCount": 140 }
            }
            """;
        var sut = CreateSut(new StubHandler(response));

        var result = await sut.SummarizeAsync("A note", "Some content", CancellationToken.None);

        result.Summary.Should().Be("Signed but still the answer.");
        result.TokensUsed.Should().Be(140);
    }

    [Fact]
    public async Task SummarizeAsync_WhenTheBudgetRanOutWhileThinking_ShouldThrowBillingWhatTheProviderReported()
    {
        var sut = CreateSut(new StubHandler(BudgetSpentThinkingResponse));

        var act = () => sut.SummarizeAsync("A note", "Some content", CancellationToken.None);

        // The answer never arrived, but the thinking was charged for, so the handler must be
        // able to record it before the client is given its 502 (§15.3)
        var thrown = await act.Should().ThrowAsync<ExternalServiceException>();
        thrown.Which.TokensBilled.Should().Be(900);
    }

    [Fact]
    public async Task SummarizeAsync_WhenTheAnswerIsWrappedInACodeFence_ShouldStillReadIt()
    {
        const string response =
            """
            {
              "candidates": [
                { "content": { "parts": [ { "text": "```json\n{\"summary\":\"Fenced but usable.\"}\n```" } ] } }
              ],
              "usageMetadata": { "totalTokenCount": 90 }
            }
            """;
        var sut = CreateSut(new StubHandler(response));

        var result = await sut.SummarizeAsync("A note", "Some content", CancellationToken.None);

        result.Summary.Should().Be("Fenced but usable.");
    }

    [Fact]
    public async Task SummarizeAsync_WithNeitherThinkingSettingSet_ShouldSendNoThinkingConfig()
    {
        var handler = new StubHandler(ThinkingSummaryResponse);
        var sut = CreateSut(handler);

        await sut.SummarizeAsync("A note", "Some content", CancellationToken.None);

        // A model that does not know the field refuses the whole request, so it is absent
        // until the owner asks for it
        GenerationConfigOf(handler).TryGetProperty("thinkingConfig", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SummarizeAsync_WithAThinkingBudgetOfZero_ShouldStillSendIt()
    {
        var handler = new StubHandler(ThinkingSummaryResponse);
        var sut = CreateSut(handler, Settings with { ThinkingBudget = 0 });

        await sut.SummarizeAsync("A note", "Some content", CancellationToken.None);

        // Zero is a real instruction — "do not think" — not an absent value
        var thinking = GenerationConfigOf(handler).GetProperty("thinkingConfig");
        thinking.GetProperty("thinkingBudget").GetInt32().Should().Be(0);
        thinking.TryGetProperty("thinkingLevel", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SummarizeAsync_WithAThinkingLevel_ShouldSendItAlone()
    {
        var handler = new StubHandler(ThinkingSummaryResponse);
        var sut = CreateSut(handler, Settings with { ThinkingLevel = "low" });

        await sut.SummarizeAsync("A note", "Some content", CancellationToken.None);

        var thinking = GenerationConfigOf(handler).GetProperty("thinkingConfig");
        thinking.GetProperty("thinkingLevel").GetString().Should().Be("low");
        thinking.TryGetProperty("thinkingBudget", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SummarizeAsync_WhenTheProviderRefusesTheRequest_ShouldThrowWithNothingBilled()
    {
        const string refusal =
            """{"error":{"code":400,"message":"Unknown name \"thinkingConfig\"","status":"INVALID_ARGUMENT"}}""";
        var sut = CreateSut(new StubHandler(refusal, HttpStatusCode.BadRequest));

        var act = () => sut.SummarizeAsync("A note", "Some content", CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ExternalServiceException>();
        thrown.Which.TokensBilled.Should().Be(0);
        thrown.Which.Message.Should().NotContain("thinkingConfig");
    }

    [Fact]
    public async Task SummarizeAsync_WhenTheProviderReportsNoTokenCount_ShouldRecordTheEstimate()
    {
        const string response =
            """{"candidates":[{"content":{"parts":[{"text":"{\"summary\":\"No usage field.\"}"}]}}]}""";
        var sut = CreateSut(new StubHandler(response));

        var result = await sut.SummarizeAsync("A note", "Some content", CancellationToken.None);

        result.TokensUsed.Should().Be(Settings.EstimateTokens("A note", "Some content"));
    }
}
