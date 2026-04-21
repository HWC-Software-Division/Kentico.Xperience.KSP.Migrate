// ลบ assembly attributes ออกทั้งหมด ไม่ต้องมี [assembly: UIPage(...)] เลย

using Kentico.Xperience.Admin.Base;

namespace ContentTypeTransfer.Admin;

public class ContentTypeTransferApp : ApplicationPage { }

public class OverviewPage : Page<OverviewPageClientProperties>
{
    public override Task<OverviewPageClientProperties> ConfigureTemplateProperties(
        OverviewPageClientProperties p)
    {
        p.ApiBaseUrl = "/api/content-type-transfer";
        return Task.FromResult(p);
    }
}
public class OverviewPageClientProperties : TemplateClientProperties
{
    public string ApiBaseUrl { get; set; } = string.Empty;
}

public class ExportPage : Page<ExportPageClientProperties>
{
    public override Task<ExportPageClientProperties> ConfigureTemplateProperties(
        ExportPageClientProperties p)
    {
        p.ApiBaseUrl = "/api/content-type-transfer";
        return Task.FromResult(p);
    }
}
public class ExportPageClientProperties : TemplateClientProperties
{
    public string ApiBaseUrl { get; set; } = string.Empty;
}

public class ImportPage : Page<ImportPageClientProperties>
{
    public override Task<ImportPageClientProperties> ConfigureTemplateProperties(
        ImportPageClientProperties p)
    {
        p.ApiBaseUrl = "/api/content-type-transfer";
        return Task.FromResult(p);
    }
}
public class ImportPageClientProperties : TemplateClientProperties
{
    public string ApiBaseUrl { get; set; } = string.Empty;
}