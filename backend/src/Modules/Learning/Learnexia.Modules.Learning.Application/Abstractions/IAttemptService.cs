using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Learning.Application.Abstractions;

public interface IAttemptService : IBaseService<Attempt>
{
    /// <summary>
    /// Creates a new <see cref="Attempt"/> (Status=InProgress, StartedAt=UtcNow) and immediately
    /// persists it via <c>LearningDbContext.SaveChangesAsync(studentId)</c> so that the
    /// DB-generated <c>Id</c> is populated before the response is built.
    /// Mirrors the precedent established by <c>LinkParentStudentService</c> (Parent module).
    /// </summary>
    Task<Attempt> StartNewAsync(int studentId, int lessonId, CancellationToken cancellationToken = default);
}
