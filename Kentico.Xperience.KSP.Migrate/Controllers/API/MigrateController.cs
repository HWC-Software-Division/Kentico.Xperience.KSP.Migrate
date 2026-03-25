using CMS.ContentEngine;
using CMS.DataEngine;
using CMS.Helpers;
using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.KSP.Migrate.Models.API;
using Kentico.Xperience.KSP.Migrate.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;
using Org.BouncyCastle.Utilities;
using System;
using System.Linq;
using System.Text.Json;
using CMS.DataEngine;
using System.Collections.Generic;

using System.Text;
using System.Threading.Tasks;

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

        #region ContentType Migration

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

                //build XML form definition + mapping fields
                var formXml = contentType.ClassFormDefinition;

                if (string.IsNullOrWhiteSpace(formXml))
                {
                    formXml = "<form></form>";
                }

                foreach (var f in model.Fields)
                {
                    //กัน field ซ้ำ
                    if (formXml.Contains($"column=\"{f.Name}\""))
                        continue;

                    var allowempty = f.IsRequired ? "false" : "true";
                    var sizeXml = (f.Size.HasValue && f.Size > 0) ? f.Size.Value : 200;
                    var defaultXml = string.IsNullOrEmpty(f.DefaultValue) ? "" : $"<defaultvalue>{System.Security.SecurityElement.Escape(f.DefaultValue)}</defaultvalue>"; 
                    var caption = string.IsNullOrEmpty(f.Caption) ? f.Name : f.Caption;

                    var dropdownOptions = "";
                    if (f.FieldType?.ToLower() == "dropdown")
                    {
                        if (string.IsNullOrWhiteSpace(f.DataSource))
                            throw new Exception($"Dropdown '{f.Name}' ไม่มี DataSource");

                        dropdownOptions = BuildDropdownOptions(f.DataSource);
                    }

                    var imageAllowedContentTypes = "";
                    if (f.FieldType?.ToLower() == "combinedcontentselector") {
                        imageAllowedContentTypes = "<AllowedContentTypes>Legacy.MediaFile</AllowedContentTypes>";
                    }

                    //map form control
                    var controlName = MapFormControl(f.FieldType);

                    var fieldXml = $@"
                                <field column=""{f.Name}""
                                       columntype=""{MapToKenticoType(f.DataType)}""
                                       columnsize=""{sizeXml}"" 
                                       allowempty=""{allowempty}""
                                       visible=""true"">
                                    <properties>
                                        <fieldcaption>{caption}</fieldcaption>  
                                        {defaultXml}
                                    </properties>
                                    <settings>
                                        <controlname>{controlName}</controlname> 
                                        {imageAllowedContentTypes}
                                         <Options>{dropdownOptions}</Options>
                                        <OptionsValueSeparator>;</OptionsValueSeparator>
                                    </settings>
                                </field>";

                    formXml = formXml.Replace("</form>", fieldXml + "\n</form>");
                } 

                contentType.ClassFormDefinition = formXml;

                //create DataClass
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

        private string MapToKenticoType(string type)
        {
            return type switch
            {
                "text" => "text",
                "longtext" => "richtext",
                "integer" => "integer",
                "boolean" => "boolean",
                "guid" => "guid",
                //"mediafiles" => "guid",
                "contentitemreference" => "contentitemreference",
                _ => "text"
            };
        }

        private string MapFormControl(string type)
        {
            if (string.IsNullOrEmpty(type))
                return "Kentico.Administration.TextInput";

            type = type.ToLower().Trim(); 

            return type?.ToLower() switch
            {
                "textbox"  => "Kentico.Administration.TextInput",
                "textarea" => "Kentico.Administration.TextArea",
                "richtext" => "Kentico.Administration.RichTextEditor",
                "dropdown" => "Kentico.Administration.DropDownSelector",
                "checkbox" => "Kentico.Administration.CheckBox",
                //"media" => "Kentico.Administration.AssetSelector",
                //"mediafiles" => "Kentico.Administration.AssetSelector",
                "contentitemselector" => "Kentico.Administration.ContentItemSelector",
                "combinedcontentselector" => "Kentico.Administration.ContentItemSelector",

                _ => "Kentico.Administration.TextInput"
            };
        }

        private string BuildDropdownOptions(string dataSource)
        { 
            if (string.IsNullOrWhiteSpace(dataSource))
                return "";

            //var lines = dataSource.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            //var options = "";

            //foreach (var line in lines)
            //{
            //    var parts = line.Split(';');

            //    if (parts.Length == 2)
            //    {
            //        var value = parts[0].Trim();
            //        var text = parts[1].Trim();

            //        options += $"<option value=\"{value}\">{text}</option>";
            //    }
            //}

            //return $"<options>{options}</options>";

            //ใช้ raw string
            //var escaped = System.Security.SecurityElement.Escape(dataSource);

            //return $"<data>{escaped}</data>";

            var normalized = dataSource.Replace("\r\n", "\n")
                                       .Replace("\r", "\n");

            //var escaped = System.Security.SecurityElement.Escape(normalized);

            return dataSource;
        }

        #endregion ContentType Migration

        #region LocalString Migration

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

        #endregion LocalString Migration
    }
}