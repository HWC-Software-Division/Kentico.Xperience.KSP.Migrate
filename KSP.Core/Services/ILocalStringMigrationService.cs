using KSP.Core.Models;

namespace KSP.Core.Services;

public interface ILocalStringMigrationService
{
    LocalStringBatchImportResult ImportMany(IEnumerable<LocalStringImportDto> models);
}

public class LocalStringBatchImportResult
{
    public int TotalKeysProcessed          { get; set; }
    public int TotalTranslationsProcessed  { get; set; }
    public int TotalTranslationsCreated    { get; set; }
    public int TotalTranslationsUpdated    { get; set; }
    public List<LocalStringImportResult> Items  { get; set; } = new();
    public List<LocalStringImportError>  Errors { get; set; } = new();
}

public class LocalStringImportResult
{
    public string Key                  { get; set; } = string.Empty;
    public int    KeyItemId            { get; set; }
    public bool   KeyCreated           { get; set; }
    public int    TranslationsProcessed{ get; set; }
    public int    TranslationsCreated  { get; set; }
    public int    TranslationsUpdated  { get; set; }
    public List<string> Warnings       { get; set; } = new();
}

public class LocalStringImportError
{
    public string Key     { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
