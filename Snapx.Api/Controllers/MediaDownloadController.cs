using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Snapx.Application.Interfaces;
using Snapx.Domain.DTOs;

namespace Snapx.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MediaDownloadController : ControllerBase
    {
        private readonly IMediaDownloadService _service;

        public MediaDownloadController(IMediaDownloadService service)
        {
            _service = service;
        }

        [HttpPost("media-analyze")]
        public async Task<IActionResult> Analyze([FromBody] MediaAnalyzeRequestDto request)
        {
            var result = await _service.AnalyzeAsync(request);
            return Ok(result);
        }

        [HttpPost("media-download")]
        public async Task<IActionResult> Download([FromBody] MediaDownloadRequestDto request)
        {
            var result = await _service.DownloadVideoAsync(request);
            return PhysicalFile(result.FilePath, result.ContentType, result.FileName);
        }
    }
}
