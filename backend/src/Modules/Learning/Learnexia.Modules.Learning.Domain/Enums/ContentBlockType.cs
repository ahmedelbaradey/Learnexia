namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// Discriminates the content-block types supported by the lesson content editor (P7-02)
/// and the student lesson renderer (P2-05 and future).
///
/// Stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c> per the project no-free-text rule.
///
/// Member rationale:
/// - <see cref="Text"/>     = 1 — rich-text / Markdown paragraph; the primary narrative block.
///   Supersedes the legacy <c>Lesson.Explanation</c> flat field for new authored content.
/// - <see cref="Image"/>    = 2 — image URL or IStorageService asset key; maps directly to the
///   existing <c>Lesson.Visual</c> flat field (the admin can now attach multiple images).
///   Student renderer already handles a single visual block (P2-05).
/// - <see cref="Video"/>    = 3 — video URL or embedded asset key; not yet rendered by the P2-05
///   student player (renderer will add a VideoPlayer component when P7-02 content goes live).
/// - <see cref="Callout"/>  = 4 — highlighted tip/note/warning box; a special-styled text block.
///   Common in educational content to surface key takeaways. Not rendered in P2-05 today;
///   the renderer will add a Callout card component.
///
/// These four types are a strict superset of the P2-05 renderer's current capabilities
/// (it renders <c>explanation</c> as Text and <c>visual</c> as Image) and fully cover the story's
/// <c>text/image/video/callout</c> requirement. Future types (Audio, Interactive, Embed …) add
/// new enum members without breaking existing rows.
/// </summary>
public enum ContentBlockType
{
    Text = 1,
    Image = 2,
    Video = 3,
    Callout = 4,
}
