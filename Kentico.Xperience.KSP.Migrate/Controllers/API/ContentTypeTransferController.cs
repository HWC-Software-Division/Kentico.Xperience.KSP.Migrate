using CMS.DataEngine;
using CMS.FormEngine;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kentico.Xperience.KSP.Migrate.Controllers.API;

[ApiController]
[Route("api/content-type-transfer")]
public class ContentTypeTransferController : ControllerBase
{
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

            var dtos = req.CodeNames
                .Select(cn => DataClassInfoProvider.GetDataClassInfo(cn))
                .Where(c => c != null)
                .Select(MapToExportDto!)
                .ToList();

            var json = JsonSerializer.Serialize(dtos, JsonOpts);

            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry("content-types.json", CompressionLevel.Fastest);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(json);
            }

            return File(ms.ToArray(), "application/zip",
                $"content-types-{DateTime.UtcNow:yyyy-MM-dd}.zip");
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

            List<ExportDto> dtos;
            using (var ms = new MemoryStream())
            {
                file.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
                using var zip   = new ZipArchive(ms, ZipArchiveMode.Read);
                var entry = zip.GetEntry("content-types.json")
                    ?? throw new InvalidOperationException("content-types.json not found in zip.");
                using var reader = new StreamReader(entry.Open());
                var json = reader.ReadToEnd();
                dtos = JsonSerializer.Deserialize<List<ExportDto>>(json,
                           new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                       ?? new();
            }

            int created = 0, updated = 0;
            var errors  = new List<string>();

            foreach (var dto in dtos)
            {
                try   { ApplyDto(dto, ref created, ref updated); }
                catch (Exception ex) { errors.Add($"{dto.CodeName}: {ex.Message}"); }
            }

            return Ok(new ApiResp<ImportResult>(new ImportResult(created, updated, errors)));
        }
        catch (Exception ex)
        {
            return Ok(new ApiResp<ImportResult>(ex.Message));
        }
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

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

    private static ExportDto MapToExportDto(DataClassInfo c)
    {
        var fields = GetFields(c);
        return new ExportDto
        {
            Name     = c.ClassDisplayName,
            CodeName = c.ClassName,
            Fields   = fields.Select(f => new ExportFieldDto
            {
                Name         = f.Name,
                DataType     = f.DataType,
                IsRequired   = !f.AllowEmpty,
                Size         = f.Size > 0 ? f.Size : null,
                DefaultValue = f.DefaultValue,
                FieldType    = MapControlToFieldType(f.Settings["controlname"]?.ToString()),
                Caption      = f.Caption,
                DataSource   = f.Settings["Options"]?.ToString(),
                MinItems     = TryParseInt(f.Settings["MinimumPages"]),
                MaxItems     = TryParseInt(f.Settings["MaximumPages"]),
            }).ToList()
        };
    }

    private static List<FormFieldInfo> GetFields(DataClassInfo c)
    {
        if (string.IsNullOrEmpty(c.ClassFormDefinition)) return new();
        var fi = new FormInfo(c.ClassFormDefinition);
        return fi.ItemsList.OfType<FormFieldInfo>()
                 .Where(f => !f.System)
                 .ToList();
    }

    private static void ApplyDto(ExportDto dto, ref int created, ref int updated)
    {
        var existing    = DataClassInfoProvider.GetDataClassInfo(dto.CodeName);
        var isNew       = existing == null;
        var contentType = existing ?? new DataClassInfo
        {
            ClassName        = dto.CodeName,
            ClassDisplayName = dto.Name,
            ClassTableName   = dto.CodeName.Replace(".", "_"),
        };

        var formXml = string.IsNullOrWhiteSpace(contentType.ClassFormDefinition)
            ? "<form></form>"
            : contentType.ClassFormDefinition;
        var fi  = new FormInfo(formXml);
        var changed = false;

        foreach (var f in dto.Fields)
        {
            var existing_f = fi.GetFormField(f.Name);
            var size       = (f.Size.HasValue && f.Size > 0) ? f.Size.Value : 200;
            var allowEmpty = f.IsRequired ? "false" : "true";
            var ctrl       = MapFieldTypeToControl(f.FieldType);
            var caption    = string.IsNullOrEmpty(f.Caption) ? f.Name : f.Caption;
            var defaultXml = string.IsNullOrEmpty(f.DefaultValue)
                ? "" : $"<defaultvalue>{System.Security.SecurityElement.Escape(f.DefaultValue)}</defaultvalue>";
            var dropOpts   = f.FieldType?.ToLower() == "dropdown" ? (f.DataSource ?? "") : "";

            if (existing_f == null)
            {
                formXml = formXml.Replace("</form>",
                    $"<field column=\"{f.Name}\" columntype=\"{MapDataType(f.DataType, f.FieldType)}\" columnsize=\"{size}\" allowempty=\"{allowEmpty}\" visible=\"true\" enabled=\"true\">" +
                    $"<properties><fieldcaption>{caption}</fieldcaption>{defaultXml}</properties>" +
                    $"<settings><controlname>{ctrl}</controlname><Options>{dropOpts}</Options><OptionsValueSeparator>;</OptionsValueSeparator></settings>" +
                    "</field>\n</form>");
            }
            else
            {
                existing_f.Caption    = caption;
                existing_f.AllowEmpty = allowEmpty != "false";
                existing_f.Size       = size;
                if (!string.IsNullOrEmpty(f.DefaultValue)) existing_f.DefaultValue = f.DefaultValue;
                existing_f.Settings["controlname"] = ctrl;
                if (f.FieldType?.ToLower() == "dropdown")
                {
                    existing_f.Settings["Options"] = dropOpts;
                    existing_f.Settings["OptionsValueSeparator"] = ";";
                }
                changed = true;
            }
        }

        contentType.ClassFormDefinition = changed ? fi.GetXmlDefinition() : formXml;
        DataClassInfoProvider.SetDataClassInfo(contentType);

        if (isNew) created++; else updated++;
    }

    private static string MapDataType(string? dt, string? fieldType) =>
        (fieldType?.ToLower() is "pageselector" or "pages" || dt?.ToLower() == "page") ? "webpages" :
        dt?.ToLower() switch
        {
            "text"                 => "text",
            "longtext"             => "richtext",
            "integer"              => "integer",
            "boolean"              => "boolean",
            "guid"                 => "guid",
            "contentitemreference" => "contentitemreference",
            _                      => "text",
        };

    private static string MapFieldTypeToControl(string? ft) =>
        ft?.ToLower().Trim() switch
        {
            "textbox"                                        => "Kentico.Administration.TextInput",
            "textarea"                                       => "Kentico.Administration.TextArea",
            "richtext"                                       => "Kentico.Administration.RichTextEditor",
            "dropdown"                                       => "Kentico.Administration.DropDownSelector",
            "checkbox"                                       => "Kentico.Administration.CheckBox",
            "contentitemselector" or "combinedcontentselector" => "Kentico.Administration.ContentItemSelector",
            "pageselector" or "pages"                        => "Kentico.Administration.WebPageSelector",
            _                                                => "Kentico.Administration.TextInput",
        };

    private static string MapControlToFieldType(string? ctrl) =>
        ctrl switch
        {
            "Kentico.Administration.TextInput"         => "textbox",
            "Kentico.Administration.TextArea"          => "textarea",
            "Kentico.Administration.RichTextEditor"    => "richtext",
            "Kentico.Administration.DropDownSelector"  => "dropdown",
            "Kentico.Administration.CheckBox"          => "checkbox",
            "Kentico.Administration.ContentItemSelector" => "contentitemselector",
            "Kentico.Administration.WebPageSelector"   => "pageselector",
            _                                          => "textbox",
        };

    private static int? TryParseInt(object? v) =>
        v != null && int.TryParse(v.ToString(), out var n) ? n : null;
}

