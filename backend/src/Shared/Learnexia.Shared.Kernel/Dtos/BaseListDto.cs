namespace Learnexia.Shared.Kernel.Dtos;

public record BaseListDto
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public string? Search { get; set; }
    public string? OrderBy { get; set; }
}
