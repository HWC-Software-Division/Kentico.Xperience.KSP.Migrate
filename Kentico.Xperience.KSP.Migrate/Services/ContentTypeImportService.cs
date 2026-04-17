using CMS.DataEngine;
using CMS.FormEngine;
using Kentico.Xperience.KSP.Migrate.Models.API;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Kentico.Xperience.KSP.Migrate.Services
{
    public class ContentTypeImportService
    {
        public (string message, string codeName, int fieldCount) Import(ContentTypeDto model)
        {
            try 
            {
                if (model == null)
                    throw new Exception("Model is null");

                DataClassInfo contentType;
                var formXmlChanged = false;
                var msg = "";

                var existing = DataClassInfoProvider.GetDataClassInfo(model.CodeName);
                contentType = existing ?? new DataClassInfo
                {
                    ClassName = model.CodeName,
                    ClassDisplayName = model.Name,
                    ClassTableName = model.CodeName.Replace(".", "_"),

                    ClassContactOverwriteEnabled = false,
                    ClassCodeGenerationSettings = GenerateClassCodeGenerationSettings(model.CodeName),
                    ClassIconClass = "icon-kentico",
                    ClassHasUnmanagedDbSchema = false,
                    ClassWebPageHasUrl = false, //0 = No URL, 1 = Have URL

                    ClassType = "Content",
                    ClassContentTypeType = "Website",
                    ClassShortName = GenerateClassShortName(model.CodeName)
                };

                //var formXml = string.IsNullOrWhiteSpace(contentType.ClassFormDefinition)
                //                ? "<form></form>"
                //                : contentType.ClassFormDefinition;

                var formXml = contentType.ClassFormDefinition;

                if (string.IsNullOrWhiteSpace(formXml))
                {
                    //CREATE ใหม่ ต้องมี Primary Key
                    //formXml = @"
                    //    <form>
                    //        <field column=""ContentItemDataID""
                    //               columntype=""integer""
                    //               ispk=""true""
                    //               identity=""true""
                    //               allowempty=""false""
                    //               visible=""false"" />
                    //    </form>";

                    formXml = @"
                            <form>
                                <field column=""ContentItemDataID"" columntype=""integer"" ispk=""true"" enabled=""true"" />
                                <field column=""ContentItemDataCommonDataID"" columntype=""integer"" system=""true"" />
                                <field column=""ContentItemDataGUID"" columntype=""guid"" system=""true"" isunique=""true"" />
                            </form>";
                }

                var formDefinition = new FormInfo(formXml);

                //Process fields : Create new or update existing
                foreach (var f in model.Fields)
                {
                    try
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
                        var visibilityXml = f.Visibility != null ? BuildVisibilityXml(f.Visibility) : "";


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
                                        {visibilityXml}
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

                            formDefinition = ApplyVisibility(formDefinition, f.Name, f.Visibility);
                        }
                    }
                    catch (Exception exField)
                    {
                        //log ราย field
                        LogError($"[FIELD ERROR] {model.CodeName} - {f.Name}", exField);
                    }

                }

                contentType.ClassFormDefinition = formXmlChanged
                    ? formDefinition.GetXmlDefinition()
                    : formXml;

                DataClassInfoProvider.SetDataClassInfo(contentType);

                return (msg, model.CodeName, model.Fields.Count);
            }
            catch (Exception ex)
            {
                LogError($"Failed to import content type '{model.CodeName}'", ex);
                return ($"Error: {ex.Message}", model.CodeName, model.Fields?.Count ?? 0);
            } 
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

        private string GenerateClassShortName(string codeName)
        { 
            var baseName = codeName.Replace(".", "");             
            var randomPart = GenerateRandomString(8);             
            return baseName + randomPart;
        }

        private static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();

            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        } 

        public static string GenerateClassCodeGenerationSettings(string className)
        {
            var nameSpace = GetNamespaceFromClassName(className);

            return $"<Data><NameSpace>{nameSpace}</NameSpace></Data>";
        }
        public static string GetNamespaceFromClassName(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
                return string.Empty;

            var parts = className.Split('.');
            return parts.Length > 0 ? parts[0] : string.Empty;
        }

        private string BuildVisibilityXml(VisibilityConditionDto v)
        {
            if (v == null) return "";

            return $@"
                    <visibilityconditiondata>
                      <VisibilityConditionConfiguration>
                        <Identifier>{v.Operator}</Identifier>
                        <Properties>
                          <PropertyName>{v.Field}</PropertyName>
                          <Value>{v.Value}</Value>
                          <CaseSensitive>{v.CaseSensitive.ToString().ToLower()}</CaseSensitive>
                        </Properties>
                      </VisibilityConditionConfiguration>
                    </visibilityconditiondata>";
        }

        private FormInfo ApplyVisibility(FormInfo formDefinition, string fieldName, VisibilityConditionDto visibility)
        {
            if (visibility == null) return formDefinition;

            var target = formDefinition.GetFormField(visibility.Field);
            if (target == null) return formDefinition;

            var formXml = formDefinition.GetXmlDefinition();

            var doc = new XmlDocument();
            doc.LoadXml(formXml);

            var fieldNode = doc.SelectSingleNode($"//field[@column='{fieldName}']");
            if (fieldNode == null) return formDefinition;

            var oldNode = fieldNode.SelectSingleNode("visibilityconditiondata");
            oldNode?.ParentNode.RemoveChild(oldNode);

            var xml = BuildVisibilityXml(visibility);

            if (!string.IsNullOrEmpty(xml))
            {
                var fragment = doc.CreateDocumentFragment();
                fragment.InnerXml = xml;

                fieldNode.AppendChild(fragment);
            }

            return new FormInfo(doc.OuterXml);
        }

        private void LogError(string message, Exception ex)
        {
            var date = DateTime.Now.ToString("yyyyMMdd");

            var fileName = $"logs/import-error-{date}.txt";

            var log = $@"
[{DateTime.Now}]
{message}
ERROR: {ex.Message}
STACK: {ex.StackTrace}
--------------------------";

            System.IO.File.AppendAllText(fileName, log);
        }
    }
}
