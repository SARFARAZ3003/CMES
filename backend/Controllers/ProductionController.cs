using Microsoft.AspNetCore.Mvc;

namespace CMES.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductionController : ControllerBase
    {
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new {
                message = "Production API is working!",
                data = DateTime.Now
            });
        }

        [HttpGet("summary")]
        public IActionResult GetSummary()
        {
            // Ab ke liye dummy data - baad mein DB se aayega
            var data = new
            {
                productionToday = 101,
                shiftA = 101,
                shiftB = 0,
                shiftC = 0,
                fesCount = 98,
                testOk = 111
            };
            return Ok(data);
        }

        // Shift wise production report - frontend Production Report page use karega
        [HttpGet("report")]
        public IActionResult GetReport()
        {
            var report = new[]
            {
                new { date = "2026-06-08", shift = "A", oldLine = 97, newLine = 102, testCycle = 137, fes = 98, dispatched = 0, testOK = 111 },
                new { date = "2026-06-08", shift = "B", oldLine = 0,  newLine = 0,   testCycle = 0,   fes = 0,  dispatched = 0, testOK = 0 },
                new { date = "2026-06-08", shift = "C", oldLine = 0,  newLine = 0,   testCycle = 0,   fes = 0,  dispatched = 0, testOK = 0 },
                new { date = "2026-06-07", shift = "A", oldLine = 95, newLine = 99,  testCycle = 130, fes = 94, dispatched = 88, testOK = 105 },
                new { date = "2026-06-07", shift = "B", oldLine = 41, newLine = 38,  testCycle = 60,  fes = 44, dispatched = 40, testOK = 51 },
            };
            return Ok(report);
        }

        // Aaj ki hourly production - charts ke liye
        [HttpGet("hourly")]
        public IActionResult GetHourly()
        {
            var hourly = new[]
            {
                new { hour = "6",  oldLine = 28, newLine = 25 },
                new { hour = "7",  oldLine = 26, newLine = 24 },
                new { hour = "8",  oldLine = 28, newLine = 21 },
                new { hour = "9",  oldLine = 21, newLine = 18 },
                new { hour = "10", oldLine = 18, newLine = 16 },
                new { hour = "11", oldLine = 22, newLine = 19 },
                new { hour = "12", oldLine = 15, newLine = 8 },
                new { hour = "13", oldLine = 9,  newLine = 4 },
                new { hour = "14", oldLine = 7,  newLine = 0 },
            };
            return Ok(hourly);
        }
    }
}
