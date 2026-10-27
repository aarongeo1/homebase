using System.Net;
using System.Threading.Tasks;
using Homebase.Api.Contracts;
using Homebase.Api.Infrastructure;
using Homebase.Api.Mapping;
using Homebase.Application;
using Microsoft.AspNetCore.Mvc;

namespace Homebase.Api.Controllers
{
    [ApiController]
    [Route("api/presence")]
    public sealed class PresenceController : ControllerBase
    {
        private readonly PresenceService _presence;
        private readonly ReportToken _token;

        public PresenceController(PresenceService presence, ReportToken token)
        {
            _presence = presence;
            _token = token;
        }

        /// <summary>The report token is only shown to a browser on the Mac itself, never to the rest of the network.</summary>
        [HttpGet("")]
        public async Task<PresenceDto> Get()
        {
            var remote = HttpContext.Connection.RemoteIpAddress;
            var onTheMac = remote != null && IPAddress.IsLoopback(remote);
            return ApiMapper.ToDto(await _presence.StatusAsync().ConfigureAwait(false), onTheMac ? _token.Value : null);
        }
    }
}
