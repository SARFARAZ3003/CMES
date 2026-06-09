using Microsoft.AspNetCore.Mvc;

namespace CMES.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EngineController : ControllerBase
    {
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new {
                message = "Engine API is working!",
                data = DateTime.Now
            });
        }

        // Model Tracking page - engines list with current location/status
        [HttpGet("tracking")]
        public IActionResult GetTracking()
        {
            // Ab ke liye dummy data - baad mein DB se aayega
            var engines = new[]
            {
                new { engineNo = "ENG-260601", model = "TX-450", line = "New Line", location = "Test Cell",    status = "WIP",        shift = "A" },
                new { engineNo = "ENG-260602", model = "TX-450", line = "New Line", location = "Quality Dock", status = "FES",        shift = "A" },
                new { engineNo = "ENG-260603", model = "DX-200", line = "Old Line", location = "Paint Line",   status = "WIP",        shift = "A" },
                new { engineNo = "ENG-260604", model = "DX-200", line = "Old Line", location = "Dispatch",     status = "Dispatched", shift = "A" },
                new { engineNo = "ENG-260605", model = "GX-700", line = "New Line", location = "EQA Audit",    status = "TestOK",     shift = "A" },
                new { engineNo = "ENG-260606", model = "GX-700", line = "New Line", location = "Lineset Line", status = "WIP",        shift = "B" },
                new { engineNo = "ENG-260607", model = "TX-450", line = "Old Line", location = "Test Cell",    status = "WIP",        shift = "B" },
            };
            return Ok(engines);
        }

        // Model wise count - kitne engine har model ke
        [HttpGet("models")]
        public IActionResult GetModels()
        {
            var models = new[]
            {
                new { model = "TX-450", today = 42, wip = 96 },
                new { model = "DX-200", today = 31, wip = 74 },
                new { model = "GX-700", today = 28, wip = 93 },
            };
            return Ok(models);
        }

        // Single engine ka detail - engineNo se search
        [HttpGet("{engineNo}")]
        public IActionResult GetByEngineNo(string engineNo)
        {
            var engine = new
            {
                engineNo,
                model = "TX-450",
                line = "New Line",
                location = "Test Cell",
                status = "WIP",
                shift = "A",
                createdAt = "2026-06-08T06:42:00"
            };
            return Ok(engine);
        }
    }
}
