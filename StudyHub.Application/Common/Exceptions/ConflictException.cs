namespace StudyHub.Application.Common.Exceptions;

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
// 404 — الكيان غير موجود
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

// 403 — الكيان موجود لكنه ليس ملك المستخدم الحالي
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}