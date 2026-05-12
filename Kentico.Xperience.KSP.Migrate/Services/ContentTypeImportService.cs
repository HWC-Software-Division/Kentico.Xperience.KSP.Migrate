using CMS.Core;
using CMS.DataEngine;
using CMS.EventLog;
using CMS.FormEngine;
using HotChocolate.Data.Filters;
using Kentico.Xperience.KSP.Migrate.Models.API;
using Microsoft.AspNetCore.Mvc;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Xml;

namespace Kentico.Xperience.KSP.Migrate.Services
{
    public class ContentTypeImportService
    {
        public (string message, string codeName, int fieldCount, List<string> warnings) Import(ContentTypeDto model)
        {
            var warnings = new List<string>();
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
                    ClassHasUnmanagedDbSchema = false,
                    ClassType = "Content",
                    ClassContentTypeType = string.IsNullOrEmpty(model.ContentTypeType) ? "Website" : model.ContentTypeType,
                    ClassShortName = GenerateClassShortName(model.CodeName)
                };

                // Update display name และ icon ทั้ง create และ update
                contentType.ClassDisplayName = model.Name;
                contentType.ClassIconClass = string.IsNullOrEmpty(model.IconClass) ? "icon-kentico" : model.IconClass;
                contentType.ClassWebPageHasUrl = model.WebPageHasUrl; //0, false = No URL, 1, true = Have URL

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
                                    guids.Add(classInfo.ClassGUID.ToString());
                                else
                                    warnings.Add($"Field \"{f.Name}\": AllowedContentType \"{codeName}\" not found in target");
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
                                          
                            //Define Min max settings ตาม FieldType
                            ApplyMinMaxSettings(newField, f); 

                            //ใช้ FormInfo API แทน string.Replace
                            formDefinition.AddFormItem(newField);

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
                                        guids.Add(classInfo.ClassGUID.ToString());
                                    else
                                        warnings.Add($"Field \"{f.Name}\": AllowedContentType \"{codeName}\" not found in target");
                                }

