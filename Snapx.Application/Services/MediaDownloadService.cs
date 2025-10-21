using Snapx.Application.Interfaces;
using Snapx.Domain.DTOs;
using Snapx.Domain.Entities;
using Snapx.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Snapx.Application.Services
{
    public class MediaDownloadService : IMediaDownloadService
    {
        private readonly IVideoDownloader _downloader;
        private readonly IVideoProcessor _processor;
        private readonly ITempCleaner _cleaner;

        public MediaDownloadService(IVideoDownloader downloader, IVideoProcessor processor, ITempCleaner cleaner)
        {
            _downloader = downloader;
            _processor = processor;
            _cleaner = cleaner;
        }

        public async Task<MediaDownloadResponseDto> DownloadVideoAsync(MediaDownloadRequestDto request)
        {
            string url = request.Url.Trim();
            string platform = DetectPlatform(url);
            string tempFile = await _downloader.DownloadAsync(url);

            string outputFile = tempFile;

            //if (platform == "TikTok")
            //    outputFile = await _processor.RemoveTikTokWatermarkAsync(tempFile);

            await _cleaner.CleanupTemp(outputFile);

            return new MediaDownloadResponseDto
            {
                FilePath = outputFile,
                FileName = Path.GetFileName(outputFile),
                Platform = platform
            };
        }

        private string DetectPlatform(string url)
        {
            if (url.Contains("tiktok.com")) return "TikTok";
            if (url.Contains("youtube.com") || url.Contains("youtu.be")) return "YouTube";
            if (url.Contains("twitter.com") || url.Contains("x.com")) return "Twitter";
            throw new Exception("Unsupported URL");
        }

    }
}
