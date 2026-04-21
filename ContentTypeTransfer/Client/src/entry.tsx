// ─────────────────────────────────────────────────────────────────────────────
// Module entry point — Kentico discovers templates by their export name.
//
// Template naming convention: export name must MATCH the last segment of
// templateName in [UIPage] attribute:
//   templateName: "@contenttypetransfer/web-admin/OverviewPage"
//   → export { OverviewPage }
// ─────────────────────────────────────────────────────────────────────────────

export { OverviewPage } from "./templates/OverviewPage";
export { ExportPage   } from "./templates/ExportPage";
export { ImportPage   } from "./templates/ImportPage";
