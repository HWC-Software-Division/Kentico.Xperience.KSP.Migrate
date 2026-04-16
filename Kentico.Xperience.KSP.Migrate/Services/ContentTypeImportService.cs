using CMS.DataEngine;
using CMS.FormEngine;
using Kentico.Xperience.KSP.Migrate.Models.API;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

namespace Kentico.Xperience.KSP.Migrate.Services
{
    public class ContentTypeImportService
    {
        public (string message, string codeName, int fieldCount) Import(ContentTypeDto model)
        {
            DataClassInfo contentType;
            var formXmlChanged = false;
            var msg = "";

            var existing = DataClassInfoProvider.GetDataClassInfo(model.CodeName);
            contentType = existing ?? new DataClassInfo
            {
                ClassName = model.CodeName,
                ClassDisplayName = model.Name,
                ClassTableName = model.CodeName.Replace(".", "_")
            };

            var formXml = string.IsNullOrWhiteSpace(contentType.ClassFormDefinition)
                            ? "<form></form>"
                            : contentType.ClassFormDefinition;

            var formDefinition = new FormInfo(formXml);             

            foreach (var f in model.Fields)
            {
                var field = formDefinition.GetFormField(f.Name);

                var allowempty = f.IsRequired ? "false" : "true";
                var sizeXml = (f.Size.HasValue && f.Size > 0) ? f.Size.Value : 200;
                var caption = string.IsNullOrEmpty(f.Caption) ? f.Name : f.Caption;                 
                var defaultXml = string.IsNullOrEmpty(f.DefaultValue) ? "" : $"<defaultvalue>{System.Security.SecurityElement.Escape(f.DefaultValue)}</defaultvalue>";

                //var minItems = (f.MinItems.HasValue && f.MinItems > 0) ? f.MinItems.Value : 1;
                //var maxItems = (f.MaxItems.HasValue && f.MaxItems > 0) ? f.MaxItems.Value : 1;

                var minMaxXml = "";
                if (f.MinItems.HasValue)
                {
                    minMaxXml += $"<minitems>{f.MinItems.Value}</minitems>";
                }

                if (f.MaxItems.HasValue)
                {
                    minMaxXml += $"<maxitems>{f.MaxItems.Value}</maxitems>";
                }

                var dropdownOptions = "";
                if (f.FieldType?.ToLower() == "dropdown")
                {
                    dropdownOptions = BuildDropdownOptions(f.DataSource);
                }

                var controlName = MapFormControl(f.FieldType, f.DataType);

                var allowedTypesXml = "";

                if (f.FieldType?.ToLower() == "contentitemselector" && f.AllowedContentTypes?.Any() == true)
                {
                    var guids = new List<string>();

                    foreach (var codeName in f.AllowedContentTypes)
                    {
                        var classInfo = DataClassInfoProvider.GetDataClassInfo(codeName);
                        if (classInfo != null)
                        {
                            guids.Add(classInfo.ClassGUID.ToString());
                        }
                    }

                    if (guids.Any())
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(guids);
                        allowedTypesXml = $"<AllowedContentItemTypeIdentifiers>{json}</AllowedContentItemTypeIdentifiers>";
                    }
                }

                var visible = f.Visible;

                if (field == null)
                {
                    //Create
                    var fieldXml = $@"
                                    <field column=""{f.Name}""
                                           columntype=""{MapToKenticoType(f.DataType, f.FieldType)}""
                                           columnsize=""{sizeXml}""
                                           allowempty=""{allowempty}""
                                           visible=""{visible}"">
                                        <properties>
                                            <fieldcaption>{caption}</fieldcaption>
                                            {defaultXml}
                                        </properties>
                                        <settings>
                                            <controlname>{controlName}</controlname>
                                            {allowedTypesXml}
                                            {dropdownOptions}
                                            {minMaxXml}
                                        </settings>
                                    </field>";

                    formXml = formXml.Replace("</form>", fieldXml + "\n</form>");
                    msg = "Created";
                }
                else
                {
                    //Update
                    field.Caption = caption;
                    field.AllowEmpty = allowempty == "true";
                    field.Size = sizeXml;
                    field.Settings["controlname"] = controlName;
                    field.Visible = f.Visible;

                    field.DataType = MapToKenticoType(f.DataType, f.FieldType);

                    if (!string.IsNullOrEmpty(f.DefaultValue))
                    {
                        field.DefaultValue = f.DefaultValue;
                    }

                    if (f.FieldType?.ToLower() == "dropdown" && !string.IsNullOrEmpty(f.DataSource))
                    {
                        field.Settings["Options"] = f.DataSource;
                    }

                    if (f.FieldType?.ToLower() == "contentitemselector" && f.AllowedContentTypes?.Any() == true)
                    {
                        var guids = new List<string>();

                        foreach (var codeName in f.AllowedContentTypes)
                        {
                            var classInfo = DataClassInfoProvider.GetDataClassInfo(codeName);
                            if (classInfo != null)
                            {
                                guids.Add(classInfo.ClassGUID.ToString());
                            }
                        }

                        if (guids.Any())
                        {
                            field.Settings["AllowedContentItemTypeIdentifiers"] =
                                System.Text.Json.JsonSerializer.Serialize(guids);
                        }
                    }

                    if (f.MinItems.HasValue)
                    {
                        field.Settings["minitems"] = f.MinItems.Value.ToString();
                    }

                    if (f.MaxItems.HasValue)
                    {
                        field.Settings["maxitems"] = f.MaxItems.Value.ToString();
                    }

                    formXmlChanged = true;
                    msg = "Updated";
                }
            }

            contentType.ClassFormDefinition = formXmlChanged
                ? formDefinition.GetXmlDefinition()
                : formXml;

            DataClassInfoProvider.SetDataClassInfo(contentType);

            return (msg, model.CodeName, model.Fields.Count);
        }

        private string MapToKenticoType(string type, string fieldType)
        {
            if (fieldType == "pageselector") return "webpages";

            return type?.ToLower() switch
            {
                "text" => "text",
                "longtext" => "richtext",
                "integer" => "integer",
                "boolean" => "boolean",
                "datetime" => "datetime",
                "guid" => "guid",
                "contentitemreference" => "contentitemreference",
                "contentitemasset" => "contentitemasset",
                _ => "text"
            };
        }

        private string MapFormControl(string type , string dataType = null)
        {
            var t = type?.ToLower();
            var d = dataType?.ToLower();
             
            if (d == "datetime")
                return "Kentico.Administration.DateTimeInput";

            if (d == "contentitemasset")
                return "Kentico.Administration.AssetUploader";

            return t?.ToLower() switch
            {
                "textbox" => "Kentico.Administration.TextInput",
                "textarea" => "Kentico.Administration.TextArea",
                "dropdown" => "Kentico.Administration.DropDownSelector",
                "contentitemselector" => "Kentico.Administration.ContentItemSelector",
                "pageselector" => "Kentico.Administration.WebPageSelector",
                _ => "Kentico.Administration.TextInput"
            };
        }

        private string BuildDropdownOptions(string dataSource)
        {
            if (string.IsNullOrWhiteSpace(dataSource))
                return ""; 

            //return dataSource;
            return $"<options>{dataSource}</options>";
        }
    }
}
