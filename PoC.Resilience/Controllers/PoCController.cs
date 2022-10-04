using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;

namespace PoC.Resilience.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PoCController : ControllerBase
    {
        private readonly ILogger<PoCController> _logger;
        private readonly IHttpClientFactory _clientFactory;

        public PoCController(ILogger<PoCController> logger,
            IHttpClientFactory clientFactory)
        {
            _logger = logger;
            _clientFactory = clientFactory;
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync()
        {
            HttpClient clientHttp = _clientFactory.CreateClient("MyPoCHttpClient");
            HttpResponseMessage resp = await clientHttp.GetAsync("status/200");

            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("Ok");
                return Ok();
            }
            else
            {
                _logger.LogInformation("BadRequest");
                return StatusCode((int)resp.StatusCode);
            }
        }
    }
}
