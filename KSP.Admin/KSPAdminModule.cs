using CMS;
using CMS.Core;
using Kentico.Xperience.Admin.Base;

[assembly: RegisterModule(typeof(KSP.Admin.KSPAdminModule))]

namespace KSP.Admin;

public class KSPAdminModule : AdminModule
{
    public const string MODULE_NAME    = "KSP.Admin";
    public const string APP_IDENTIFIER = "KSP.ContentTypeTransfer";

    public KSPAdminModule() : base(MODULE_NAME) { }

    protected override void OnInit()
    {
        base.OnInit();
        // orgName="ksp" projectName="admin"
        // URL: /admin/adminresources/ksp.admin/entry.js
        // templateName: "@ksp/admin/OverviewPageTemplate"
        RegisterClientModule("ksp", "admin");
    }
}
