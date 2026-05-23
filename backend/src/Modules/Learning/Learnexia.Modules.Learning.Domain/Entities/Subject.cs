using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// A curriculum subject (Math, Science, Arabic, English) scoped to a single <see cref="Grade"/>.
/// Subject 1—* Unit and Subject 1—* Concept.
/// </summary>
public class Subject : AggregateRoot
{
    public string Name { get; set; } = null!;
    public string? Country { get; set; }

    public int GradeId { get; set; }
    public Grade Grade { get; set; } = null!;

    public List<Unit> Units { get; set; } = null!;
    public List<Concept> Concepts { get; set; } = null!;
}
