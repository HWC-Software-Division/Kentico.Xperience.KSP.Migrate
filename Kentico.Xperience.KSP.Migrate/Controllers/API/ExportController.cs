using CMS.DataEngine;
using CMS.FormEngine;
using Kentico.Xperience.KSP.Migrate.Models.API;
using Kentico.Xperience.KSP.Migrate.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;

namespace Kentico.Xperience.KSP.Migrate.Controllers.API
{
    [ApiController]
    [Route("api/export")]
    public class ExportController : ControllerBase
    {
        private readonly ContentTypeExportService exportService;

        public ExportController(ContentTypeExportService exportService)
        {
            this.exportService = exportService;
        }

        [HttpGet("content-types")]
        public IActionResult ExportContentTypes()
        {
            var data = exportService.Export();

            //return Ok(data);

            var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            var zipBytes = CreateZip(json);

            var date = DateTime.Now.ToString("yyyyMMdd_HHmm");
            var fileName = $"export_content_types_{date}.zip";

            return File(zipBytes, "application/zip", fileName);

        }


        private byte[] CreateZip(string json)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    var date = DateTime.Now.ToString("yyyyMMdd");

                    var fileName = $"export-error-{date}.json";

                    var entry = archive.CreateEntry("content-types.json");

                    using (var entryStream = entry.Open())
                    using (var writer = new StreamWriter(entryStream))
                    {
                        writer.Write(json);
                    }
                }

                return memoryStream.ToArray();
            }
        }
    }
}
