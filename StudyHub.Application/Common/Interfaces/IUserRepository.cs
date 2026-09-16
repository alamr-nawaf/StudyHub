using StudyHub.Domain.Entities;

namespace StudyHub.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    // متزامنة عن قصد: المعرّف يُولَّد في الكيان، فلا حاجة لجولة على القاعدة
    void Add(User user);

    // للدخول: المقارنة بكائن Email كاملًا لا بقيمته النصية — درس D6
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    // للتجديد: الهوية تأتي من الصف المخزَّن لا من التوكن
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}