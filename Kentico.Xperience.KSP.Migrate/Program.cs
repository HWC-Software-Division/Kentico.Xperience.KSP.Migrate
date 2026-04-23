using Kentico.Web.Mvc;
using KSP.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Force load KSP.Admin assembly so XbyK scans its [assembly: RegisterModule] + [assembly: UIPage]
_ = typeof(KSP.Admin.KSPAdminModule);

builder.Services.AddKentico(features =>
{
    // features.UsePageBuilder();
});

builder.Services.AddAuthentication();
builder.Services.AddXperienceCommunityLocalization();

// KSP.Core services
builder.Services.AddScoped<ILocalStringMigrationService, LocalStringMigrationService>();

builder.Services.AddControllersWithViews()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

builder.Services.AddScoped<ContentTypeImportService>();
builder.Services.AddScoped<ContentTypeExportService>();

var app = builder.Build();

app.InitKentico();
app.UseStaticFiles();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseKentico();
app.Kentico().MapRoutes();
app.MapControllers();
app.MapGet("/", () => "Kentico.Xperience.KSP.Migrate");

app.Run();
