using ContentTypeTransfer;
using Kentico.Web.Mvc;
using Kentico.Xperience.KSP.Migrate.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddContentTypeTransfer(builder.Configuration);

builder.Services.AddKentico(features =>
{
    // features.UsePageBuilder();
});

builder.Services.AddAuthentication();
builder.Services.AddXperienceCommunityLocalization();
builder.Services.AddScoped<ILocalStringMigrationService, LocalStringMigrationService>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ✅ Force load ContentTypeTransfer assembly ก่อน InitKentico scan attributes
_ = typeof(ContentTypeTransfer.ContentTypeTransferModule);

app.InitKentico();  // ← XbyK scan assembly attributes ตรงนี้

app.UseStaticFiles();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseKentico();
app.Kentico().MapRoutes();
app.MapControllers();
app.Run();