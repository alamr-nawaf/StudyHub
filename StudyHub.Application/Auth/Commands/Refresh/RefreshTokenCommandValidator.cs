using FluentValidation;

namespace StudyHub.Application.Auth.Commands.Refresh;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    // فارغ فقط. لا قاعدة طول ولا صيغة: توكن مزيَّف اعتمادٌ مرفوض (401) لا طلب مشوَّه (400)
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}