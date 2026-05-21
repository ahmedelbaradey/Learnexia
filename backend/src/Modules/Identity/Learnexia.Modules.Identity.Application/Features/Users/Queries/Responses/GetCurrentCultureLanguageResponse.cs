namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.Responses;

public record GetCurrentCultureLanguageResponse
{
    public string CurrentCulture { get; set; } = string.Empty;
    public string PreferredLanguage { get; set; } = string.Empty;
}
