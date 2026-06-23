namespace Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Validators;

/// <summary>
/// Detected curriculum file type — resolved from both the declared content-type (allow-list)
/// and the actual magic-byte signature. The detected type determines the MIME string used when
/// storing the object in the curriculum bucket (stored with the TRUE detected type, not the
/// client-declared one). Technical identifiers — not user-facing text (no localization).
/// </summary>
public enum CurriculumFileType
{
    Pdf,
    Docx,
    Jpeg,
    Png,
    Webp,
}
