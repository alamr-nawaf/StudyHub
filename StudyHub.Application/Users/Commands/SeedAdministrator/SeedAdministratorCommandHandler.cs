using MediatR;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Settings;
using StudyHub.Domain.Entities;

namespace StudyHub.Application.Users.Commands.SeedAdministrator;

// يحلّ محلّ UPDATE اليدوي (§9.4) الذي كان يتجاوز PromoteToAdmin ويترك UpdatedAt فارغًا.
// آمن للتكرار عند كل إقلاع: الحساب القائم يُرقّى فقط، وكلمة مروره لا تُمسّ أبدًا —
// وإلا لأعادت الإعدادات القديمة تعيين كلمة مرور غيّرها صاحبها
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
            // خطأ إعدادات عند الإقلاع لا طلب HTTP: يوقف التشغيل برسالة تسمّي المفتاح الناقص
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

        // حفظ واحد: لا يوجد حساب أُنشئ ولم يُرقَّ
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SeedAdministratorResult(user.Id, created, user.IsActive);
    }
}
