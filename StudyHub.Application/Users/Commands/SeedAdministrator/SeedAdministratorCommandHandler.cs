using MediatR;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Settings;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Users.Commands.SeedAdministrator;

/// <summary>
/// Creates or promotes the first administrator from configuration. It replaces the manual
/// UPDATE of §9.4, which bypassed PromoteToAdmin and left UpdatedAt unstamped. Safe to run at
/// every startup: an existing account is only promoted, and its password is never touched —
/// otherwise stale configuration would reset a password its owner has since changed.
/// </summary>
public class SeedAdministratorCommandHandler
    : IRequestHandler<SeedAdministratorCommand, SeedAdministratorResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserQuotaSettings _quotaSettings;

    public SeedAdministratorCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        UserQuotaSettings quotaSettings)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _quotaSettings = quotaSettings;
    }

    public async Task<SeedAdministratorResult> Handle(
        SeedAdministratorCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        var created = user is null;

        if (user is null)
        {
            // A configuration error at startup, not an HTTP request: it stops the process
            // with a message naming the missing key
            if (request.FullName is null || request.Password is null)
                throw new InvalidOperationException(
                    "No account exists for AdminSeed:Email, so AdminSeed:FullName and AdminSeed:Password are required.");

            user = User.Create(
                request.FullName,
                request.Email,
                _passwordHasher.Hash(request.Password),
                _quotaSettings.DefaultMonthlyTokens);

            _userRepository.Add(user);
        }

        user.PromoteToAdmin();

        // One save, so there is no state in which the account was created but not promoted
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SeedAdministratorResult(user.Id, created, user.IsActive);
    }
}
