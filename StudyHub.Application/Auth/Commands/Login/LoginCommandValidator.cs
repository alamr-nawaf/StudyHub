using FluentValidation;

namespace StudyHub.Application.Auth.Commands.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        // إلزامية: Email.Create ترمي على إيميل مشوَّه، فبلا هذه القاعدة يرجع 500 بدل 400
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(150);

        // لا قواعد تعقيد هنا: الدخول يقارن بالمخزَّن لا يفرض سياسة.
        // حدّ طول أدنى يرفض كلمة مرور قديمة صحيحة بـ 400 بدل 401
        RuleFor(x => x.Password).NotEmpty();
    }
}