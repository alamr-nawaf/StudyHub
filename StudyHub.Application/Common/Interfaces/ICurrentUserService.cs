namespace StudyHub.Application.Common.Interfaces;

// من هو المستخدم الحالي؟ التطبيق يسأل، والـ API يجيب.
public interface ICurrentUserService
{
    Guid UserId { get; }
}