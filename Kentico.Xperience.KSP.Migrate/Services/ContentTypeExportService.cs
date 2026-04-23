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
    public class ContentTypeExportService 
    {
        public List<ContentTypeDto> Export()
        {
            var classes = DataClassInfoProvider.GetClasses()
                .WhereEquals("ClassType", "Content");

            var result = new List<ContentTypeDto>();

            foreach (DataClassInfo c in classes)
            {
                var form = new FormInfo(c.ClassFormDefinition);

                var dto = new ContentTypeDto
                {
                    Name = c.ClassDisplayName,
                    CodeName = c.ClassName,
                    IconClass = c.ClassIconClass,
                    WebPageHasUrl = c.ClassWebPageHasUrl,

                    Fields = new List<FieldDto>()
                };

                foreach (var f in form.GetFields(true, true))
                {
                    if (f.System || f.PrimaryKey || f.Name == "ContentItemDataID")
                        continue;

                    dto.Fields.Add(new FieldDto
                    {
                        Name = f.Name,
                        DataType = f.DataType,
                        FieldType = MapBack(f),
                        IsRequired = !f.AllowEmpty,
                        DefaultValue = f.DefaultValue,
                        Caption = f.Caption,
                        Size = f.Size,
                        AllowedContentTypes = GetAllowedTypes(f),
                        DataSource = f.Settings["Options"]?.ToString(),
                        Visible = f.Visible,
                        Visibility = GetVisibility(form, f)
                    });
                }

                result.Add(dto);
            }

            return result;
        }

        private string MapBack(FormFieldInfo f)
        {
            var control = f.Settings["controlname"]?.ToString();
            var dataType = f.DataType?.ToLower();

            if (dataType == "datetime")
                return "datetime";

            if (dataType == "contentitemasset")
                return "contentitemasset";

            return control switch
            {
                "Kentico.Administration.TextInput" => "textbox",
                "Kentico.Administration.TextArea" => "textarea",
                "Kentico.Administration.RichTextEditor" => "richtext",
                "Kentico.Administration.DropDownSelector" => "dropdown",
                "Kentico.Administration.ContentItemSelector" => "contentitemselector",
                "Kentico.Administration.WebPageSelector" => "pageselector",
                "Kentico.Administration.CheckBox" => "checkbox",
                "Kentico.Administration.NumberInput" => "number",
                _ => "textbox"
            };
        }

        private List<string> GetAllowedTypes(FormFieldInfo f)
        {
            var json = f.Settings["AllowedContentItemTypeIdentifiers"]?.ToString();

            if (string.IsNullOrEmpty(json))
                return null;

            var guids = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);

            var result = new List<string>();

            foreach (var g in guids)
            {
                var classInfo = DataClassInfoProvider.GetClasses().FirstOrDefault(x => x.ClassGUID == Guid.Parse(g));
                if (classInfo != null)
                {
                    result.Add(classInfo.ClassName);
                }
            }

            return result;
        }

        private VisibilityConditionDto GetVisibility(FormInfo form, FormFieldInfo f)
        {
            var xml = form.GetXmlDefinition();

            var doc = new XmlDocument();
            doc.LoadXml(xml);

            var node = doc.SelectSingleNode($"//field[@column='{f.Name}']/visibilityconditiondata/VisibilityConditionConfiguration");

            if (node == null)
                return null;

            return new VisibilityConditionDto
            {
                Field = node.SelectSingleNode("Properties/PropertyName")?.InnerText,
                Value = node.SelectSingleNode("Properties/Value")?.InnerText,
                CaseSensitive = node.SelectSingleNode("Properties/CaseSensitive")?.InnerText == "true",
                Operator = node.SelectSingleNode("Identifier")?.InnerText
            };
        }
    }
}
