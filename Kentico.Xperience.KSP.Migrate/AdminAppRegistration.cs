using KSP.Admin.UIPages;
using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;

// UIApplication lives in host assembly
// UIPage registrations live in KSP.Admin assembly (ContentTypeTransferPages.cs)
[assembly: UIApplication(
    identifier:   KSP.Admin.KSPAdminModule.APP_IDENTIFIER,
    type:         typeof(ContentTypeTransferApp),
    slug:         "content-type-transfer",
    name:         "Content Type Transfer",
    category:     BaseApplicationCategories.DEVELOPMENT,
    icon:         Icons.ArrowCrookedLeft,
    templateName: TemplateNames.SECTION_LAYOUT)]

namespace Kentico.Xperience.KSP.Migrate;

public class AdminAppRegistration { }
