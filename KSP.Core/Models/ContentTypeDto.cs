namespace KSP.Core.Models;

public class ContentTypeDto
{
    public string Name     { get; set; } = string.Empty;
    public string CodeName { get; set; } = string.Empty;
    public List<FieldDto> Fields { get; set; } = new();
}

public class FieldDto
{
    public string  Name         { get; set; } = string.Empty;
    public string  DataType     { get; set; } = string.Empty;
    public bool    IsRequired   { get; set; }
    public int?    Size         { get; set; }
    public string? DefaultValue { get; set; }
    public string? FieldType    { get; set; }
    public string? Caption      { get; set; }
    public string? DataSource   { get; set; }
    public int?    MinItems     { get; set; }
    public int?    MaxItems     { get; set; }
    public List<string>? AllowedContentTypes { get; set; }
}
