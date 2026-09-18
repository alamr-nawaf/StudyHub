using FluentAssertions;
using Moq;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Settings;
using StudyHub.Application.Notes.Commands.SummarizeNote;
using StudyHub.Application.Tests.Common;
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

    private readonly Mock<IItemRepository> _itemRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IAiService> _aiServiceMock = new();
    private readonly Mock<IAiUsageRecorder> _usageRecorderMock = new();
    private readonly SummarizeNoteCommandHandler _handler;

    private User _user = PersistedUser.With(quota: 1_000, tokensUsed: 0);

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
            _unitOfWorkMock.Object,
            _aiServiceMock.Object,
            _usageRecorderMock.Object,
            Settings);
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
        => _usageRecorderMock.Verify(
            r => r.RecordAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

    [Fact]
    public async Task Handle_OwnNote_ShouldReturnTheSummaryAndRecordTheUsageOnce()
    {
        var note = StoredNote();

        var result = await _handler.Handle(new SummarizeNoteCommand(note.Id), CancellationToken.None);

        result.Should().Be(new AiSummaryResult("A short summary.", 120));
        _usageRecorderMock.Verify(
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
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TaskId_ShouldThrowNotFoundWithoutCallingTheProvider()
    {
        var task = StoredItem(TaskItem.Create(_user.Id, "A task"));

        var act = () => _handler.Handle(new SummarizeNoteCommand(task.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        VerifyProviderWasNeverCalled();
        VerifyNothingWasRecorded();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoteOfAnotherUser_ShouldThrowForbiddenWithoutCallingTheProvider()
    {
        var note = StoredNote(ownerId: Guid.NewGuid());

        var act = () => _handler.Handle(new SummarizeNoteCommand(note.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        VerifyProviderWasNeverCalled();
        VerifyNothingWasRecorded();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_QuotaTooSmallForTheEstimate_ShouldThrowQuotaExceededWithoutCallingTheProvider()
    {
        CurrentUserIs(PersistedUser.With(quota: 20, tokensUsed: 19));
        var note = StoredNote();

        var act = () => _handler.Handle(new SummarizeNoteCommand(note.Id), CancellationToken.None);

        await act.Should().ThrowAsync<QuotaExceededException>();
        VerifyProviderWasNeverCalled();
        VerifyNothingWasRecorded();
    }

    [Fact]
    public async Task Handle_NewMonth_ShouldResetTheQuotaBeforeCheckingIt()
    {
        // Spent to the last token last month: without the reset this call is refused
        CurrentUserIs(PersistedUser.With(
            quota: 20, tokensUsed: 20, lastReset: DateTime.UtcNow.AddMonths(-1)));
        var note = StoredNote();

        var result = await _handler.Handle(new SummarizeNoteCommand(note.Id), CancellationToken.None);

        result.TokensUsed.Should().Be(120);
        _user.TokensUsedThisMonth.Should().Be(0);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SameMonth_ShouldNotSaveTheUnchangedUser()
    {
        var note = StoredNote();

        await _handler.Handle(new SummarizeNoteCommand(note.Id), CancellationToken.None);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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
        _usageRecorderMock.Verify(
            r => r.RecordAsync(_user.Id, "Summarize", 77, It.IsAny<CancellationToken>()), Times.Once);
    }
}
