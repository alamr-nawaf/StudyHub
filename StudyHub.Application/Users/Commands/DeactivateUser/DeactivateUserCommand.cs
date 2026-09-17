using MediatR;

namespace StudyHub.Application.Users.Commands.DeactivateUser;

// المعرّف هو الحساب المستهدَف لا هوية المتصل — الصلاحية فُحصت بالسياسة قبل الوصول هنا (§9.5)
public record DeactivateUserCommand(Guid Id) : IRequest;
