using CMS.ContentEngine;
using CMS.Helpers;
using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.KSP.Migrate.Models.API;
using Kentico.Xperience.KSP.Migrate.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Text.Json;
using CMS.DataEngine;
using System.Collections.Generic;

namespace Kentico.Xperience.KSP.Migrate.Controllers.API
{
    [ApiController]
    [Route("api/migrate")]
    public class MigrateController : ControllerBase
    {
        private readonly ILocalStringMigrationService localStringMigrationService;

        public MigrateController(ILocalStringMigrationService localStringMigrationService)
        {
            this.localStringMigrationService = localStringMigrationService;
        }

        [HttpPost("content-type")]
        public IActionResult CreateContentType([FromBody] ContentTypeDto model)
        {
            try
            {
                DataClassInfo contentType;

                if (model == null)
                    return BadRequest("Model is null");

                var existing = DataClassInfoProvider.GetDataClassInfo(model.CodeName);
                if (existing != null)
                {
                    contentType = existing;
                }
                else
                {
                    contentType = new DataClassInfo
                    {
                        ClassName = model.CodeName,
                        ClassDisplayName = model.Name,
                        ClassTableName = model.CodeName.Replace(".", "_")
                    };
                }

                var formXml = contentType.ClassFormDefinition ?? "<form>";

                foreach (var f in model.Fields)
                {
                    if (formXml.Contains($"column=\"{f.Name}\""))
                        continue;

                    var required = f.IsRequired ? "false" : "true";
                    var sizeXml = f.Size ?? 200;
                    var defaultXml = string.IsNullOrEmpty(f.DefaultValue) ? "" : $"<defaultvalue>{f.DefaultValue}</defaultvalue>";
                    var caption = string.IsNullOrEmpty(f.Caption) ? f.Name : f.Caption;

                    var dropdownOptions = "";

                    if (f.FieldType?.ToLower() == "dropdown")
                    {
                        dropdownOptions = BuildDropdownOptions(f.DataSource);
                    }

                    var controlName = MapFormControl(f.FieldType);

                    var fieldXml = $@"
                                <field column=""{f.Name}""
                                       columntype=""{MapToKenticoType(f.DataType)}""
                                       columnsize=""{sizeXml}"" 
                                       allowempty=""{required}""
                                       visible=""true"">
                                    <properties>
                                        <fieldcaption>{caption}</fieldcaption>  
                                        {defaultXml}
                                    </properties>
                                    <settings>
                                        <controlname>{controlName}</controlname>
                                        {dropdownOptions}
                                    </settings>
                                </field>";

                    formXml = formXml.Replace("</form>", fieldXml + "\n</form>");
                }

                contentType.ClassFormDefinition = formXml;

                DataClassInfoProvider.SetDataClassInfo(contentType);

                return Ok(new
                {
                    message = "ContentType created",
                    model.CodeName,
                    fields = model.Fields.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("local-string")]
        public IActionResult ImportLocalString([FromBody] JsonElement body)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                List<LocalStringImportDto> models;

                if (body.ValueKind == JsonValueKind.Array)
                {
                    models = JsonSerializer.Deserialize<List<LocalStringImportDto>>(body.GetRawText(), options)
                             ?? new List<LocalStringImportDto>();
                }
                else if (body.ValueKind == JsonValueKind.Object)
                {
                    var single = JsonSerializer.Deserialize<LocalStringImportDto>(body.GetRawText(), options);

                    models = single == null
                        ? new List<LocalStringImportDto>()
                        : new List<LocalStringImportDto> { single };
                }
                else
                {
                    return BadRequest("Request body must be a JSON object or JSON array.");
                }

                if (!models.Any())
                {
                    return BadRequest("No local strings found in request body.");
                }

                var result = localStringMigrationService.ImportMany(models);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private string MapToKenticoType(string type)
        {
            return type switch
            {
                "text" => "text",
                "longtext" => "richtext",
                "integer" => "integer",
                "boolean" => "boolean",
                "guid" => "guid",
                "mediafiles" => "guid",
                _ => "text"
            };
        }

        private string MapFormControl(string type)
        {
            return type?.ToLower() switch
            {
                "textbox" => "Kentico.Administration.TextInput",
                "textarea" => "Kentico.Administration.TextArea",
                "richtext" => "Kentico.Administration.RichText",
                "dropdown" => "Kentico.Administration.DropDown",
                "checkbox" => "Kentico.Administration.CheckBox",
                "media" => "Kentico.Administration.MediaFilesSelector",
                _ => "Kentico.Administration.TextInput"
            };
        }

        private string BuildDropdownOptions(string dataSource)
        {
            if (string.IsNullOrWhiteSpace(dataSource))
                return "";

            var lines = dataSource.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            var options = "";

            foreach (var line in lines)
            {
                var parts = line.Split(';');

                if (parts.Length == 2)
                {
                    var value = parts[0].Trim();
                    var text = parts[1].Trim();

                    options += $"<option value=\"{value}\">{text}</option>";
                }
            }

            return $"<options>{options}</options>";
        }
    }
}