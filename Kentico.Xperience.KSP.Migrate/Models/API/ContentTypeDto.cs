using System.Collections.Generic;

namespace Kentico.Xperience.KSP.Migrate.Models.API
{
    public class ContentTypeDto
    {
        public string Name { get; set; }
        public string CodeName { get; set; }
        public List<FieldDto> Fields { get; set; }
    }

    public class FieldDto
    {
        public string Name { get; set; }
        public string DataType { get; set; }

        public bool IsRequired { get; set; }
        public int? Size { get; set; }
        public string DefaultValue { get; set; }
        public string FieldType { get; set; } //(form control)
        public string Caption { get; set; }

        public string DataSource { get; set; }
        public int? MinItems { get; set; }
        public int? MaxItems { get; set; }

        public List<string> AllowedContentTypes { get; set; }
        public bool Visible { get; set; }
        public VisibilityConditionDto Visibility { get; set; }
    }

    public class VisibilityConditionDto
    {
        public string Field { get; set; }
        public string Operator { get; set; }
        public string Value { get; set; }
        public bool CaseSensitive { get; set; }
    }
}
