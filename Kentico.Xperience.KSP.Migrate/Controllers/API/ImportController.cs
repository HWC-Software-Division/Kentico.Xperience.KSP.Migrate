using Kentico.Xperience.KSP.Migrate.Models.API;
using Kentico.Xperience.KSP.Migrate.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

namespace Kentico.Xperience.KSP.Migrate.Controllers.API
{
    [ApiController]
    [Route("api/import")]
    public class ImportController : ControllerBase
    {
        private readonly ContentTypeImportService importService;

        public ImportController(ContentTypeImportService importService)
        {
            this.importService = importService;
        }

        [HttpPost("content-types")]
        public IActionResult ImportContentTypes([FromBody] List<ContentTypeDto> models)
        {
            if (models == null || !models.Any())
                return BadRequest("No data");

            var results = new List<object>();

            foreach (var model in models)
            {
                var result = importService.Import(model);

                results.Add(new
                {
                    result.codeName,
                    result.fieldCount,
                    result.message
                });
            }

            return Ok(results);
        }
    
    }
}
