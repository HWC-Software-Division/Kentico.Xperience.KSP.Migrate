using System.Collections.Generic;

namespace Kentico.Xperience.KSP.Migrate.Models.API
{
    public class ContentTypeDto
    {
        public string Name { get; set; }  //Display name
        public string CodeName { get; set; }

        public string IconClass { get; set; }
        public bool WebPageHasUrl { get; set; }

        public List<FieldDto> Fields { get; set; }

        public List<string> AllowedChannels { get; set; } //ChannelName

        /// <summary>"Website" | "Reusable"</summary>
        public string ContentTypeType { get; set; } = "Website";

        /// <summary>Names of reusable field schemas attached to this content type.</summary>
        public List<string> ReusableSchemas { get; set; }
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

        /// <summary>
        /// Raw &lt;visibilityconditiondata&gt;...&lt;/visibilityconditiondata&gt; XML,
        /// preserved exactly as Kentico stores it so all condition types round-trip correctly.
        /// </summary>
        public string Visibility { get; set; }
    }

    public class ReusableFieldSchemaDto
    {
        public string Name        { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Description { get; set; }
        public string Guid        { get; set; } = "";
        public List<FieldDto> Fields { get; set; } = new();
    }
}
