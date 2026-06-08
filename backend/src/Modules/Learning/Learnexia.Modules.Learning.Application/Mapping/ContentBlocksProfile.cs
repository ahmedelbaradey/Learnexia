using AutoMapper;
using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Mapping;

/// <summary>
/// AutoMapper profile for ContentBlock → DTO mappings (P7-02).
///
/// Mass-assignment prevention:
/// - IsActive defaults to true on the entity (not mapped from command input).
/// - SequenceOrder is set by the handler on Add (append) or ReorderContentBlocks command; not from command input.
/// - IsDeleted is set by DeleteContentBlockCommand; never from Add/Edit input.
/// </summary>
public class ContentBlocksProfile : Profile
{
    public ContentBlocksProfile()
    {
        // ContentBlock entity → student-facing DTO (no IsActive).
        CreateMap<ContentBlock, ContentBlockDto>();

        // ContentBlock entity → admin-facing DTO (includes IsActive).
        CreateMap<ContentBlock, AdminContentBlockDto>();
    }
}
