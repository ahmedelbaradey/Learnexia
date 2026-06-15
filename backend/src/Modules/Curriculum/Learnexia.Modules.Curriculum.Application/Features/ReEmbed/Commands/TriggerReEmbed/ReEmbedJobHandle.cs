namespace Learnexia.Modules.Curriculum.Application.Features.ReEmbed.Commands.TriggerReEmbed;

/// <summary>
/// Response DTO returned when an admin triggers the re-embed job.
/// Contains the Hangfire job ID so the caller can track job progress via the Hangfire dashboard.
/// </summary>
/// <param name="JobId">Hangfire job identifier (string, assigned by Hangfire at enqueue time).</param>
/// <param name="Message">Human-readable status message (non-localized, admin-only surface).</param>
public sealed record ReEmbedJobHandle(string JobId, string Message);
