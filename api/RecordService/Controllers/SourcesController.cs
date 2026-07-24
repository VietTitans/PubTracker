using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace RecordService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SourcesController : ControllerBase
{
    [HttpGet]
    public IActionResult GetSources()
    {
        return Ok(new[] { "IEEE", "PubMed", "Scopus" });
    }

}
