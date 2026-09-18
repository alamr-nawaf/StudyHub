using FluentAssertions;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Settings;
using StudyHub.Infrastructure.Ai;

namespace StudyHub.Infrastructure.Tests.Ai;

public class FakeAiServiceTests
{
    private static readonly AiSettings Settings = new(
        BaseUrl: "https://example.invalid/",
        Model: "",
        TimeoutSeconds: 30,
        MaxOutputTokens: 800,
        CharsPerToken: 4,
        ResponseReserveTokens: 400,
        MaxSuggestions: 2);

    private static FakeAiService CreateSut(bool failEveryCall = false)
        => new(Settings, failEveryCall);

    [Fact]
    public async Task SummarizeAsync_WithContent_ShouldReturnTheEstimateAsItsTokenCount()
    {
        var sut = CreateSut();

        var result = await sut.SummarizeAsync("Title", "First. Second. Third. Fourth.", CancellationToken.None);

        result.Summary.Should().NotBeEmpty();
        result.TokensUsed.Should().Be(Settings.EstimateTokens("Title", "First. Second. Third. Fourth."));
    }

    [Fact]
    public async Task SummarizeAsync_WithoutContent_ShouldFallBackToTheTitle()
    {
        var sut = CreateSut();

        var result = await sut.SummarizeAsync("A note with no body", null, CancellationToken.None);

        result.Summary.Should().Contain("A note with no body");
    }

    [Fact]
    public async Task ExtractTasksAsync_WithMoreLinesThanAllowed_ShouldNotExceedMaxSuggestions()
    {
        var sut = CreateSut();

        var result = await sut.ExtractTasksAsync("Title", "one\ntwo\nthree", CancellationToken.None);

        result.Suggestions.Should().HaveCount(Settings.MaxSuggestions);
        result.Suggestions.Should().OnlyContain(suggestion => suggestion.Title.Length > 0);
    }

    [Fact]
    public async Task ExtractTasksAsync_WithoutContent_ShouldStillSuggestSomething()
    {
        var sut = CreateSut();

        var result = await sut.ExtractTasksAsync("Revise chapter four", null, CancellationToken.None);

        result.Suggestions.Should().ContainSingle();
    }

    [Fact]
    public async Task SummarizeAsync_WhenFakeFailureIsOn_ShouldThrowWithNothingBilled()
    {
        var sut = CreateSut(failEveryCall: true);

        var act = () => sut.SummarizeAsync("Title", "Body", CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ExternalServiceException>();
        thrown.Which.TokensBilled.Should().Be(0);
    }

    [Fact]
    public async Task ExtractTasksAsync_WhenFakeFailureIsOn_ShouldThrowWithNothingBilled()
    {
        var sut = CreateSut(failEveryCall: true);

        var act = () => sut.ExtractTasksAsync("Title", "Body", CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ExternalServiceException>();
        thrown.Which.TokensBilled.Should().Be(0);
    }
}
