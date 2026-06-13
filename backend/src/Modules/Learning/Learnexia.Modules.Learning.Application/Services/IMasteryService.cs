using Learnexia.Modules.Learning.Application.Features.Mastery.Dtos;

namespace Learnexia.Modules.Learning.Application.Services;

/// <summary>
/// In-process seam for per-student mastery data — consumable by other Learning module features
/// (P3-08 adaptive difficulty, P3-10 spaced-repetition scheduler, P3-11 weak-area detection)
/// without going through HTTP.
///
/// P5-02 CROSS-MODULE READ (deferred): the Parent module (P5-02 weak-area report) needs to read
/// a child's mastery data. Since Parent and Learning are separate modules, this CANNOT be satisfied
/// via this interface directly — it would violate module isolation (CLAUDE rule 12).
/// When P5-02 is built, the correct approach is one of:
///   (a) An integration event / read-model exposed through Shared.Contracts, OR
///   (b) A Shared.Contracts interface seam implemented here and consumed cross-module.
/// This is flagged for the P5-02 story. Do not add a cross-module project reference here.
/// </summary>
public interface IMasteryService
{
    /// <summary>
    /// Returns all <see cref="MasteryDto"/> rows for the given student.
    /// Used by P3-08 (difficulty selection), P3-10 (SR scheduling), and P3-11 (weak-area detection)
    /// as an in-process read within the Learning module boundary.
    /// </summary>
    Task<List<MasteryDto>> GetMasteryForStudent(int studentId, CancellationToken ct = default);
}
