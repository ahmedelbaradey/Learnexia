namespace Learnexia.Modules.Learning.Application.Features.Skills.Dtos;

/// <summary>
/// Admin read model for a single skill (used by List + GetById admin endpoints).
/// Extends <see cref="SkillDto"/> with <see cref="IsActive"/> so admins can see
/// active/inactive state in the list — consistent with the Subjects/Units/Lessons pattern.
///
/// Security: this DTO is returned ONLY by AdminOnly endpoints (Skills/List, Skills/GetById).
/// The student-facing SkillTree uses <see cref="Learnexia.Modules.Learning.Application.Features.Subjects.Dtos.SkillNodeDto"/>
/// which does NOT carry IsActive, so inactive status is never disclosed to students.
/// </summary>
public record SingleSkillResponse : SkillDto
{
    /// <summary>
    /// P7-03 / api-tester #7: Whether this skill is currently active.
    /// Mapped read-only from <c>Skill.IsActive</c> (mass-assignment Ignore on Add/Edit command maps).
    /// </summary>
    public bool IsActive { get; set; }
}
