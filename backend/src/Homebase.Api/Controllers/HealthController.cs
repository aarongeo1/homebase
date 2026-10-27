using System.Threading.Tasks;
using Homebase.Api.Contracts;
using Homebase.Domain.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Homebase.Api.Controllers
{
    [ApiController]
    [Route("api/health")]
    public sealed class HealthController : ControllerBase
    {
        private readonly IDatabaseHealthCheck _health;

        public HealthController(IDatabaseHealthCheck health)
        {
            _health = health;
        }

        [HttpGet("")]
        public async Task<HealthDto> Get()
        {
            var databaseUp = await _health.CanConnectAsync().ConfigureAwait(false);
            return new HealthDto { Status = "ok", Database = databaseUp ? "ok" : "unavailable" };
        }
    }
}
