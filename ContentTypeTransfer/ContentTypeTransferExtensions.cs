using ContentTypeTransfer.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContentTypeTransfer;

public static class ContentTypeTransferExtensions
{
    /// <summary>
    /// Register in Program.cs of Kentico.Xperience.KSP.Migrate:
    /// <code>builder.Services.AddContentTypeTransfer(builder.Configuration);</code>
    /// </summary>
    public static IServiceCollection AddContentTypeTransfer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ContentTypeServiceOptions>(
            configuration.GetSection(ContentTypeServiceOptions.SectionName));

        services.AddHttpClient(nameof(ContentTypeService));
        services.AddScoped<IContentTypeService, ContentTypeService>();

        return services;
    }
}
