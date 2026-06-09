using Microsoft.AspNetCore.Mvc;

namespace CMES.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new {
                message = "Inventory API is working!",
                data = DateTime.Now
            });
        }

        // Inventory summary - top cards ke liye
        [HttpGet("summary")]
        public IActionResult GetSummary()
        {
            var data = new
            {
                totalItems = 6,
                lowStock = 2,
                outOfStock = 1
            };
            return Ok(data);
        }

        // Pura stock list - Inventory page table use karega
        [HttpGet("items")]
        public IActionResult GetItems()
        {
            // Ab ke liye dummy data - baad mein DB se aayega
            var items = new[]
            {
                new { partNo = "PRT-1001", partName = "Crankshaft Assembly", category = "Component",  inStock = 320, minLevel = 100, unit = "pcs", status = "OK" },
                new { partNo = "PRT-1002", partName = "Cylinder Head",       category = "Component",  inStock = 85,  minLevel = 120, unit = "pcs", status = "Low" },
                new { partNo = "PRT-1003", partName = "Piston Ring Set",     category = "Component",  inStock = 540, minLevel = 150, unit = "pcs", status = "OK" },
                new { partNo = "PRT-1004", partName = "Engine Oil 15W-40",   category = "Consumable", inStock = 0,   minLevel = 50,  unit = "ltr", status = "Out" },
                new { partNo = "PRT-1005", partName = "Gasket Kit",          category = "Component",  inStock = 78,  minLevel = 90,  unit = "pcs", status = "Low" },
                new { partNo = "PRT-1006", partName = "Cast Iron Block",     category = "Raw",        inStock = 410, minLevel = 120, unit = "pcs", status = "OK" },
            };
            return Ok(items);
        }
    }
}
