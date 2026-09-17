namespace StudyHub.Application.Users.Commands.SeedAdministrator;

// Created وIsActive للإقلاع وحده: يحذّر من كلمة مرور متروكة في الإعدادات، ومن مسؤول معطَّل لا يستطيع الدخول
public record SeedAdministratorResult(Guid UserId, bool Created, bool IsActive);
