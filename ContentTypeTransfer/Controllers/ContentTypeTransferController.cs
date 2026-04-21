using ContentTypeTransfer.Models;
using ContentTypeTransfer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ContentTypeTransfer.Controllers;

/// <summary>
/// Internal REST API consumed by the React admin UI pages.
/// Prefix: /api/content-type-transfer/
/// </summary>
[ApiController]
[Route("api/content-type-transfer")]
public class ContentTypeTransferController : ControllerBase
{
    private readonly IContentTypeService _svc;
    private readonly ILogger<ContentTypeTransferController> _logger;

    public ContentTypeTransferController(
        IContentTypeService svc,
        ILogger<ContentTypeTransferController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    // ── GET /api/content-type-transfer/list ──────────────────────────────────

    /// <summary>
    /// Returns all content types fetched from the external API.
    /// Used by all 3 admin pages to populate the table.
    /// </summary>
    [HttpGet("list")]
    [ProducesResponseType<ApiResponse<List<ContentTypeDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetList(CancellationToken ct)
    {
        try
        {
            var items = await _svc.GetAllAsync(ct);
            return Ok(new ApiResponse<List<ContentTypeDto>>
            {
                Success = true,
                Data = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetList failed.");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Failed to load content types. Check API connection."
            });
        }
    }

    // ── POST /api/content-type-transfer/export ───────────────────────────────

    /// <summary>
    /// Exports selected content types as a downloadable .zip file.
    /// Body: { codeNames: string[] }  (empty = export all)
    /// </summary>
    [HttpPost("export")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FileResult))]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Export([FromBody] ExportRequest request, CancellationToken ct)
    {
        try
        {
            var stream = await _svc.ExportToZipAsync(request.CodeNames, ct);
            var fileName = $"content-types-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
            return File(stream, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed.");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Export failed. Check API connection."
            });
        }
    }

    // ── POST /api/content-type-transfer/import ───────────────────────────────

    /// <summary>
    /// Accepts a .zip upload, reads content-types.json inside,
    /// then calls the external API to create/update each type.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB max
    [ProducesResponseType<ApiResponse<ImportResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Error = "No file uploaded."
            });

        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Error = "Only .zip files are accepted."
            });

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _svc.ImportFromZipAsync(stream, ct);

            return Ok(new ApiResponse<ImportResultDto>
            {
                Success = result.Success,
                Data = new ImportResultDto(result.Created, result.Updated, result.Errors),
                Error = result.Errors.Count > 0
                    ? $"{result.Errors.Count} error(s) occurred during import."
                    : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed for file {FileName}.", file.FileName);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = "Import failed unexpectedly."
            });
        }
    }
}

/// <summary>DTO returned in the import response body.</summary>
public record ImportResultDto(int Created, int Updated, List<string> Errors);
