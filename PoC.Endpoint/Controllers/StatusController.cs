using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace PoC.Endpoint.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class StatusController : ControllerBase
    {
        private readonly ILogger<StatusController> _logger;

        public StatusController(ILogger<StatusController> logger)
        {
            _logger = logger;
        }

        [HttpGet("{statusCode}")]
        public IActionResult Get([FromRoute] int statusCode)
        {
            _logger.LogInformation($"Status code: {statusCode}");
            return StatusCode(statusCode);
        }
    }
}
