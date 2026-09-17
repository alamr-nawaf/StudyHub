using MediatR;

namespace StudyHub.Application.Users.Commands.SeedAdministrator;

// يُرسَل مرة عند الإقلاع من إعدادات AdminSeed، لا من أي endpoint.
// FullName وPassword لازمان فقط حين لا يوجد حساب بهذا الإيميل بعد
public record SeedAdministratorCommand(
    string Email,
    string? FullName,
    string? Password) : IRequest<SeedAdministratorResult>;
