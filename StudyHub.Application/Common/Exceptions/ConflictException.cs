namespace StudyHub.Application.Common.Exceptions;

// ConflictException.cs — 409 تعارض مع حالة قائمة
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
// NotFoundException.cs — 404 الكيان غير موجود
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

// ForbiddenException.cs — 403 موجود لكنه ليس ملك المستخدم الحالي
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}