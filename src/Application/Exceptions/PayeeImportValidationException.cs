using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Exceptions;

namespace YoFi.V3.Application.Exceptions;

/// <summary>A workbook import cannot proceed because it failed detection or validation.</summary>
public sealed class PayeeImportValidationException : ValidationException
{
    /// <summary>Gets the complete preview, including row-level errors for display or download.</summary>
    public PayeeImportPreviewDto Preview { get; }

    /// <summary>Initializes the exception with the analysis that prevented persistence.</summary>
    public PayeeImportValidationException(PayeeImportPreviewDto preview)
        : base(nameof(preview), "The Payee worksheet is missing or contains validation errors.")
    {
        Preview = preview;
    }
}
