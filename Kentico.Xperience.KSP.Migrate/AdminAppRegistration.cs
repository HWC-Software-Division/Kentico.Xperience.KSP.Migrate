using ContentTypeTransfer.Admin;
using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;

// UIApplication อยู่ใน host assembly เหมือนเดิม
// UIPage ย้ายไปอยู่ใน UIPages.cs (ContentTypeTransfer assembly) แล้ว
[assembly: UIApplication(
    identifier:   ContentTypeTransfer.ContentTypeTransferModule.APP_IDENTIFIER,
    type:         typeof(ContentTypeTransferApp),
    slug:         "content-type-transfer",
    name:         "Content Type Transfer",
    category:     BaseApplicationCategories.DEVELOPMENT,
    icon:         Icons.ArrowCrookedLeft,
    templateName: TemplateNames.SECTION_LAYOUT)]

namespace Kentico.Xperience.KSP.Migrate
{
    public class AdminAppRegistration { }
}
