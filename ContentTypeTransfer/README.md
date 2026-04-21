# ContentTypeTransfer — XbyK Custom Module

Custom Admin UI module for Xperience by Kentico v31.xx  
Provides Export and Import of Content Types via your team's external API.

---

## Project Structure

```
ContentTypeTransfer/
├── ContentTypeTransfer.csproj          # Project file (NuGet refs)
├── ContentTypeTransferModule.cs        # Module registration + UIApplication
├── ContentTypeTransferExtensions.cs    # IServiceCollection extension
├── Program.snippet.cs                  # How to wire into host app
├── appsettings.ContentTypeTransfer.json
│
├── Models/
│   └── ContentTypeDto.cs               # DTOs (mirrors your API's JSON)
│
├── Services/
│   ├── IContentTypeService.cs          # Interface
│   └── ContentTypeService.cs          # Implementation (HttpClient → zip)
│
├── Controllers/
│   └── ContentTypeTransferController.cs  # /api/content-type-transfer/*
│
├── Admin/
│   └── UIPages.cs                      # XbyK page registrations (C#)
│
└── Client/
    └── src/
        └── ContentTypeTransferApp.tsx  # React UI (all 3 pages)
```

---

## Setup (5 steps)

### 1 — Add NuGet reference in your host app

```xml
<PackageReference Include="ContentTypeTransfer" Version="1.0.0" />
```

Or add as a project reference during development:

```xml
<ProjectReference Include="../ContentTypeTransfer/ContentTypeTransfer.csproj" />
```

### 2 — Register services in Program.cs

```csharp
builder.Services.AddContentTypeTransfer(builder.Configuration);
```

### 3 — Add config to appsettings.json

```json
{
  "ContentTypeTransfer": {
    "ApiBaseUrl": "https://your-team-api.example.com/",
    "ApiKey": "your-secret-api-key",
    "TimeoutSeconds": 30
  }
}
```

### 4 — Build the client-side React UI

```bash
cd Client
npm install
npm run build
```

The build output goes to `Client/dist/` and is served by XbyK admin automatically.

### 5 — Run the app

The admin UI will appear under:  
**XbyK Admin → Development → Content Type Transfer**

---

## API Endpoints (internal, consumed by React UI)

| Method | Path | Description |
|--------|------|-------------|
| GET  | `/api/content-type-transfer/list`   | List all content types from external API |
| POST | `/api/content-type-transfer/export` | Export selected types → .zip download |
| POST | `/api/content-type-transfer/import` | Upload .zip → create/update in XbyK |

### External API contract (your team's API)

| Method | Path | Description |
|--------|------|-------------|
| GET  | `{ApiBaseUrl}/content-types`        | Returns `ContentTypeDto[]` |
| POST | `{ApiBaseUrl}/content-types/export` | Body: `{ codeNames: string[] }` → returns `ContentTypeDto[]` |
| POST | `{ApiBaseUrl}/content-types/upsert` | Body: `ContentTypeDto` → 201 Created or 200 Updated |

---

## Flow Diagram

```
EXPORT
  React UI  →  POST /api/content-type-transfer/export
            →  ContentTypeService.ExportToZipAsync()
            →  POST {ApiBaseUrl}/content-types/export
            →  receive ContentTypeDto[] JSON
            →  pack into .zip stream
            →  return as file download

IMPORT
  React UI  →  POST /api/content-type-transfer/import  (multipart/form-data)
            →  ContentTypeService.ImportFromZipAsync()
            →  extract content-types.json from .zip
            →  for each ContentType:
               POST {ApiBaseUrl}/content-types/upsert
            →  return ImportResult { created, updated, errors }
```
