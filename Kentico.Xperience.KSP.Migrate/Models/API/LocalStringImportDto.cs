using System.Collections.Generic;

namespace Kentico.Xperience.KSP.Migrate.Models.API
{
    public class LocalStringImportDto
    {
        public int? Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<LocalStringTranslationDto> Values { get; set; } = new();
    }

    public class LocalStringTranslationDto
    {
        public string Language { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}