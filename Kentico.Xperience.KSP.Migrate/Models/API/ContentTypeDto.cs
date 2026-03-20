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

    }
}
