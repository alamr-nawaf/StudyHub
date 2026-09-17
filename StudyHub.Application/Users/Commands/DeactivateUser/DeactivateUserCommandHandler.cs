using MediatR;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Application.Users.Commands.DeactivateUser;

// UC-09. لا فحص ملكية: هذا أول معالِج يعمل على صف غيرك، وحارسه صلاحية الدور على الـ endpoint.
// لا إلغاء لتوكنات التجديد هنا: التجديد يرفض المعطَّل (§9.3)، وتوكن الوصول يعيش مدّته فقط (§9.4)
public class DeactivateUserCommandHandler : IRequestHandler<DeactivateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateUserCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"User '{request.Id}' was not found.");

        // متسامحة: المعطَّل أصلًا يمرّ بلا خطأ، فلا يحتاج المسؤول أن يفحص أولًا
        user.Deactivate();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
