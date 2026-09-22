using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Settings;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Users.Commands.RegisterUser;

/// <summary>
/// Creates an account with the configured default AI quota, and refuses an e-mail that is
/// already taken.
/// </summary>
public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserQuotaSettings _quotaSettings;

    public RegisterUserCommandHandler(
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

    public async Task<Guid> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // The pre-check exists for the friendly message; the unique index in the database is
        // the real protection
        if (await _userRepository.EmailExistsAsync(request.Email, cancellationToken))
            throw new ConflictException("An account with this email already exists.");

        var passwordHash = _passwordHasher.Hash(request.Password);

        var user = User.Create(
            request.FullName, request.Email, passwordHash, _quotaSettings.DefaultMonthlyTokens);

        _userRepository.Add(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}