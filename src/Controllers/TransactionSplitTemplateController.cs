using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Application.Services;

namespace YoFi.V3.Controllers;

/// <summary>Provides the reusable XLSX template for uploading transaction splits.</summary>
/// <param name="workbookService">Service that creates transaction split workbooks.</param>
/// <param name="logger">Logger for diagnostic output.</param>
[Authorize]
[Route("api/transactions/splits/upload-template")]
[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public partial class TransactionSplitTemplateController(
    TransactionSplitWorkbookService workbookService,
    ILogger<TransactionSplitTemplateController> logger) : ControllerBase
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Downloads the tenant-independent XLSX template for uploading transaction splits.</summary>
    /// <returns>An XLSX workbook attachment.</returns>
    [HttpGet]
    [Produces(XlsxContentType)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, XlsxContentType)]
    public IActionResult GetSplitUploadTemplate()
    {
        LogStarting();
        var result = workbookService.CreateTemplate();
        LogOk();
        return File(result.Content, XlsxContentType, result.FileName);
    }

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK")]
    private partial void LogOk([CallerMemberName] string? location = null);
}
