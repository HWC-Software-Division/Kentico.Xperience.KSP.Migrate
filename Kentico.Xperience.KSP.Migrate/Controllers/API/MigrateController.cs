using CMS.DataEngine;
using CMS.FormEngine;
using KSP.Core.Models;
using KSP.Core.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Kentico.Xperience.KSP.Migrate.Controllers.API;

[ApiController]
[Route("api/migrate")]
public class MigrateController : ControllerBase
{
    private readonly ILocalStringMigrationService _localStringService;

    public MigrateController(ILocalStringMigrationService localStringService) =>
        _localStringService = localStringService;

    [HttpPost("content-type")]
    public IActionResult CreateContentType([FromBody] ContentTypeDto model)
    {
        try
        {
            if (model == null) return BadRequest("Model is null");

            var existing   = DataClassInfoProvider.GetDataClassInfo(model.CodeName);
            var contentType = existing ?? new DataClassInfo
            {
                ClassName        = model.CodeName,
                ClassDisplayName = model.Name,
                ClassTableName   = model.CodeName.Replace(".", "_")
            };

            var formXml        = string.IsNullOrWhiteSpace(contentType.ClassFormDefinition)
                                     ? "<form></form>"
                                     : contentType.ClassFormDefinition;
            var formDefinition = new FormInfo(formXml);
            var formXmlChanged = false;
            var msg            = "ContentType created";

            foreach (var f in model.Fields)
            {
                var field       = formDefinition.GetFormField(f.Name);
                var allowEmpty  = f.IsRequired ? "false" : "true";
                var size        = (f.Size.HasValue && f.Size > 0) ? f.Size.Value : 200;
                var defaultXml  = string.IsNullOrEmpty(f.DefaultValue) ? "" : $"<defaultvalue>{System.Security.SecurityElement.Escape(f.DefaultValue)}</defaultvalue>";
                var caption     = string.IsNullOrEmpty(f.Caption) ? f.Name : f.Caption;
                var controlName = MapFormControl(f.FieldType);
                var dropdownOpts = f.FieldType?.ToLower() == "dropdown" ? (f.DataSource ?? "") : "";

                if (field == null)
                {
                    formXml = formXml.Replace("</form>", $@"<field column=""{f.Name}"" columntype=""{MapToKenticoType(f.DataType, f.FieldType)}"" columnsize=""{size}"" allowempty=""{allowEmpty}"" visible=""true"" enabled=""true"">
                        <properties><fieldcaption>{caption}</fieldcaption>{defaultXml}</properties>
                        <settings><controlname>{controlName}</controlname><Options>{dropdownOpts}</Options><OptionsValueSeparator>;</OptionsValueSeparator></settings>
                    </field>" + "\n</form>");
                }
                else
                {
                    field.Caption    = caption;
                    field.AllowEmpty = allowEmpty != "false";
                    field.Size       = size;
                    if (!string.IsNullOrEmpty(f.DefaultValue)) field.DefaultValue = f.DefaultValue;
                    field.Settings["controlname"] = controlName;
                    if (f.FieldType?.ToLower() == "dropdown") { field.Settings["Options"] = dropdownOpts; field.Settings["OptionsValueSeparator"] = ";"; }
                    formXmlChanged = true;
                    msg = "ContentType updated";
                }
            }

            contentType.ClassFormDefinition = formXmlChanged ? formDefinition.GetXmlDefinition() : formXml;
            DataClassInfoProvider.SetDataClassInfo(contentType);

            var classInfo  = DataClassInfoProvider.GetDataClassInfo(model.CodeName);
            formDefinition = new FormInfo(classInfo.ClassFormDefinition);

            foreach (var f in model.Fields)
            {
                if (f.FieldType?.ToLower() == "contentitemselector")
                {
                    var fd = formDefinition.GetFormField(f.Name);
                    if (fd != null)
                    {
                        var legacy = DataClassInfoProvider.GetDataClassInfo("Legacy.MediaFile");
                        if (legacy != null) fd.Settings["AllowedContentItemTypeIdentifiers"] = $"[\"{legacy.ClassGUID}\"]";
                    }
                }
                if (f.FieldType?.ToLower() == "pageselector")
                {
                    var fd = formDefinition.GetFormField(f.Name);
                    if (fd != null) { fd.Settings["MinimumPages"] = (f.MinItems ?? 1).ToString(); fd.Settings["MaximumPages"] = (f.MaxItems ?? 1).ToString(); }
                }
            }

            classInfo.ClassFormDefinition = formDefinition.GetXmlDefinition();
            DataClassInfoProvider.SetDataClassInfo(classInfo);

            return Ok(new { message = msg, model.CodeName, fields = model.Fields.Count });
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("local-string")]
    public IActionResult ImportLocalString([FromBody] JsonElement body)
    {
        try
        {
            var opts   = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var models = body.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<List<LocalStringImportDto>>(body.GetRawText(), opts) ?? new()
                : body.ValueKind == JsonValueKind.Object
                    ? new List<LocalStringImportDto> { JsonSerializer.Deserialize<LocalStringImportDto>(body.GetRawText(), opts)! }
                    : null;

            if (models == null) return BadRequest("Body must be a JSON object or array.");
            if (!models.Any())  return BadRequest("No local strings found.");

            return Ok(_localStringService.ImportMany(models));
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    private static string MapToKenticoType(string type, string? fieldType = null) =>
        (fieldType?.ToLower() == "pageselector" || type?.ToLower() == "page") ? "webpages" :
        type?.ToLower() switch
        {
            "text" => "text", "longtext" => "richtext", "integer" => "integer",
            "boolean" => "boolean", "guid" => "guid",
            "contentitemreference" => "contentitemreference", _ => "text"
        };

    private static string MapFormControl(string? type) =>
        type?.ToLower().Trim() switch
        {
            "textbox" => "Kentico.Administration.TextInput",
            "textarea" => "Kentico.Administration.TextArea",
            "richtext" => "Kentico.Administration.RichTextEditor",
            "dropdown" => "Kentico.Administration.DropDownSelector",
            "checkbox" => "Kentico.Administration.CheckBox",
            "contentitemselector" or "combinedcontentselector" => "Kentico.Administration.ContentItemSelector",
            "pageselector" or "pages" => "Kentico.Administration.WebPageSelector",
            _ => "Kentico.Administration.TextInput"
        };
}
