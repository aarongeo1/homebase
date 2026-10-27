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
    [Route("api/scenes")]
    public sealed class ScenesController : ControllerBase
    {
        private readonly SceneService _scenes;

        public ScenesController(SceneService scenes)
        {
            _scenes = scenes;
        }

        [HttpGet("")]
        public async Task<List<SceneDto>> List()
        {
            var names = await _scenes.NamesAsync().ConfigureAwait(false);
            var scenes = await _scenes.ListAsync().ConfigureAwait(false);
            return scenes.Select(s => ApiMapper.ToDto(s, names)).ToList();
        }

        [HttpPost("{sceneId}/activate")]
        public async Task<SceneActivationDto> Activate(string sceneId)
        {
            var activation = await _scenes.ActivateAsync(sceneId).ConfigureAwait(false);
            return ApiMapper.ToDto(activation, await _scenes.NamesAsync().ConfigureAwait(false));
        }
    }
}
