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
                string pkXml = "";

                if (string.IsNullOrWhiteSpace(formXml))
                {
                    //เก็บ PK เป็น raw string แยกไว้ก่อน
                    pkXml = @"
                             <field column=""ContentItemDataID"" columntype=""integer"" enabled=""true"" isPK=""true"" />
                             <field column=""ContentItemDataCommonDataID"" columntype=""integer"" enabled=""true"" refobjtype=""cms.contentitemcommondata"" reftype=""Required"" system=""true"" />
                             <field column=""ContentItemDataGUID"" columntype=""guid"" enabled=""true"" isunique=""true"" system=""true"" />";

                    formXml = "<form></form>";
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
                            var newField = new FormFieldInfo
                            {
                                Name = f.Name,
                                DataType = MapToKenticoType(f.DataType, f.FieldType),
                                Size = sizeXml,
                                AllowEmpty = allowempty == "true",
                                Visible = visible,
                                Caption = caption,
                            };

                            if (!string.IsNullOrEmpty(f.DefaultValue))
                                newField.DefaultValue = f.DefaultValue;

                            newField.Settings["controlname"] = controlName;

                            if (!string.IsNullOrEmpty(dropdownOptions))
                                newField.Settings["Options"] = f.DataSource;

                            if (!string.IsNullOrEmpty(allowedTypesXml))
                                newField.Settings["AllowedContentItemTypeIdentifiers"] =
                                    System.Text.Json.JsonSerializer.Serialize(
                                        f.AllowedContentTypes
                                            .Select(cn => DataClassInfoProvider.GetDataClassInfo(cn))
                                            .Where(ci => ci != null)
                                            .Select(ci => ci.ClassGUID.ToString())
                                            .ToList()
                                    );

                            //if (f.MinItems.HasValue)
                            //    newField.Settings["minitems"] = f.MinItems.Value.ToString();

                            //if (f.MaxItems.HasValue)
                            //    newField.Settings["maxitems"] = f.MaxItems.Value.ToString();

                            //Define Min max settings ตาม FieldType
                            ApplyMinMaxSettings(newField, f); 

                            //ใช้ FormInfo API แทน string.Replace
                            formDefinition.AddFormItem(newField);

                            // Apply visibility ด้วย
                            if (f.Visibility != null)
                                formDefinition = ApplyVisibility(formDefinition, f.Name, f.Visibility);
                            
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

                            //if (f.MinItems.HasValue)
                            //{
                            //    field.Settings["minitems"] = f.MinItems.Value.ToString();
                            //}

                            //if (f.MaxItems.HasValue)
                            //{
                            //    field.Settings["maxitems"] = f.MaxItems.Value.ToString();
                            //}

                            ApplyMinMaxSettings(field, f);

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

                //contentType.ClassFormDefinition = formXmlChanged
                //    ? formDefinition.GetXmlDefinition()
                //    : formXml;

                var userFieldsXml = formDefinition.GetXmlDefinition();
                string finalXml;

                if (!string.IsNullOrEmpty(pkXml))
                {
                    // inject PK เข้าไปเป็น first children ของ <form>
                    var doc = new XmlDocument();
                    doc.LoadXml(userFieldsXml);

                    var formNode = doc.SelectSingleNode("//form");
                    var pkFragment = doc.CreateDocumentFragment();
                    pkFragment.InnerXml = pkXml;

                    if (formNode.FirstChild != null)
                        formNode.InsertBefore(pkFragment, formNode.FirstChild);
                    else
                        formNode.AppendChild(pkFragment);

                    finalXml = doc.OuterXml;
                }
                else
                {
                    finalXml = userFieldsXml;
                }

                LogDebug($"[DEBUG] {model.CodeName} - ClassFormDefinition:\n{finalXml}");

                contentType.ClassFormDefinition = finalXml;
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
                "longtext" => "longtext",
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
            //oldNode?.ParentNode.RemoveChild(oldNode);
            if (oldNode != null && oldNode.ParentNode != null)
            {
                oldNode.ParentNode.RemoveChild(oldNode);
            }

            var xml = BuildVisibilityXml(visibility);

            if (!string.IsNullOrEmpty(xml))
            {
                var fragment = doc.CreateDocumentFragment();
                fragment.InnerXml = xml;

                fieldNode.AppendChild(fragment);
            }

            return new FormInfo(doc.OuterXml);
        }

        private void ApplyMinMaxSettings(FormFieldInfo field, FieldDto f)
        {
            var ft = f.FieldType?.ToLower();

            switch (ft)
            {
                case "textarea":
                    if (f.MinItems.HasValue)
                        field.Settings["MinRowsNumber"] = f.MinItems.Value.ToString();
                    if (f.MaxItems.HasValue)
                        field.Settings["MaxRowsNumber"] = f.MaxItems.Value.ToString();
                    break;

                case "pageselector":
                    // MinItems ไม่มีใน UI, มีแค่ MaximumPages
                    if (f.MaxItems.HasValue)
                        field.Settings["MaximumPages"] = f.MaxItems.Value.ToString();
                    break;

                case "contentitemselector":
                    if (f.MinItems.HasValue)
                        field.Settings["MinimumItems"] = f.MinItems.Value.ToString();
                    if (f.MaxItems.HasValue)
                        field.Settings["MaximumItems"] = f.MaxItems.Value.ToString();
                    break;

                    // FieldType อื่นที่ไม่มี min/max → ไม่ทำอะไร
            }
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

        private void LogDebug(string message)
        {
            var date = DateTime.Now.ToString("yyyyMMdd");
            var fileName = $"logs/import-debug-{date}.txt";
            var log = $"[{DateTime.Now}]\n{message}\n--------------------------\n";
            System.IO.File.AppendAllText(fileName, log);
        }
    }
}
