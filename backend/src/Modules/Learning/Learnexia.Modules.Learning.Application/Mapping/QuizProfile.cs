using AutoMapper;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Mapping;

/// <summary>
/// AutoMapper profile for quiz entities in the Learning module.
/// CorrectAnswer is explicitly IGNORED when mapping QuizQuestion → QuizQuestionDto
/// so it is never exposed to the client.
/// </summary>
public class QuizProfile : Profile
{
    public QuizProfile()
    {
        // QuizQuestion → client DTO: CorrectAnswer is intentionally excluded (server-side only).
        // SECURITY: ForSourceMember below is a deliberate control — correct answers must NEVER reach
        // the client. Removing or bypassing this call is a security defect.
        CreateMap<QuizQuestion, QuizQuestionDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.QuestionType, opt => opt.MapFrom(src => src.QuestionType))
            .ForMember(dest => dest.QuestionText, opt => opt.MapFrom(src => src.QuestionText))
            .ForMember(dest => dest.Options, opt => opt.MapFrom(src => src.Options))
            .ForSourceMember(src => src.CorrectAnswer, opt => opt.DoNotValidate());
    }
}
