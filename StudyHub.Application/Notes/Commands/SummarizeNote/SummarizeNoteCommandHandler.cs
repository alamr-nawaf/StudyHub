using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Settings;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Notes.Commands.SummarizeNote;

/// <summary>
/// Summarizes one of the caller's notes through the AI provider, in the order §15 fixes:
/// ownership, then the quota question, then the paid call, then the record. There is no
/// depth guard here — a summary creates nothing, so a note at maximum depth is normal.
/// </summary>
public class SummarizeNoteCommandHandler : IRequestHandler<SummarizeNoteCommand, AiSummaryResult>
{
    /// <summary>What an AiUsageLog row of this operation is called (ADR-38).</summary>
    public const string OperationType = "Summarize";

    private readonly IItemRepository _itemRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAiService _aiService;
    private readonly IAiUsageRecorder _usageRecorder;
    private readonly AiSettings _aiSettings;

    public SummarizeNoteCommandHandler(
        IItemRepository itemRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IAiService aiService,
        IAiUsageRecorder usageRecorder,
        AiSettings aiSettings)
    {
        _itemRepository = itemRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _aiService = aiService;
        _usageRecorder = usageRecorder;
        _aiSettings = aiSettings;
    }

    public async Task<AiSummaryResult> Handle(
        SummarizeNoteCommand request, CancellationToken cancellationToken)
    {
        // A task's id on a note's route names no note, so it is 404 and not 400
        if (await _itemRepository.GetByIdAsync(request.NoteId, cancellationToken) is not Note note)
            throw new NotFoundException($"Note '{request.NoteId}' was not found.");

        if (note.UserId != _currentUser.UserId)
            throw new ForbiddenException("You do not own this note.");

        var user = await _userRepository.GetByIdAsync(_currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("The account of the current user no longer exists.");

        // Before the check, or a user entering a new month is refused against last
        // month's counter (§15.1)
        var lastReset = user.LastTokenResetDate;
        user.ResetQuotaIfNeeded(DateTime.UtcNow);

        if (user.LastTokenResetDate != lastReset)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        var estimate = _aiSettings.EstimateTokens(note.Title, note.Content);

        if (!user.HasQuotaFor(estimate))
            throw new QuotaExceededException("Your monthly AI token quota is exhausted.");

        AiSummaryResult result;

        try
        {
            result = await _aiService.SummarizeAsync(note.Title, note.Content, cancellationToken);
        }
        catch (ExternalServiceException exception) when (exception.TokensBilled > 0)
        {
            // The answer is unusable but it has been paid for: record it, then let the
            // client have its 502 (§15.3)
            await _usageRecorder.RecordAsync(
                user.Id, OperationType, exception.TokensBilled, cancellationToken);
            throw;
        }

        await _usageRecorder.RecordAsync(user.Id, OperationType, result.TokensUsed, cancellationToken);

        return result;
    }
}
