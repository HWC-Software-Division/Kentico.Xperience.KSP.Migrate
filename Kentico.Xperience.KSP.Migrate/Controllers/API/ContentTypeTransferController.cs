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
    private readonly ContentTypeExportService     _exportService;
    private readonly ContentTypeImportService     _importService;
    private readonly IReusableFieldSchemaManager  _schemaManager;

    public ContentTypeTransferController(
        ContentTypeExportService    exportService,
        ContentTypeImportService    importService,
        IReusableFieldSchemaManager schemaManager)
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

    // ─── GET /list ────────────────────────────────────────────────────────────
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
        catch (Exception ex) { return Ok(new ApiResp<List<ContentTypeResponse>>(ex.Message)); }
    }

    // ─── GET /list-reusable ───────────────────────────────────────────────────
    // #5: filter out Legacy.* by default (migration artifacts); pass ?includeLegacy=true to see all
    [HttpGet("list-reusable")]
    public IActionResult ListReusable([FromQuery] bool includeLegacy = false)
    {
        try
        {
            var classes = DataClassInfoProvider.GetClasses()
                .WhereEquals("ClassContentTypeType", "Reusable")
                .OrderBy("ClassDisplayName")
                .ToList();

            if (!includeLegacy)
                classes = classes
                    .Where(c => !c.ClassName.StartsWith("Legacy.", StringComparison.OrdinalIgnoreCase))
                    .ToList();

            return Ok(new ApiResp<List<ContentTypeResponse>>(
                classes.Select(MapToContentTypeResponse).ToList()));
        }
        catch (Exception ex) { return Ok(new ApiResp<List<ContentTypeResponse>>(ex.Message)); }
    }

    // ─── GET /list-field-schemas ──────────────────────────────────────────────
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
        catch (Exception ex) { return Ok(new ApiResp<List<SchemaResponse>>(ex.Message)); }
    }

    // ─── POST /reusable-deps ──────────────────────────────────────────────────
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
                .Where(c => !c.ClassName.StartsWith("Legacy.", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(c => c.ClassGUID, c => c.ClassName);

            var deps = new HashSet<string>();
            foreach (var cn in codeNames)
            {
                var classInfo = DataClassInfoProvider.GetDataClassInfo(cn);
                if (classInfo == null || string.IsNullOrEmpty(classInfo.ClassFormDefinition)) continue;

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
                            if (Guid.TryParse(g, out var guid) && reusableByGuid.TryGetValue(guid, out var reusableCn))
                                deps.Add(reusableCn);
                    }
                    catch { }
                }
            }

            return Ok(new ApiResp<List<string>>(deps.ToList()));
        }
        catch (Exception ex) { return Ok(new ApiResp<List<string>>(ex.Message)); }
    }

    // ─── POST /schema-deps ────────────────────────────────────────────────────
    // #3: detect which Field Schemas the selected Content Types depend on
    [HttpPost("schema-deps")]
    public IActionResult SchemaDeps([FromBody] List<string> codeNames)
    {
        try
        {
            if (codeNames == null || codeNames.Count == 0)
                return Ok(new ApiResp<List<SchemaDepResponse>>(new List<SchemaDepResponse>()));

            var deps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cn in codeNames)
            {
                var schemas = _schemaManager.GetSchemasForContentType(cn);
                foreach (var s in schemas)
                    deps[s.Name] = s.DisplayName;
            }

            return Ok(new ApiResp<List<SchemaDepResponse>>(
                deps.Select(kvp => new SchemaDepResponse(kvp.Key, kvp.Value)).ToList()));
        }
        catch (Exception ex) { return Ok(new ApiResp<List<SchemaDepResponse>>(ex.Message)); }
    }

    // ─── POST /export ─────────────────────────────────────────────────────────
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
                    // Build a GUID→codeName lookup so ReusableSchemas stores code names,
                    // not GUIDs (GUIDs differ per environment and break cross-env import).
                    var schemaByGuid = _schemaManager.GetAll()
                        .ToDictionary(s => s.Guid.ToString(), s => s.Name, StringComparer.OrdinalIgnoreCase);

                    var contentDtos = _exportService.Export(req!.CodeNames);
                    foreach (var dto in contentDtos)
                    {
                        if (dto.ReusableSchemas != null)
                            dto.ReusableSchemas = dto.ReusableSchemas
                                .Select(g => schemaByGuid.TryGetValue(g, out var name) ? name : g)
                                .ToList();
                    }
                    WriteZipEntry(zip, "content-types.json", contentDtos);
                }

                if (hasReusable)
                    WriteZipEntry(zip, "reusable-fields.json", _exportService.Export(req!.ReusableCodeNames));

                if (hasSchemas)
                {
                    var dtos = _schemaManager.GetAll()
                        .Where(s => req!.SchemaNames.Contains(s.Name))
                        .Select(s =>
                        {
                            var schemaFields = _schemaManager.GetSchemaFields(s.Name).ToList();

                            // Build a temporary FormInfo from the schema fields so GetVisibility
                            // can parse visibility conditions out of each field's XML data.
                            var schemaForm = new FormInfo();
                            foreach (var sf in schemaFields)
                                schemaForm.AddFormItem(sf);

                            return new ReusableFieldSchemaDto
                            {
                                Name        = s.Name,
                                DisplayName = s.DisplayName,
                                Description = s.Description,
                                Guid        = s.Guid.ToString(),
                                Fields      = schemaFields
                                                .Select(f => _exportService.MapFieldToDto(f, schemaForm))
                                                .ToList()
                            };
                        })
                        .ToList();
                    WriteZipEntry(zip, "reusable-field-schemas.json", dtos);
                }
            }

            var date = DateTime.Now.ToString("yyyyMMdd_HHmm");
            return File(ms.ToArray(), "application/zip", $"export_content_types_{date}.zip");
        }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    // ─── POST /import ─────────────────────────────────────────────────────────
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

            // 1. Field Schemas first
            var (sCreated, sUpdated, sErrors, sCreatedNames, sUpdatedNames) = ImportSchemasBatch(schemaDtos);

            // 2. Reusable Content Types
            var (rCreated, rUpdated, rErrors, rCreatedNames, rUpdatedNames, rWarnings) = ImportBatch(reusableDtos);

            // 3. Website Content Types
            var (created, updated, errors, createdNames, updatedNames, ctWarnings) = ImportBatch(contentDtos);

            var allWarnings = rWarnings.Concat(ctWarnings).ToList();

            return Ok(new ApiResp<ImportResult>(new ImportResult(
                created, updated, errors, createdNames, updatedNames,
                rCreated, rUpdated, rErrors, rCreatedNames, rUpdatedNames,
                sCreated, sUpdated, sErrors, sCreatedNames, sUpdatedNames,
                allWarnings)));
        }
        catch (Exception ex) { return Ok(new ApiResp<ImportResult>(ex.Message)); }
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    // #6: collect warnings from ImportService (missing AllowedContentTypes)
    private (int created, int updated, List<string> errors,
             List<string> createdNames, List<string> updatedNames,
             List<string> warnings)
        ImportBatch(List<ContentTypeDto> dtos)
    {
        int created = 0, updated = 0;
        var errors       = new List<string>();
        var createdNames = new List<string>();
        var updatedNames = new List<string>();
        var warnings     = new List<string>();

        foreach (var dto in dtos)
        {
            var (message, codeName, _, importWarnings) = _importService.Import(dto);
            warnings.AddRange(importWarnings.Select(w => $"[{codeName}] {w}"));

            if (message == "Created")      { created++; createdNames.Add(dto.Name); }
            else if (message == "Updated") { updated++; updatedNames.Add(dto.Name); }
            else                           errors.Add($"{codeName}: {message}");

            // Attach reusable field schema references to the content type's form definition
            if ((message == "Created" || message == "Updated") && dto.ReusableSchemas?.Any() == true)
            {
                try
                {
                    var classInfo = DataClassInfoProvider.GetDataClassInfo(codeName);
                    if (classInfo != null)
                    {
                        var formInfo = new FormInfo(classInfo.ClassFormDefinition);
                        var changed  = false;

                        foreach (var schemaRef in dto.ReusableSchemas)
                        {
                            // schemaRef may be a GUID (as stored in form XML) or a code name
                            var schema = _schemaManager.GetAll().FirstOrDefault(s =>
                                s.Name.Equals(schemaRef, StringComparison.OrdinalIgnoreCase) ||
                                s.Guid.ToString().Equals(schemaRef, StringComparison.OrdinalIgnoreCase));

                            if (schema == null)
                            {
                                warnings.Add($"[{codeName}] Reusable schema \"{schemaRef}\" not found in target — skipped.");
                                continue;
                            }

                            // Skip if already attached (check by GUID, which is how Kentico stores it)
                            var guidStr = schema.Guid.ToString();
                            if (formInfo.GetFormSchema(guidStr) != null) continue;

                            var schemaInfo = new FormSchemaInfo
                            {
                                Name = guidStr,   // Kentico stores GUID as the name in form XML
                                Guid = schema.Guid
                            };
                            formInfo.AddFormItem(schemaInfo);
                            changed = true;
                        }

                        if (changed)
                        {
                            classInfo.ClassFormDefinition = formInfo.GetXmlDefinition();
                            DataClassInfoProvider.SetDataClassInfo(classInfo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"[{codeName}] Failed to attach reusable schemas: {ex.Message}");
                }
            }
        }

        return (created, updated, errors, createdNames, updatedNames, warnings);
    }

    // #2: delete fields that exist in target but are absent from the exported schema
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
                    _schemaManager.CreateSchema(new CreateReusableFieldSchemaParameters(
                        dto.Name, dto.DisplayName, dto.Description ?? ""));

                    foreach (var f in dto.Fields)
                        try { _schemaManager.AddField(dto.Name, _importService.BuildFormFieldInfo(f)); }
                        catch { }

                    created++;
                    createdNames.Add(dto.DisplayName);
                }
                else
                {
                    _schemaManager.UpdateSchema(dto.Name, new EditReusableFieldSchemaParameters(
                        dto.Name, dto.DisplayName, dto.Description ?? ""));

                    var currentFields    = _schemaManager.GetSchemaFields(dto.Name).ToList();
                    var currentFieldNames = currentFields.Select(f => f.Name)
                                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var dtoFieldNames    = dto.Fields.Select(f => f.Name)
                                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    // Add new / update existing
                    foreach (var f in dto.Fields)
                        try
                        {
                            var fi = _importService.BuildFormFieldInfo(f);
                            if (currentFieldNames.Contains(f.Name))
                                _schemaManager.UpdateField(dto.Name, f.Name, fi);
                            else
                                _schemaManager.AddField(dto.Name, fi);
                        }
                        catch { }

                    // #2 Delete fields that are no longer in the exported schema
                    foreach (var orphan in currentFields.Where(f => !dtoFieldNames.Contains(f.Name)))
                        try { _schemaManager.DeleteField(dto.Name, orphan.Name); }
                        catch { }

                    updated++;
                    updatedNames.Add(dto.DisplayName);
                }

                // Second pass: apply visibility conditions after ALL fields are in the schema.
                // Build a temp FormInfo from the current schema fields, apply visibility via XML,
                // then write each modified field back using UpdateField.
                var fieldsWithVisibility = dto.Fields.Where(f => f.Visibility != null).ToList();
                if (fieldsWithVisibility.Any())
                {
                    try
                    {
                        var schemaFields = _schemaManager.GetSchemaFields(dto.Name).ToList();
                        var tempForm = new FormInfo();
                        foreach (var sf in schemaFields) tempForm.AddFormItem(sf);

                        foreach (var f in fieldsWithVisibility)
                            tempForm = _importService.ApplyVisibility(tempForm, f.Name, f.Visibility);

                        foreach (var f in fieldsWithVisibility)
                        {
                            var updatedField = tempForm.GetFormField(f.Name);
                            if (updatedField != null)
                                try { _schemaManager.UpdateField(dto.Name, f.Name, updatedField); }
                                catch { }
                        }
                    }
                    catch { }
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
        return new ContentTypeResponse(c.ClassDisplayName, c.ClassName,
            fields.Select(MapFieldResponse).ToList());
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
        return fi.ItemsList.OfType<FormFieldInfo>().Where(f => !f.System).ToList();
    }

    private static int? TryParseInt(object? v) =>
        v != null && int.TryParse(v.ToString(), out var n) ? n : null;
}

// ─── records / DTOs ───────────────────────────────────────────────────────────

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
    string  Name, string DisplayName, string? Description,
    string  Guid, int    FieldCount);

public record SchemaDepResponse(string Name, string DisplayName);

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
    List<string> SchemaCreatedNames, List<string> SchemaUpdatedNames,
    List<string> Warnings);

public class ExportRequest
{
    public List<string> CodeNames         { get; set; } = new();
    public List<string> ReusableCodeNames { get; set; } = new();
    public List<string> SchemaNames       { get; set; } = new();
}
