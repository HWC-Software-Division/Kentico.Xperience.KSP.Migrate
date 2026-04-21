using CMS;
using CMS.Core;
using CMS.Membership;
using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;

// ── Auto-registration ──────────────────────────────────────────────────────
[assembly: RegisterModule(typeof(ContentTypeTransfer.ContentTypeTransferModule))]

namespace ContentTypeTransfer;

/// <summary>
/// Inherits AdminModule (not Module) so RegisterClientModule() is available.
/// The string "ContentTypeTransfer" must match the Code name in
/// Admin UI → Development → Modules.
/// </summary>
public class ContentTypeTransferModule : AdminModule
{
    public const string MODULE_NAME    = "ContentTypeTransfer";
    public const string APP_IDENTIFIER = "ContentTypeTransfer.App";

    public ContentTypeTransferModule() : base(MODULE_NAME) { }
    protected override void OnInit()   // ← ไม่มี parameter
    {
        base.OnInit();
        RegisterClientModule("contenttypetransfer", "web.admin");  // ← เปลี่ยน - เป็น .
    }
}
