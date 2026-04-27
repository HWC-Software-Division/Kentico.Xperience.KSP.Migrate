using CMS.DataEngine;
using CMS.FormEngine;
using Kentico.Xperience.KSP.Migrate.Models.API;
using Kentico.Xperience.KSP.Migrate.Services;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using static HotChocolate.ErrorCodes;

namespace Kentico.Xperience.KSP.Migrate.Controllers.API;

[ApiController]
[Route("api/content-type-transfer")]
public class ContentTypeTransferController : ControllerBase
{
    private readonly ContentTypeExportService _exportService;
    private readonly ContentTypeImportService _importService;

    public ContentTypeTransferController(
        ContentTypeExportService exportService,
        ContentTypeImportService importService)
    {
        _exportService = exportService;
        _importService = importService;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ─── GET /api/content-type-transfer/list ─────────────────────────────────
    [HttpGet("list")]
    public IActionResult List()
    {
        try
        {
            var classes = DataClassInfoProvider.GetClasses()
                .WhereNotEmpty("ClassContentTypeType")
                .OrderBy("ClassDisplayName")
                .ToList();

            var result = classes.Select(MapToContentTypeResponse).ToList();
            return Ok(new ApiResp<List<ContentTypeResponse>>(result));
        }
        catch (Exception ex)
        {
            return Ok(new ApiResp<List<ContentTypeResponse>>(ex.Message));
        }
    }

    // ─── POST /api/content-type-transfer/export ───────────────────────────────
    [HttpPost("export")]
    public IActionResult Export([FromBody] ExportRequest req)
    {
        try
        {
            if (req?.CodeNames == null || req.CodeNames.Count == 0)
                return BadRequest("No code names provided.");

            var dtos = _exportService.Export(req.CodeNames);
            var json = System.Text.Json.JsonSerializer.Serialize(dtos, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            var jsonBytes = CreateJsonFile(json);

            var date = DateTime.Now.ToString("yyyyMMdd_HHmm");
            var fileName = $"export_content_types_{date}.zip";

            return File(jsonBytes, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    // ─── POST /api/content-type-transfer/import ───────────────────────────────
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public IActionResult Import(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return Ok(new ApiResp<ImportResult>("No file uploaded."));

            List<ContentTypeDto> dtos;
            using (var ms = new MemoryStream())
            {
                file.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
                using var zip   = new ZipArchive(ms, ZipArchiveMode.Read);
                var entry = zip.GetEntry("content-types.json")
                    ?? throw new InvalidOperationException("content-types.json not found in zip.");
                using var reader = new StreamReader(entry.Open());
                var json = reader.ReadToEnd();
                dtos = JsonSerializer.Deserialize<List<ContentTypeDto>>(json,
                           new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                       ?? new();
            }

            int created = 0, updated = 0;
            var errors  = new List<string>();

            foreach (var dto in dtos)
            {
                var (message, codeName, _) = _importService.Import(dto);
                if (message == "Created")      created++;
                else if (message == "Updated") updated++;
                else                           errors.Add($"{codeName}: {message}");
            }

            return Ok(new ApiResp<ImportResult>(new ImportResult(created, updated, errors)));
        }
        catch (Exception ex)
        {
            return Ok(new ApiResp<ImportResult>(ex.Message));
        }
    }

    // ─── helpers (List only) ──────────────────────────────────────────────────

    private static ContentTypeResponse MapToContentTypeResponse(DataClassInfo c)
    {
        var fields = GetFields(c);
        return new ContentTypeResponse(
            c.ClassDisplayName,
            c.ClassName,
            fields.Select(MapFieldResponse).ToList()
        );
    }

    private static FieldResponse MapFieldResponse(FormFieldInfo f) => new(
        Name:                f.Name,
        DataType:            f.DataType,
        IsRequired:          !f.AllowEmpty,
        Size:                f.Size,
        DefaultValue:        f.DefaultValue,
        FieldType:           f.Settings["controlname"]?.ToString() ?? "",
        Caption:             f.Caption,
        DataSource:          f.Settings["Options"]?.ToString(),
        MinItems:            TryParseInt(f.Settings["MinimumPages"]),
        MaxItems:            TryParseInt(f.Settings["MaximumPages"]),
        AllowedContentTypes: null,
        Visible:             f.Visible
    );

    private static List<FormFieldInfo> GetFields(DataClassInfo c)
    {
        if (string.IsNullOrEmpty(c.ClassFormDefinition)) return new();
        var fi = new FormInfo(c.ClassFormDefinition);
        return fi.ItemsList.OfType<FormFieldInfo>()
                 .Where(f => !f.System)
                 .ToList();
    }

    private static int? TryParseInt(object? v) =>
        v != null && int.TryParse(v.ToString(), out var n) ? n : null;

    private byte[] CreateJsonFile(string json)
    {
        using (var memoryStream = new MemoryStream())
        {
            using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
            { 
                var entry = archive.CreateEntry("content-types.json");

                using (var entryStream = entry.Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    writer.Write(json);
                }
            }

            return memoryStream.ToArray();
        }
    }
}

// ─── DTOs / responses ─────────────────────────────────────────────────────────

public record ApiResp<T>
{
    public bool    Success { get; init; }
    public T?      Data    { get; init; }
    public string? Error   { get; init; }

    public ApiResp(T data)       { Success = true;  Data  = data; }
    public ApiResp(string error) { Success = false; Error = error; }
}

public record ContentTypeResponse(string Name, string CodeName, List<FieldResponse> Fields);

public record FieldResponse(
    string    Name, string DataType, bool IsRequired, int Size,
    string?   DefaultValue, string FieldType, string? Caption,
    string?   DataSource, int? MinItems, int? MaxItems,
    string[]? AllowedContentTypes, bool Visible);

public record ImportResult(int Created, int Updated, List<string> Errors);

public class ExportRequest
{
    public List<string> CodeNames { get; set; } = new();
}
