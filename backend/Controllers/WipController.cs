using Microsoft.AspNetCore.Mvc;

namespace CMES.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WipController : ControllerBase
    {
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new {
                message = "WIP API is working!",
                data = DateTime.Now
            });
        }

        // Total WIP plant mein
        [HttpGet("summary")]
        public IActionResult GetSummary()
        {
            var data = new
            {
                totalWip = 263,
                locations = 9,
                oldestHours = 18
            };
            return Ok(data);
        }

        // Location wise WIP count - WIP Report page use karega
        [HttpGet("locations")]
        public IActionResult GetLocations()
        {
            // Ab ke liye dummy data - baad mein DB se aayega
            var locations = new[]
            {
                new { location = "Quality Dock", count = 91 },
                new { location = "Paint Line",   count = 48 },
                new { location = "Test Cell",    count = 41 },
                new { location = "New Line",     count = 22 },
                new { location = "PE",           count = 15 },
                new { location = "Lineset Line", count = 15 },
                new { location = "EQA Audit",    count = 13 },
                new { location = "Old Line",     count = 5 },
                new { location = "Others",       count = 13 },
            };
            return Ok(locations);
        }
    }
}