// ─── DTOs / responses ────────────────────────────────────────────────────────

public record ApiResp<T>
{
    public bool    Success { get; init; }
    public T?      Data    { get; init; }
    public string? Error   { get; init; }

    public ApiResp(T data)          { Success = true;  Data  = data; }
    public ApiResp(string error)    { Success = false; Error = error; }
}

public record ContentTypeResponse(string Name, string CodeName, List<FieldResponse> Fields);

public record FieldResponse(
    string   Name, string DataType, bool IsRequired, int Size,
    string?  DefaultValue, string FieldType, string? Caption,
    string?  DataSource, int? MinItems, int? MaxItems,
    string[]? AllowedContentTypes, bool Visible);

public record ImportResult(int Created, int Updated, List<string> Errors);

public class ExportRequest
{
    public List<string> CodeNames { get; set; } = new();
}

public class ExportDto
{
    public string          Name     { get; set; } = "";
    public string          CodeName { get; set; } = "";
    public List<ExportFieldDto> Fields { get; set; } = new();
}

public class ExportFieldDto
{
    public string  Name         { get; set; } = "";
    public string  DataType     { get; set; } = "";
    public bool    IsRequired   { get; set; }
    public int?    Size         { get; set; }
    public string? DefaultValue { get; set; }
    public string? FieldType    { get; set; }
    public string? Caption      { get; set; }
    public string? DataSource   { get; set; }
    public int?    MinItems     { get; set; }
    public int?    MaxItems     { get; set; }
}
