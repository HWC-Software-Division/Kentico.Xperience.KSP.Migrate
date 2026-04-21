using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
using CMS.DataEngine;
using CMS.FormEngine;
using ContentTypeTransfer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContentTypeTransfer.Services;

public class ContentTypeServiceOptions
{
    public const string SectionName = "ContentTypeTransfer";

    /// <summary>
    /// Base URL of the KSP.Migrate API that handles create/update.
    /// e.g. "https://localhost:44386/" or "http://localhost:5000/"
    /// </summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Optional bearer token / API key header value.</summary>
    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}

public class ContentTypeService : IContentTypeService
{
    private const string JSON_FILE_NAME = "content-types.json";

    private readonly HttpClient _http;
    private readonly ILogger<ContentTypeService> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented               = true
    };

    public ContentTypeService(
        IHttpClientFactory factory,
        IOptions<ContentTypeServiceOptions> options,
        ILogger<ContentTypeService> logger)
    {
        var opt = options.Value;
        _logger = logger;
        _http   = factory.CreateClient(nameof(ContentTypeService));

        if (!string.IsNullOrWhiteSpace(opt.ApiBaseUrl))
            _http.BaseAddress = new Uri(opt.ApiBaseUrl);

        _http.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);

        if (!string.IsNullOrWhiteSpace(opt.ApiKey))
            _http.DefaultRequestHeaders.Add("X-API-Key", opt.ApiKey);
    }

    // ── GET ALL — reads directly from XbyK local DB ────────────────────────

    public Task<List<ContentTypeDto>> GetAllAsync(CancellationToken ct = default)
    {
        // Query DataClassInfo directly — no external HTTP call needed for listing.
        // Exclude system classes (ClassIsSystem = true) to keep the list clean.
        var classes = DataClassInfoProvider
            .GetClasses()
            .WhereEquals("ClassIsSystem", false)
            .OrderBy("ClassDisplayName")
            .ToList();

        var result = classes.Select(c =>
        {
            var fields = new List<ContentTypeFieldDto>();

            if (!string.IsNullOrWhiteSpace(c.ClassFormDefinition))
            {
                try
                {
                    var formInfo = new FormInfo(c.ClassFormDefinition);
                    fields = formInfo.GetFields<FormFieldInfo>()
                        .Select(f => new ContentTypeFieldDto
                        {
                            Name         = f.Name,
                            DataType     = f.DataType,
                            IsRequired   = !f.AllowEmpty,
                            Size         = f.Size,
                            DefaultValue = f.DefaultValue?.ToString(),
                            FieldType    = f.Settings["controlname"]?.ToString() ?? string.Empty,
                            Caption      = f.Caption,
                            DataSource   = f.Settings["Options"]?.ToString(),
                            Visible      = f.Visible
                        })
                        .ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not parse form definition for {ClassName}", c.ClassName);
                }
            }

            return new ContentTypeDto
            {
                Name     = c.ClassDisplayName,
                CodeName = c.ClassName,
                Fields   = fields
            };
        }).ToList();

        return Task.FromResult(result);
    }

    // ── EXPORT — DB → JSON → .zip ──────────────────────────────────────────

    public async Task<Stream> ExportToZipAsync(
        IEnumerable<string> codeNames,
        CancellationToken ct = default)
    {
        // Get all, then filter to requested code names (empty = all)
        var all      = await GetAllAsync(ct);
        var codeList = codeNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var items    = codeList.Count > 0
            ? all.Where(x => codeList.Contains(x.CodeName)).ToList()
            : all;

        return BuildZip(items);
    }

    // ── IMPORT — .zip → JSON → POST /api/migrate/content-type ─────────────

    public async Task<ImportResult> ImportFromZipAsync(
        Stream zipStream,
        CancellationToken ct = default)
    {
        List<ContentTypeDto> contentTypes;

        try
        {
            contentTypes = ReadZip(zipStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read zip.");
            return new ImportResult(false, 0, 0, [$"Invalid zip: {ex.Message}"]);
        }

        var errors  = new List<string>();
        int created = 0, updated = 0;

        foreach (var ct2 in contentTypes)
        {
            try
            {
                // Determine create vs update by checking if the class exists
                var existing = DataClassInfoProvider.GetDataClassInfo(ct2.CodeName);
                bool isNew   = existing == null;

                // POST to KSP.Migrate API → it handles the DB write
                var response = await _http.PostAsJsonAsync(
                    "api/migrate/content-type", ct2, _jsonOpts, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    errors.Add($"{ct2.CodeName}: HTTP {(int)response.StatusCode} — {body}");
                    continue;
                }

                if (isNew) created++; else updated++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Import failed for {CodeName}.", ct2.CodeName);
                errors.Add($"{ct2.CodeName}: {ex.Message}");
            }
        }

        return new ImportResult(errors.Count == 0, created, updated, errors);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static MemoryStream BuildZip(List<ContentTypeDto> items)
    {
        var ms = new MemoryStream();
        using var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);
        var entry = zip.CreateEntry(JSON_FILE_NAME, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(JsonSerializer.Serialize(items, _jsonOpts));
        ms.Position = 0;
        return ms;
    }

    private static List<ContentTypeDto> ReadZip(Stream zipStream)
    {
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        var entry = zip.GetEntry(JSON_FILE_NAME)
            ?? throw new InvalidDataException($"'{JSON_FILE_NAME}' not found in zip.");
        using var reader = new StreamReader(entry.Open());
        return JsonSerializer.Deserialize<List<ContentTypeDto>>(reader.ReadToEnd(), _jsonOpts)
            ?? throw new InvalidDataException("Failed to deserialize content-types.json.");
    }
}
