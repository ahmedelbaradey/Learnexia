using AutoMapper;
using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Dtos;
using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Queries.GetCurriculumDocument;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Infrastructure.Features.CurriculumDocuments.Queries;

/// <summary>
/// Handles <see cref="GetCurriculumDocumentQuery"/> — returns the full detail of a
/// single curriculum document by id (BL-01 BE-7).
///
/// <para>Returns 404 when the document is not found.</para>
///
/// <para><strong>Security note for security-auditor:</strong> The <c>ObjectKey</c> field is
/// included in <see cref="CurriculumDocumentDto"/> because this endpoint is class-level
/// <c>AdminOnly</c>-gated. If the endpoint is ever widened, replace <c>ObjectKey</c> with
/// a presigned URL built via <c>IStorageService.GetPreviewUrlAsync</c>.</para>
///
/// <para>Queries skip ValidationBehavior — this handler guards inputs itself.</para>
/// </summary>
public sealed class GetCurriculumDocumentQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetCurriculumDocumentQuery, BaseResponse<CurriculumDocumentDto>>
{
    private readonly CurriculumDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetCurriculumDocumentQueryHandler(
        CurriculumDbContext db,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _db        = db;
        _mapper    = mapper;
        _logger    = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<CurriculumDocumentDto>> Handle(
        GetCurriculumDocumentQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.Id <= 0)
                return NotFound<CurriculumDocumentDto>(
                    _localizer[SharedResourcesKey.CurriculumDocumentNotFound]);

            var document = await _db.CurriculumDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

            if (document is null)
                return NotFound<CurriculumDocumentDto>(
                    _localizer[SharedResourcesKey.CurriculumDocumentNotFound]);

            var dto      = _mapper.Map<CurriculumDocumentDto>(document);
            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.CurriculumDocumentRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetCurriculumDocumentQuery");
            return ServerError<CurriculumDocumentDto>(
                _localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
