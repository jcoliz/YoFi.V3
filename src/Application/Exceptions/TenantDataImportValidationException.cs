using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Exceptions;

namespace YoFi.V3.Application.Exceptions;

/// <summary>A workbook import cannot proceed because detection or worksheet validation failed.</summary>
public sealed class TenantDataImportValidationException : ValidationException
{
    /// <summary>Gets the complete format-neutral preview containing row-level issues.</summary>
    /// <remarks>
    /// This preview is not currently returned to the caller.
    /// TODO: Find a way to return this preview to the caller, or stop including it in the exception.
    /// </remarks>
    public TenantDataImportPreviewDto Preview { get; }

    /// <summary>Initializes the exception with the analysis that prevented persistence.</summary>
    public TenantDataImportValidationException(TenantDataImportPreviewDto preview)
        : base("file", "The workbook format is unsupported or one or more worksheets contain validation errors. Review the workbook preview for validation details, then try importing again.")
    {
        Title = "Import could not be completed.";
        Preview = preview;
    }
}
