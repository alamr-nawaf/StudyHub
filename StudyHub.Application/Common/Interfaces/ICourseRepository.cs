using StudyHub.Domain.Entities;

namespace StudyHub.Application.Common.Interfaces;

public interface ICourseRepository
{
    Task<Course?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    // متزامنة عن قصد: المعرّف يُولَّد في الكيان، فلا حاجة لجولة على القاعدة
    void Add(Course course);
}