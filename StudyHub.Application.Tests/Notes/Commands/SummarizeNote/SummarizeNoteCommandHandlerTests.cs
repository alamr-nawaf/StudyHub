using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Settings;
using StudyHub.Application.Common.Time;
using StudyHub.Application.Notes.Commands.SummarizeNote;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Tests.Notes.Commands.SummarizeNote;

public class SummarizeNoteCommandHandlerTests
{
    // 4 characters per token plus a 10-token reserve keeps every estimate small enough to
    // reason about: "A note" with "Some content" costs (6 + 12) / 4 + 10 = 14.
    private static readonly AiSettings Settings = new(
        BaseUrl: "https://example.invalid/",
        Model: "",
        TimeoutSeconds: 30,
        MaxOutputTokens: 800,
        CharsPerToken: 4,
        ResponseReserveTokens: 10,
        MaxSuggestions: 5);

    // Riyadh observes no daylight saving, so a fixed UTC+3 zone is exact and keeps the
    // tests independent of the machine's time-zone database
    private static readonly BusinessCalendar Calendar = new(
        TimeZoneInfo.CreateCustomTimeZone("Test/Riyadh", TimeSpan.FromHours(3), "Riyadh", "Riyadh"));

    private readonly Mock<IItemRepository> _itemRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IAiService> _aiServiceMock = new();
    private readonly Mock<IAiUsageLedger> _usageLedgerMock = new();
    private readonly SummarizeNoteCommandHandler _handler;

    private User _user = User.Create("Quota Owner", "quota@test.com", "hash", 1_000);

    public SummarizeNoteCommandHandlerTests()
    {
        CurrentUserIs(_user);

        _aiServiceMock
            .Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiSummaryResult("A short summary.", 120));

        _handler = new SummarizeNoteCommandHandler(
            _itemRepositoryMock.Object,
            _userRepositoryMock.Object,
            _currentUserMock.Object,
            _aiServiceMock.Object,
            _usageLedgerMock.Object,
            Settings,
            Calendar);
    }

    private void CurrentUserIs(User user)
    {
        _user = user;
        _currentUserMock.Setup(c => c.UserId).Returns(user.Id);
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
    }

    private Item StoredItem(Item item)
    {
        _itemRepositoryMock
            .Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        return item;
    }

    private Note StoredNote(Guid? ownerId = null)
        => (Note)StoredItem(Note.Create(ownerId ?? _user.Id, "A note", "Some content"));

    private static Note NoteAtMaxDepth(Guid ownerId)
    {
        var note = Note.Create(ownerId, "Depth 0");

        for (var depth = 1; depth <= Item.MaxDepth; depth++)
            note = Note.Create(ownerId, $"Depth {depth}", parent: note);

        return note;
    }

    private void VerifyProviderWasNeverCalled()
        => _aiServiceMock.Verify(
            s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private void VerifyNothingWasRecorded()
        => _usageLedgerMock.Verify(
            r => r.RecordAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

    [Fact]
    public async Task Handle_OwnNote_ShouldReturnTheSummaryAndRecordTheUsageOnce()
    {
        var note = StoredNote();

        var result = await _handler.Handle(new SummarizeNoteCommand(note.Id), CancellationToken.None);

        result.Should().Be(new AiSummaryResult("A short summary.", 120));
        _usageLedgerMock.Verify(
            r => r.RecordAsync(_user.Id, "Summarize", 120, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoteAtMaximumDepth_ShouldStillSummarize()
    {
        var note = (Note)StoredItem(NoteAtMaxDepth(_user.Id));

        var result = await _handler.Handle(new SummarizeNoteCommand(note.Id), CancellationToken.None);

        // A summary creates nothing, so the depth guard of ADR-26 does not apply to it
        result.TokensUsed.Should().Be(120);
    }

    [Fact]
    public async Task Handle_MissingNote_ShouldThrowNotFoundWithoutCallingTheProvider()
    {
        var act = () => _handler.Handle(new SummarizeNoteCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        VerifyProviderWasNeverCalled();
        VerifyNothingWasRecorded();
    }

    [Fact]
    public async Task Handle_TaskId_ShouldThrowNotFoundWithoutCallingTheProvider()
    {
        var task = StoredItem(TaskItem.Create(_user.Id, "A task"));

        var act = () => _handler.Handle(new SummarizeNoteCommand(task.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        VerifyProviderWasNeverCalled();
        VerifyNothingWasRecorded();
    }

    [Fact]
    public async Task Handle_NoteOfAnotherUser_ShouldThrowForbiddenWithoutCallingTheProvider()
    {
        var note = StoredNote(ownerId: Guid.NewGuid());

        var act = () => _handler.Handle(new SummarizeNoteCommand(note.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        VerifyProviderWasNeverCalled();
        VerifyNothingWasRecorded();
    }

    [Fact]
    public async Task Handle_QuotaTooSmallForTheEstimate_ShouldThrowQuotaExceededWithoutCallingTheProvider()
    {
        CurrentUserIs(User.Create("Quota Owner", "quota@test.com", "hash", 20));
        _usageLedgerMock
            .Setup(l => l.SumTokensSinceAsync(_user.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(19);
        var note = StoredNote();

        var act = () => _handler.Handle(new SummarizeNoteCommand(note.Id), CancellationToken.None);

        await act.Should().ThrowAsync<QuotaExceededException>();
        VerifyProviderWasNeverCalled();
        VerifyNothingWasRecorded();
    }

    [Fact]
    public async Task Handle_Always_ShouldSumUsageFromTheStartOfTheRiyadhMonth()
    {
        var note = StoredNote();
        DateTime? askedSince = null;
        _usageLedgerMock
            .Setup(l => l.SumTokensSinceAsync(_user.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTime, CancellationToken>((_, since, _) => askedSince = since)
            .ReturnsAsync(0);
        var before = DateTime.UtcNow;

        await _handler.Handle(new SummarizeNoteCommand(note.Id), CancellationToken.None);

        // Either side of the call, in case the test runs across a Riyadh month boundary
        var after = DateTime.UtcNow;
        askedSince.Should().NotBeNull();
        askedSince!.Value.Kind.Should().Be(DateTimeKind.Utc);
        askedSince.Value.Should().BeOneOf(Calendar.MonthStartUtc(before), Calendar.MonthStartUtc(after));
    }

    [Fact]
    public async Task Handle_ProviderFailedWithNothingBilled_ShouldRethrowWithoutRecording()
    {
        var note = StoredNote();
        _aiServiceMock
            .Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalServiceException("The AI provider is unavailable."));

        var act = () => _handler.Handle(new SummarizeNoteCommand(note.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ExternalServiceException>();
        VerifyNothingWasRecorded();
    }

    [Fact]
    public async Task Handle_UnreadableResponseWithTokensBilled_ShouldRecordOnceAndRethrow()
    {
        var note = StoredNote();
        _aiServiceMock
            .Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalServiceException("Unusable.", tokensBilled: 77));

        var act = () => _handler.Handle(new SummarizeNoteCommand(note.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ExternalServiceException>();
        _usageLedgerMock.Verify(
            r => r.RecordAsync(_user.Id, "Summarize", 77, It.IsAny<CancellationToken>()), Times.Once);
    }
}
