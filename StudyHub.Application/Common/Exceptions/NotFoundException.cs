namespace StudyHub.Application.Common.Exceptions;

// يُرمى حين يطلب المستخدم كيانًا غير موجود — لم يُنشأ أصلًا،
// أو حُذف منطقيًا فرشّحه مرشّح الحذف. يُترجَم إلى 404.
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}