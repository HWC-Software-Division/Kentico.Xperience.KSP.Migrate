using CMS.ContentEngine.Internal;
using CMS.DataEngine;
using CMS.FormEngine;
using Kentico.Xperience.KSP.Migrate.Models.API;
using Kentico.Xperience.KSP.Migrate.Services;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kentico.Xperience.KSP.Migrate.Controllers.API;

[ApiController]
[Route("api/content-type-transfer")]
public class ContentTypeTransferController : ControllerBase
{
    private readonly ContentTypeExportService      _exportService;
    private readonly ContentTypeImportService      _importService;
    private readonly IReusableFieldSchemaManager   _schemaManager;

    public ContentTypeTransferController(
        ContentTypeExportService     exportService,
        ContentTypeImportService     importService,
        IReusableFieldSchemaManager  schemaManager)
    {
        _exportService = exportService;
        _importService = importService;
        _schemaManager = schemaManager;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions DeserOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ─── GET /api/content-type-transfer/list ──────────────────────────────────
    [HttpGet("list")]
    public IActionResult List()
    {
        try
        {
            var classes = DataClassInfoProvider.GetClasses()
                .WhereEquals("ClassContentTypeType", "Website")
                .OrderBy("ClassDisplayName")
                .ToList();

            return Ok(new ApiResp<List<ContentTypeResponse>>(
                classes.Select(MapToContentTypeResponse).ToList()));
        }
        catch (Exception ex)
        {
            return Ok(new ApiResp<List<ContentTypeResponse>>(ex.Message));
        }
    }

    // ─── GET /api/content-type-transfer/list-reusable ─────────────────────────
    [HttpGet("list-reusable")]
    public IActionResult ListReusable()
    {
        try
        {
            var classes = DataClassInfoProvider.GetClasses()
                .WhereEquals("ClassContentTypeType", "Reusable")
                .OrderBy("ClassDisplayName")
                .ToList();

            return Ok(new ApiResp<List<ContentTypeResponse>>(
                classes.Select(MapToContentTypeResponse).ToList()));
        }
        catch (Exception ex)
        {
            return Ok(new ApiResp<List<ContentTypeResponse>>(ex.Message));
        }
    }

    // ─── GET /api/content-type-transfer/list-field-schemas ────────────────────
    [HttpGet("list-field-schemas")]
    public IActionResult ListFieldSchemas()
    {
        try
        {
            var schemas = _schemaManager.GetAll()
                .OrderBy(s => s.DisplayName)
                .Select(s => new SchemaResponse(
                    s.Name,
                    s.DisplayName,
                    s.Description,
                    s.Guid.ToString(),
                    _schemaManager.GetSchemaFields(s.Name).Count()))
                .ToList();

            return Ok(new ApiResp<List<SchemaResponse>>(schemas));
        }
        catch (Exception ex)
        {
            return Ok(new ApiResp<List<SchemaResponse>>(ex.Message));
        }
    }

    // ─── POST /api/content-type-transfer/reusable-deps ────────────────────────
    [HttpPost("reusable-deps")]
    public IActionResult ReusableDeps([FromBody] List<string> codeNames)
    {
        try
        {
            if (codeNames == null || codeNames.Count == 0)
                return Ok(new ApiResp<List<string>>(new List<string>()));

            var reusableByGuid = DataClassInfoProvider.GetClasses()
                .WhereEquals("ClassContentTypeType", "Reusable")
                .ToList()
                .ToDictionary(c => c.ClassGUID, c => c.ClassName);

            var deps = new HashSet<string>();

            foreach (var cn in codeNames)
            {
                var classInfo = DataClassInfoProvider.GetDataClassInfo(cn);
                if (classInfo == null || string.IsNullOrEmpty(classInfo.ClassFormDefinition))
                    continue;

                var form = new FormInfo(classInfo.ClassFormDefinition);
                foreach (var field in form.GetFields(true, true))
                {
                    var allowedJson = field.Settings["AllowedContentItemTypeIdentifiers"]?.ToString();
                    if (string.IsNullOrEmpty(allowedJson)) continue;

                    try
                    {
                        var guids = JsonSerializer.Deserialize<List<string>>(allowedJson);
                        if (guids == null) continue;
                        foreach (var g in guids)
                        {
                            if (Guid.TryParse(g, out var guid) &&
                                reusableByGuid.TryGetValue(guid, out var reusableCn))
                                deps.Add(reusableCn);
                        }
                    }
                    catch { /* skip malformed JSON */ }
                }
            }

            return Ok(new ApiResp<List<string>>(deps.ToList()));
        }
        catch (Exception ex)
        {
            return Ok(new ApiResp<List<string>>(ex.Message));
        }
    }

    // ─── POST /api/content-type-transfer/export ───────────────────────────────
    [HttpPost("export")]
    public IActionResult Export([FromBody] ExportRequest req)
    {
        try
        {
            var hasContent  = req?.CodeNames?.Count > 0;
            var hasReusable = req?.ReusableCodeNames?.Count > 0;
            var hasSchemas  = req?.SchemaNames?.Count > 0;

            if (!hasContent && !hasReusable && !hasSchemas)
                return BadRequest("No code names provided.");

            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                if (hasContent)
                {
                    var dtos = _exportService.Export(req!.CodeNames);
                    WriteZipEntry(zip, "content-types.json", dtos);
                }
                if (hasReusable)
                {
                    var dtos = _exportService.Export(req!.ReusableCodeNames);
                    WriteZipEntry(zip, "reusable-fields.json", dtos);
                }
                if (hasSchemas)
                {
                    var dtos = _schemaManager.GetAll()
                        .Where(s => req!.SchemaNames.Contains(s.Name))
                        .Select(s => new ReusableFieldSchemaDto
                        {
                            Name        = s.Name,
                            DisplayName = s.DisplayName,
                            Description = s.Description,
                            Guid        = s.Guid.ToString(),
                            Fields      = _schemaManager.GetSchemaFields(s.Name)
                                            .Select(f => _exportService.MapFieldToDto(f))
                                            .ToList()
                        })
                        .ToList();
                    WriteZipEntry(zip, "reusable-field-schemas.json", dtos);
                }
            }

            var date = DateTime.Now.ToString("yyyyMMdd_HHmm");
            return File(ms.ToArray(), "application/zip", $"export_content_types_{date}.zip");
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

            List<ContentTypeDto>         contentDtos  = new();
            List<ContentTypeDto>         reusableDtos = new();
            List<ReusableFieldSchemaDto> schemaDtos   = new();

            using (var ms = new MemoryStream())
            {
                file.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
                using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

                var ctEntry = zip.GetEntry("content-types.json");
                if (ctEntry != null)
                {
                    using var r = new StreamReader(ctEntry.Open());
                    contentDtos = JsonSerializer.Deserialize<List<ContentTypeDto>>(r.ReadToEnd(), DeserOpts) ?? new();
                }

                var rfEntry = zip.GetEntry("reusable-fields.json");
                if (rfEntry != null)
                {
                    using var r = new StreamReader(rfEntry.Open());
                    reusableDtos = JsonSerializer.Deserialize<List<ContentTypeDto>>(r.ReadToEnd(), DeserOpts) ?? new();
                }

                var schemaEntry = zip.GetEntry("reusable-field-schemas.json");
                if (schemaEntry != null)
                {
                    using var r = new StreamReader(schemaEntry.Open());
                    schemaDtos = JsonSerializer.Deserialize<List<ReusableFieldSchemaDto>>(r.ReadToEnd(), DeserOpts) ?? new();
                }
            }

            if (contentDtos.Count == 0 && reusableDtos.Count == 0 && schemaDtos.Count == 0)
                return Ok(new ApiResp<ImportResult>("No content found in zip file."));

            // 1. Import Field Schemas first (may be depended on by content types)
            var (sCreated, sUpdated, sErrors, sCreatedNames, sUpdatedNames) = ImportSchemasBatch(schemaDtos);

            // 2. Import Reusable Content Types (dependencies of website content types)
            var (rCreated, rUpdated, rErrors, rCreatedNames, rUpdatedNames) = ImportBatch(reusableDtos);

            // 3. Import Website Content Types
            var (created, updated, errors, createdNames, updatedNames) = ImportBatch(contentDtos);

            return Ok(new ApiResp<ImportResult>(new ImportResult(
                created, updated, errors, createdNames, updatedNames,
                rCreated, rUpdated, rErrors, rCreatedNames, rUpdatedNames,
                sCreated, sUpdated, sErrors, sCreatedNames, sUpdatedNames)));
        }
        catch (Exception ex)
        {
            return Ok(new ApiResp<ImportResult>(ex.Message));
        }
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private (int created, int updated, List<string> errors,
             List<string> createdNames, List<string> updatedNames)
        ImportBatch(List<ContentTypeDto> dtos)
    {
        int created = 0, updated = 0;
        var errors       = new List<string>();
        var createdNames = new List<string>();
        var updatedNames = new List<string>();

        foreach (var dto in dtos)
        {
            var (message, codeName, _) = _importService.Import(dto);
            if (message == "Created")      { created++; createdNames.Add(dto.Name); }
            else if (message == "Updated") { updated++; updatedNames.Add(dto.Name); }
            else                           errors.Add($"{codeName}: {message}");
        }

        return (created, updated, errors, createdNames, updatedNames);
    }

    private (int created, int updated, List<string> errors,
             List<string> createdNames, List<string> updatedNames)
        ImportSchemasBatch(List<ReusableFieldSchemaDto> dtos)
    {
        int created = 0, updated = 0;
        var errors       = new List<string>();
        var createdNames = new List<string>();
        var updatedNames = new List<string>();

        foreach (var dto in dtos)
        {
            try
            {
                var existing = _schemaManager.Get(dto.Name);
                if (existing == null)
                {
                    // Create new schema
                    _schemaManager.CreateSchema(new CreateReusableFieldSchemaParameters(
                        dto.Name,
                        dto.DisplayName,
                        dto.Description ?? ""));

                    foreach (var f in dto.Fields)
                    {
                        try { _schemaManager.AddField(dto.Name, _importService.BuildFormFieldInfo(f)); }
                        catch { /* skip individual field errors */ }
                    }

                    created++;
                    createdNames.Add(dto.DisplayName);
                }
                else
                {
                    // Update existing schema metadata
                    _schemaManager.UpdateSchema(dto.Name, new EditReusableFieldSchemaParameters(
                        dto.Name,
                        dto.DisplayName,
                        dto.Description ?? ""));

                    // Reconcile fields: add new, update existing
                    var currentFieldNames = _schemaManager.GetSchemaFields(dto.Name)
                        .Select(f => f.Name)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    foreach (var f in dto.Fields)
                    {
                        try
                        {
                            var fi = _importService.BuildFormFieldInfo(f);
                            if (currentFieldNames.Contains(f.Name))
                                _schemaManager.UpdateField(dto.Name, f.Name, fi);
                            else
                                _schemaManager.AddField(dto.Name, fi);
                        }
                        catch { /* skip individual field errors */ }
                    }

                    updated++;
                    updatedNames.Add(dto.DisplayName);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{dto.Name}: {ex.Message}");
            }
        }

        return (created, updated, errors, createdNames, updatedNames);
    }

    private static void WriteZipEntry(ZipArchive zip, string entryName, object data)
    {
        var json  = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(json);
    }

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

public record SchemaResponse(
    string  Name,
    string  DisplayName,
    string? Description,
    string  Guid,
    int     FieldCount);

public record FieldResponse(
    string    Name, string DataType, bool IsRequired, int Size,
    string?   DefaultValue, string FieldType, string? Caption,
    string?   DataSource, int? MinItems, int? MaxItems,
    string[]? AllowedContentTypes, bool Visible);

public record ImportResult(
    int Created, int Updated, List<string> Errors,
    List<string> CreatedNames, List<string> UpdatedNames,
    int ReusableCreated, int ReusableUpdated, List<string> ReusableErrors,
    List<string> ReusableCreatedNames, List<string> ReusableUpdatedNames,
    int SchemaCreated, int SchemaUpdated, List<string> SchemaErrors,
    List<string> SchemaCreatedNames, List<string> SchemaUpdatedNames);

public class ExportRequest
{
    public List<string> CodeNames         { get; set; } = new();
    public List<string> ReusableCodeNames { get; set; } = new();
    public List<string> SchemaNames       { get; set; } = new();
}
