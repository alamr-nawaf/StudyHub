using StudyHub.Domain.Entities;

namespace StudyHub.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    // متزامنة عن قصد: المعرّف يُولَّد في الكيان، فلا حاجة لجولة على القاعدة
    void Add(User user);
}