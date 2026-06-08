using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Dtos;

/// <summary>
/// Input shape for editing an existing content block.
/// Only Payload and BlockType may be changed via this command.
/// SequenceOrder is controlled by ReorderContentBlocksCommand.
/// IsActive is controlled by the block's IsActive toggle (future; for now deletion via soft-delete).
/// IsDeleted is controlled by DeleteContentBlockCommand.
/// </summary>
public record EditContentBlockDto
{
    public int Id { get; init; }
    public ContentBlockType BlockType { get; init; }
    public string Payload { get; init; } = null!;
}