                                if (guids.Any())
                                {
                                    field.Settings["AllowedContentItemTypeIdentifiers"] =
                                        System.Text.Json.JsonSerializer.Serialize(guids);
                                }
                            }

                            ApplyMinMaxSettings(field, f);

                            msg = "Updated";
                        }
                    }
                    catch (Exception exField)
                    {
                        //log ราย field
                        var source = "ContentTypeImportService";
                        var eventCode = "Field_ERROR";
                        var eventDescription = $@"{exField} {msg} FIELD ERROR: {model.CodeName} - {f.Name}";
                        //var eventType = EventTypeEnum.Error.ToString();

                        LogEvent(source, eventCode, eventDescription, "E"); 
                    }

                }

                // Reorder fields to match DTO order (new fields get appended; this puts them in the right position)
                {
                    var dtoOrder = model.Fields.Select(f => f.Name).ToList();
                    var allItems = formDefinition.ItemsList.ToList();
                    var sysItems = allItems.OfType<FormFieldInfo>()
                        .Where(f => f.System || f.PrimaryKey || f.Name == "ContentItemDataID")
                        .Cast<IDataDefinitionItem>().ToList();
                    var userFields = allItems.OfType<FormFieldInfo>()
                        .Where(f => !f.System && !f.PrimaryKey && f.Name != "ContentItemDataID")
                        .ToList();
                    var orderedUser = dtoOrder
                        .Select(n => userFields.FirstOrDefault(f => f.Name.Equals(n, StringComparison.OrdinalIgnoreCase)))
                        .Where(f => f != null)
                        .Cast<IDataDefinitionItem>()
                        .ToList();
                    // Include any user fields not present in the DTO at the end
                    var dtoSet = new HashSet<string>(dtoOrder, StringComparer.OrdinalIgnoreCase);
                    var extraItems = allItems.Where(i => !(i is FormFieldInfo fi2 &&
                        (fi2.System || fi2.PrimaryKey || fi2.Name == "ContentItemDataID" || dtoSet.Contains(fi2.Name)))).ToList();

                    formDefinition.ItemsList.Clear();
                    foreach (var i in sysItems)  formDefinition.ItemsList.Add(i);
                    foreach (var i in orderedUser) formDefinition.ItemsList.Add(i);
                    foreach (var i in extraItems) formDefinition.ItemsList.Add(i);
                }

                // Second pass: apply visibility conditions after ALL fields are in the form
                // (avoids ordering issues where the controlling field comes after the controlled one)
                foreach (var f in model.Fields)
                {
                    if (f.Visibility != null)
                        formDefinition = ApplyVisibility(formDefinition, f.Name, f.Visibility);
                }

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

                contentType.ClassFormDefinition = finalXml;
                DataClassInfoProvider.SetDataClassInfo(contentType);

                // #7 — only Website types have channel assignments
                if (string.Equals(model.ContentTypeType, "Website", StringComparison.OrdinalIgnoreCase))
                    SaveAllowedChannels(contentType.ClassID, model.AllowedChannels);

                return (msg, model.CodeName, model.Fields.Count, warnings);
            }
            catch (Exception ex)
            { 
                var source = "ContentTypeImportService";
                var eventCode = "Filed_ImportContentType";
                var eventDescription = $@"{ex} {model.CodeName}";
                //var eventType = EventTypeEnum.Error.ToString();

                LogEvent(source, eventCode, eventDescription, "E");

                return ($"Error: {ex.Message}", model.CodeName, model.Fields?.Count ?? 0, warnings);
            } 
        }

        /// <summary>Converts a FieldDto to FormFieldInfo — used for Reusable Field Schema import.</summary>
        public FormFieldInfo BuildFormFieldInfo(FieldDto f)
        {
            var caption  = string.IsNullOrEmpty(f.Caption) ? f.Name : f.Caption;
            // Preserve exported size exactly; only default to 200 when null.
            // Boolean fields export size=0 — do NOT convert to 200 or Kentico
            // may fail to bind the form component for the field.
            var size     = f.Size ?? 200;

            var fi = new FormFieldInfo
            {
                Name       = f.Name,
                DataType   = MapToKenticoType(f.DataType, f.FieldType),
                Size       = size,
                AllowEmpty = !f.IsRequired,
                Visible    = f.Visible,
                Caption    = caption,
            };

            if (!string.IsNullOrEmpty(f.DefaultValue))
                fi.DefaultValue = f.DefaultValue;

            fi.Settings["controlname"] = MapFormControl(f.FieldType, f.DataType);

            if (f.FieldType?.ToLower() == "dropdown" && !string.IsNullOrEmpty(f.DataSource))
                fi.Settings["Options"] = f.DataSource;

            if (f.FieldType?.ToLower() == "contentitemselector" && f.AllowedContentTypes?.Any() == true)
            {
                var guids = f.AllowedContentTypes
                    .Select(cn => DataClassInfoProvider.GetDataClassInfo(cn))
                    .Where(ci => ci != null)
                    .Select(ci => ci.ClassGUID.ToString())
                    .ToList();
                if (guids.Any())
                    fi.Settings["AllowedContentItemTypeIdentifiers"] =
                        System.Text.Json.JsonSerializer.Serialize(guids);
            }

            ApplyMinMaxSettings(fi, f);
            return fi;
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
             
            if (d == "datetime")         return "Kentico.Administration.DateTimeInput";
            if (d == "contentitemasset") return "Kentico.Administration.AssetUploader";
            if (d == "boolean")          return "Kentico.Administration.Checkbox";

            return t?.ToLower() switch
            {
                "textbox"              => "Kentico.Administration.TextInput",
                "textarea"             => "Kentico.Administration.TextArea",
                "richtext"             => "Kentico.Administration.RichTextEditor",
                "dropdown"             => "Kentico.Administration.DropDownSelector",
                "checkbox"             => "Kentico.Administration.Checkbox",
                "number"               => "Kentico.Administration.NumberInput",
                "contentitemselector"  => "Kentico.Administration.ContentItemSelector",
                "pageselector"         => "Kentico.Administration.WebPageSelector",
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

        /// <summary>
        /// Injects raw &lt;visibilityconditiondata&gt; XML into the specified field,
        /// replacing any existing visibility condition.
        /// </summary>
        public FormInfo ApplyVisibility(FormInfo formDefinition, string fieldName, string visibilityXml)
        {
            if (string.IsNullOrEmpty(visibilityXml)) return formDefinition;

            var formXml = formDefinition.GetXmlDefinition();
            var doc = new XmlDocument();
            doc.LoadXml(formXml);

            var fieldNode = doc.SelectSingleNode($"//field[@column='{fieldName}']");
            if (fieldNode == null) return formDefinition;

            // Remove existing visibility node if present
            var oldNode = fieldNode.SelectSingleNode("visibilityconditiondata");
            if (oldNode != null)
                fieldNode.RemoveChild(oldNode);

            // Inject the raw XML
            var fragment = doc.CreateDocumentFragment();
            fragment.InnerXml = visibilityXml;
            fieldNode.AppendChild(fragment);

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
                    if (f.MaxItems.HasValue)
                        field.Settings["MaximumPages"] = f.MaxItems.Value.ToString();
                    break;

                case "contentitemselector":
                    if (f.MinItems.HasValue)
                        field.Settings["MinimumItems"] = f.MinItems.Value.ToString();
                    if (f.MaxItems.HasValue)
                        field.Settings["MaximumItems"] = f.MaxItems.Value.ToString();
                    break;
            }
        }

        private void SaveAllowedChannels(int classId, List<string> channelNames)
        {
            if (channelNames == null || !channelNames.Any())
                return;

            foreach (var channelName in channelNames)
            {
                // หา ChannelID จาก ChannelName
                var channelRow = ConnectionHelper.ExecuteQuery($@"
                                                                    SELECT ChannelID FROM CMS_Channel 
                                                                    WHERE ChannelName = '{channelName}'
                                                                ", null, QueryTypeEnum.SQLQuery);

                if (channelRow.Tables[0].Rows.Count == 0)
                {
                    //LogError($"[CHANNEL] Channel not found: {channelName}",
                    //    new Exception("Channel not found"));

                    var source = "ContentTypeImportService";
                    var eventCode = "SaveAllowedChannels";
                    var eventDescription = $"Channel not found | ChannelName={channelName}";
                    //var eventType = EventTypeEnum.Error.ToString();

                    LogEvent(source, eventCode, eventDescription, "E");

                    continue;
                }

                var channelId = (int)channelRow.Tables[0].Rows[0]["ChannelID"];

                // เช็คว่ามี record อยู่แล้วหรือไม่
                var existing = ConnectionHelper.ExecuteQuery($@"
                                                                SELECT ContentTypeChannelID 
                                                                FROM CMS_ContentTypeChannel
                                                                WHERE ContentTypeChannelChannelID = {channelId}
                                                                AND ContentTypeChannelContentTypeID = {classId}
                                                            ", null, QueryTypeEnum.SQLQuery);

                if (existing.Tables[0].Rows.Count == 0)
                {
                    // Insert ใหม่
                    ConnectionHelper.ExecuteQuery($@"
                                                    INSERT INTO CMS_ContentTypeChannel 
                                                        (ContentTypeChannelChannelID, ContentTypeChannelContentTypeID)
                                                    VALUES ({channelId}, {classId})
                                                ", null, QueryTypeEnum.SQLQuery);
                }
            }
        }

        // Simplified logging method of CMS Event logs
        //I: Information, W: Warning, E: Error
        private void LogEvent(string source, string eventCode, string eventDescription, string eventType = "I")
        { 

            var eventInfo = new EventLogInfo
            {
                Source = source,
                EventCode = eventCode,
                EventDescription = eventDescription,
                EventType = eventType
            };

            EventLogProvider.LogEvent(eventInfo);            
        } 
    }
}
