using ContentTypeTransfer.Models;

namespace ContentTypeTransfer.Services;

public interface IContentTypeService
{
    /// <summary>Read all DataClassInfo from the local XbyK database.</summary>
    Task<List<ContentTypeDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Export selected code names → .zip containing content-types.json</summary>
    Task<Stream> ExportToZipAsync(IEnumerable<string> codeNames, CancellationToken ct = default);

    /// <summary>
    /// Read .zip → parse JSON → POST each ContentType to
    /// POST /api/migrate/content-type on the KSP.Migrate API.
    /// </summary>
    Task<ImportResult> ImportFromZipAsync(Stream zipStream, CancellationToken ct = default);
}

public record ImportResult(bool Success, int Created, int Updated, List<string> Errors);
