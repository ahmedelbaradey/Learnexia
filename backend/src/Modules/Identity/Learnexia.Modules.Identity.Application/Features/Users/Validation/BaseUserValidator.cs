using System.Text.RegularExpressions;
using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Users.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Validation;

public class BaseUserValidator : AbstractValidator<BaseUserDto>
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public BaseUserValidator(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;
        ApplyValidationRules();
    }

    private void ApplyValidationRules()
    {
    }

    private bool BeValidIBAN(string iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            return true;

        var ibanPattern = @"^[A-Z]{2}[0-9]{2}[A-Z0-9]+$";
        return Regex.IsMatch(iban.Replace(" ", "").ToUpper(), ibanPattern);
    }

    private bool BeValidCVFile(IFormFile file)
    {
        if (file == null) return true;

        var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
        var maxSizeInBytes = 10 * 1024 * 1024;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        return allowedExtensions.Contains(extension) && file.Length <= maxSizeInBytes;
    }

    private bool BeValidPhotoFile(IFormFile file)
    {
        if (file == null) return true;

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
        var maxSizeInBytes = 2 * 1024 * 1024;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        return allowedExtensions.Contains(extension) && file.Length <= maxSizeInBytes;
    }
}
