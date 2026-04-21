using ContentTypeTransfer.Admin;
using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;

[assembly: UIApplication(
    identifier: ContentTypeTransfer.ContentTypeTransferModule.APP_IDENTIFIER,
    type: typeof(ContentTypeTransferApp),
    slug: "content-type-transfer",
    name: "Content Type Transfer",
    category: BaseApplicationCategories.DEVELOPMENT,
    icon: Icons.ArrowCrookedLeft,
    templateName: TemplateNames.SECTION_LAYOUT)]

[assembly: UIPage(
    parentType: typeof(ContentTypeTransferApp),
    slug: "overview",
    uiPageType: typeof(OverviewPage),
    name: "Overview",
    templateName: "@contenttypetransfer/web.admin/OverviewPage",
    order: UIPageOrder.First)]

[assembly: UIPage(
    parentType: typeof(ContentTypeTransferApp),
    slug: "export",
    uiPageType: typeof(ExportPage),
    name: "Export",
    templateName: "@contenttypetransfer/web.admin/ExportPage",
    order: 200)]

[assembly: UIPage(
    parentType: typeof(ContentTypeTransferApp),
    slug: "import",
    uiPageType: typeof(ImportPage),
    name: "Import",
    templateName: "@contenttypetransfer/web.admin/ImportPage",
    order: 300)]

namespace Kentico.Xperience.KSP.Migrate
{
    public class AdminAppRegistration
    {
    }
}
