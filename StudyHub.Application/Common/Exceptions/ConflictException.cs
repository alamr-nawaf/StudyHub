namespace StudyHub.Application.Common.Exceptions;

// يُرمى حين يتعارض الطلب مع حالة قائمة في البيانات — إيميل مكرر اليوم،
// وسيُضاف إليه تعارض تزامن من M6. يُترجَم في GlobalExceptionHandler إلى 409.
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}