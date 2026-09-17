using FluentValidation;
using StudyHub.Application.Common.Validation;

namespace StudyHub.Application.Users.Commands.SeedAdministrator;

public class SeedAdministratorCommandValidator : AbstractValidator<SeedAdministratorCommand>
{
    public SeedAdministratorCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(150);

        // مشروطة: حساب قائم يُرقّى بلا كلمة مرور، لكن ما يُكتب في الإعدادات يلتزم سياسة التسجيل نفسها —
        // حساب المسؤول أضعف ما يكون لو سُمح له بما يُرفض لغيره
        RuleFor(x => x.Password!)
            .StrongPassword()
            .When(x => x.Password is not null);

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(150)
            .When(x => x.FullName is not null);
    }
}
