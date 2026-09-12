namespace StudyHub.Application.Common.Exceptions;

// يُرمى حين يكون الكيان موجودًا فعلًا لكنه ليس ملك المستخدم الحالي —
// الفرق عن NotFoundException أن الوجود لا يُخفى، فقط حق الوصول يُمنع. يُترجَم إلى 403.
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}