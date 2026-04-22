using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;

// UIPage attributes อยู่ใน ContentTypeTransfer assembly เดียวกับ class definitions
[assembly: UIPage(
    parentType:   typeof(ContentTypeTransfer.Admin.ContentTypeTransferApp),
    slug:         "overview",
    uiPageType:   typeof(ContentTypeTransfer.Admin.OverviewPage),
    name:         "Overview",
    templateName: "@contenttypetransfer/web.admin/OverviewPageTemplate",
    order:        UIPageOrder.First)]

[assembly: UIPage(
    parentType:   typeof(ContentTypeTransfer.Admin.ContentTypeTransferApp),
    slug:         "export",
    uiPageType:   typeof(ContentTypeTransfer.Admin.ExportPage),
    name:         "Export",
    templateName: "@contenttypetransfer/web.admin/ExportPageTemplate",
    order:        200)]

[assembly: UIPage(
    parentType:   typeof(ContentTypeTransfer.Admin.ContentTypeTransferApp),
    slug:         "import",
    uiPageType:   typeof(ContentTypeTransfer.Admin.ImportPage),
    name:         "Import",
    templateName: "@contenttypetransfer/web.admin/ImportPageTemplate",
    order:        300)]

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
