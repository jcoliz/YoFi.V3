using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Exceptions;

namespace YoFi.V3.Application.Exceptions;

/// <summary>A workbook import cannot proceed because detection or worksheet validation failed.</summary>
public sealed class TenantDataImportValidationException : ValidationException
{
    /// <summary>Gets the complete format-neutral preview containing row-level issues.</summary>
    public TenantDataImportPreviewDto Preview { get; }

    /// <summary>Initializes the exception with the analysis that prevented persistence.</summary>
    public TenantDataImportValidationException(TenantDataImportPreviewDto preview)
        : base("file", "The workbook format is unsupported or one or more worksheets contain validation errors.")
    {
        Preview = preview;
    }
}
