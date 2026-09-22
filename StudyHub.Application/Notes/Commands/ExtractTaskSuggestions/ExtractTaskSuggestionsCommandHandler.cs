using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Settings;
using StudyHub.Application.Common.Time;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Notes.Commands.ExtractTaskSuggestions;

/// <summary>
/// Suggests tasks from one of the caller's notes, in the order §15 fixes: ownership, the
/// depth guard, the quota question, the paid call, then the record. The depth guard is the
/// one step a summary does not have, and it runs before anything external is called.
/// </summary>
public class ExtractTaskSuggestionsCommandHandler
    : IRequestHandler<ExtractTaskSuggestionsCommand, AiTaskSuggestionsResult>
{
    /// <summary>What an AiUsageLog row of this operation is called (ADR-38).</summary>
    public const string OperationType = "ExtractTaskSuggestions";

    private readonly IItemRepository _itemRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IAiService _aiService;
    private readonly IAiUsageLedger _usageLedger;
    private readonly AiSettings _aiSettings;
    private readonly BusinessCalendar _calendar;

    public ExtractTaskSuggestionsCommandHandler(
        IItemRepository itemRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUser,
        IAiService aiService,
        IAiUsageLedger usageLedger,
        AiSettings aiSettings,
        BusinessCalendar calendar)
    {
        _itemRepository = itemRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
        _aiService = aiService;
        _usageLedger = usageLedger;
        _aiSettings = aiSettings;
        _calendar = calendar;
    }

    public async Task<AiTaskSuggestionsResult> Handle(
        ExtractTaskSuggestionsCommand request, CancellationToken cancellationToken)
    {
        // A task's id on a note's route names no note, so it is 404 and not 400
        if (await _itemRepository.GetByIdAsync(request.NoteId, cancellationToken) is not Note note)
            throw new NotFoundException($"Note '{request.NoteId}' was not found.");

        if (note.UserId != _currentUser.UserId)
            throw new ForbiddenException("You do not own this note.");

        // An approved suggestion becomes a child of this note, and a child of a depth-4
        // note cannot exist — so every suggestion would be impossible to approve. Refusing
        // here spends nothing (ADR-26, ADR-30)
        if (note.IsAtMaxDepth)
            throw new ConflictException(
                "This note is at the maximum depth, so no task could be created under it.");

        var user = await _userRepository.GetByIdAsync(_currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("The account of the current user no longer exists.");

        // The log is the only record of spending, and the month starts at Riyadh midnight,
        // so a new month needs no reset: it simply has no rows yet (ADR-39, ADR-40)
        var used = await _usageLedger.SumTokensSinceAsync(
            user.Id, _calendar.MonthStartUtc(DateTime.UtcNow), cancellationToken);

        var estimate = _aiSettings.EstimateTokens(note.Title, note.Content);

        if (!user.HasQuotaFor(used, estimate))
            throw new QuotaExceededException("Your monthly AI token quota is exhausted.");

        AiTaskSuggestionsResult result;

        try
        {
            result = await _aiService.ExtractTasksAsync(note.Title, note.Content, cancellationToken);
        }
        catch (ExternalServiceException exception) when (exception.TokensBilled > 0)
        {
            // The answer is unusable but it has been paid for: record it, then let the
            // client have its 502 (§15.3)
            await _usageLedger.RecordAsync(
                user.Id, OperationType, exception.TokensBilled, cancellationToken);
            throw;
        }

        await _usageLedger.RecordAsync(user.Id, OperationType, result.TokensUsed, cancellationToken);

        return result;
    }
}
