namespace ContentTypeTransfer.Models;

public class ContentTypeDto
{
    public string Name     { get; set; } = string.Empty;
    public string CodeName { get; set; } = string.Empty;
    public List<ContentTypeFieldDto> Fields { get; set; } = [];
}

public class ContentTypeFieldDto
{
    public string  Name         { get; set; } = string.Empty;
    public string  DataType     { get; set; } = string.Empty;
    public bool    IsRequired   { get; set; }
    public int     Size         { get; set; }
    public string? DefaultValue { get; set; }
    public string  FieldType    { get; set; } = string.Empty;
    public string? Caption      { get; set; }
    public string? DataSource   { get; set; }
    public int?    MinItems     { get; set; }
    public int?    MaxItems     { get; set; }
    public string[]? AllowedContentTypes { get; set; }
    public bool    Visible      { get; set; } = true;
    public string? Visibility   { get; set; }
}

public class ExportRequest
{
    /// <summary>Code names to export. Empty list = export all.</summary>
    public List<string> CodeNames { get; set; } = [];
}

public class ImportRequest
{
    public string ZipBase64 { get; set; } = string.Empty;
    public string FileName  { get; set; } = string.Empty;
}

public class ApiResponse<T>
{
    public bool    Success { get; set; }
    public T?      Data    { get; set; }
    public string? Error   { get; set; }
}
