using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Homebase.Api.Contracts;
using Homebase.Api.Mapping;
using Homebase.Application;
using Homebase.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Homebase.Api.Controllers
{
    [ApiController]
    [Route("api/automations")]
    public sealed class AutomationsController : ControllerBase
    {
        private readonly AutomationService _automations;

        public AutomationsController(AutomationService automations)
        {
            _automations = automations;
        }

        [HttpGet("")]
        public async Task<List<AutomationDto>> List()
        {
            var names = await _automations.NamesAsync().ConfigureAwait(false);
            var automations = await _automations.ListAsync().ConfigureAwait(false);
            return automations.Select(a => ApiMapper.ToDto(a, names)).ToList();
        }

        // Literal segments outrank {parameters} in attribute routing, so "runs" is never read as an automationId.
        [HttpGet("runs")]
        public async Task<List<AutomationRunDto>> Runs([FromQuery] int? limit = null)
        {
            var runs = await _automations.ListRunsAsync(limit).ConfigureAwait(false);
            return runs.Select(ApiMapper.ToDto).ToList();
        }

        [HttpGet("{automationId}")]
        public async Task<AutomationDto> Get(string automationId)
        {
            var automation = await _automations.GetAsync(automationId).ConfigureAwait(false);
            return ApiMapper.ToDto(automation, await _automations.NamesAsync().ConfigureAwait(false));
        }

        [HttpPut("{automationId}/enabled")]
        public async Task<AutomationDto> SetEnabled(string automationId, [FromBody] SetEnabledRequestDto request)
        {
            if (!request.Enabled.HasValue)
            {
                throw new DomainException(ErrorCodes.ValidationFailed, "Body must be { \"enabled\": true|false }.");
            }

            var automation = await _automations.SetEnabledAsync(automationId, request.Enabled.Value).ConfigureAwait(false);
            return ApiMapper.ToDto(automation, await _automations.NamesAsync().ConfigureAwait(false));
        }

        [HttpPost("{automationId}/simulate")]
        public async Task<SimulationDto> Simulate(string automationId)
        {
            var simulation = await _automations.SimulateAsync(automationId).ConfigureAwait(false);
            return ApiMapper.ToDto(simulation, await _automations.NamesAsync().ConfigureAwait(false));
        }
    }
}
