using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;

// UIPage attributes in same assembly as class definitions
[assembly: UIPage(
    parentType:   typeof(KSP.Admin.UIPages.ContentTypeTransferApp),
    slug:         "overview",
    uiPageType:   typeof(KSP.Admin.UIPages.OverviewPage),
    name:         "Overview",
    templateName: "@ksp/admin/OverviewPage",
    order:        UIPageOrder.First)]

[assembly: UIPage(
    parentType:   typeof(KSP.Admin.UIPages.ContentTypeTransferApp),
    slug:         "export",
    uiPageType:   typeof(KSP.Admin.UIPages.ExportPage),
    name:         "Export",
    templateName: "@ksp/admin/ExportPage",
    order:        200)]

[assembly: UIPage(
    parentType:   typeof(KSP.Admin.UIPages.ContentTypeTransferApp),
    slug:         "import",
    uiPageType:   typeof(KSP.Admin.UIPages.ImportPage),
    name:         "Import",
    templateName: "@ksp/admin/ImportPage",
    order:        300)]

namespace KSP.Admin.UIPages;

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
