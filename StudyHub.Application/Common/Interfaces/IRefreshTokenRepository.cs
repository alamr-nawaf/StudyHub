using StudyHub.Domain.Entities;

namespace StudyHub.Application.Common.Interfaces;

// عقد تخزين توكنات التجديد. البحث بالهاش لأن النص الخام لا يُخزَّن أبدًا
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    void Add(RefreshToken token);

    // إلغاء جماعي غير مشروط: ينفّذ فورًا ولا يمرّ بـ IUnitOfWork (ADR-27)
    Task RevokeAllForUserAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken);
}