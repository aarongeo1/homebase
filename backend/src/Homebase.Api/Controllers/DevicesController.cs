using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Homebase.Api.Contracts;
using Homebase.Api.Infrastructure;
using Homebase.Api.Mapping;
using Homebase.Application;
using Homebase.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Homebase.Api.Controllers
{
    [ApiController]
    [Route("api/devices")]
    public sealed class DevicesController : ControllerBase
    {
        private readonly DeviceService _devices;

        public DevicesController(DeviceService devices)
        {
            _devices = devices;
        }

        [HttpGet("")]
        public async Task<List<DeviceDto>> List([FromQuery] string roomId = null)
        {
            var devices = await _devices.ListAsync(roomId).ConfigureAwait(false);
            return devices.Select(ApiMapper.ToDto).ToList();
        }

        // Literal segments outrank {parameters} in attribute routing, so "discover" is never read as a deviceId.
        [HttpPost("discover")]
        public async Task<DiscoveryDto> Discover(CancellationToken cancellationToken)
        {
            return ApiMapper.ToDto(await _devices.DiscoverAsync(cancellationToken).ConfigureAwait(false));
        }

        [HttpGet("{deviceId}")]
        public async Task<DeviceDto> Get(string deviceId)
        {
            return ApiMapper.ToDto(await _devices.GetAsync(deviceId).ConfigureAwait(false));
        }

        [HttpPut("{deviceId}")]
        public async Task<DeviceDto> Update(string deviceId, [FromBody] DeviceSettingsDto settings)
        {
            return ApiMapper.ToDto(await _devices.UpdateAsync(deviceId, settings.Name, settings.RoomId).ConfigureAwait(false));
        }

        [HttpPost("{deviceId}/commands")]
        public async Task<DeviceDto> Command(string deviceId, [FromBody] DeviceCommandDto command)
        {
            return ApiMapper.ToDto(await _devices.CommandAsync(deviceId, ApiMapper.ToDomain(command)).ConfigureAwait(false));
        }

        [RequireReportToken]
        [HttpPost("{deviceId}/report")]
        public async Task<DeviceDto> Report(string deviceId, [FromBody] DeviceReportDto report)
        {
            return ApiMapper.ToDto(await _devices.ReportAsync(deviceId, ApiMapper.ToDomain(report)).ConfigureAwait(false));
        }

        [HttpPost("{deviceId}/pairing")]
        public async Task<PairingDto> StartPairing(string deviceId, [FromBody] PairingRequestDto request)
        {
            var message = await _devices.StartPairingAsync(deviceId, request.Address).ConfigureAwait(false);
            return new PairingDto { DeviceId = deviceId, Message = message };
        }

        [HttpPost("{deviceId}/pairing/confirm")]
        public async Task<DeviceDto> ConfirmPairing(string deviceId, [FromBody] PairingCodeDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                throw new DomainException(ErrorCodes.ValidationFailed, "Body must be { \"code\": \"<6 characters shown on the TV>\" }.");
            }

            return ApiMapper.ToDto(await _devices.ConfirmPairingAsync(deviceId, request.Code.Trim()).ConfigureAwait(false));
        }
    }
}
