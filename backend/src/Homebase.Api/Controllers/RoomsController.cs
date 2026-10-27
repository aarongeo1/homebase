using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Homebase.Api.Contracts;
using Homebase.Api.Mapping;
using Homebase.Application;
using Microsoft.AspNetCore.Mvc;

namespace Homebase.Api.Controllers
{
    [ApiController]
    [Route("api/rooms")]
    public sealed class RoomsController : ControllerBase
    {
        private readonly DeviceService _devices;

        public RoomsController(DeviceService devices)
        {
            _devices = devices;
        }

        [HttpGet("")]
        public async Task<List<RoomDto>> List()
        {
            var rooms = await _devices.ListRoomsAsync().ConfigureAwait(false);
            return rooms.Select(ApiMapper.ToDto).ToList();
        }
    }
}
