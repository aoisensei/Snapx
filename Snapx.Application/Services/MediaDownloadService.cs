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

            _cleaner.ScheduleCleanup(outputFile, TimeSpan.FromMinutes(5));

            return new MediaDownloadResponseDto
            {
                FilePath = outputFile,
                FileName = Path.GetFileName(outputFile),
                Platform = platform
            };
        }

        private string DetectPlatform(string url)
        {
            url = url.ToLower();

            if (url.Contains("tiktok.com"))
                return "TikTok";

            if (url.Contains("youtube.com") || url.Contains("youtu.be"))
                return "YouTube";

            if (url.Contains("twitter.com") || url.Contains("x.com"))
                return "Twitter";

            if (url.Contains("instagram.com"))
                return "Instagram";

            if (url.Contains("facebook.com") || url.Contains("fb.watch"))
                return "Facebook";

            if (url.Contains("reddit.com"))
                return "Reddit";

            if (url.Contains("twitch.tv") || url.Contains("clips.twitch.tv"))
                return "Twitch";

            if (url.Contains("pornhub.com"))
                return "Pornhub";

            if (url.Contains("xvideos.com"))
                return "XVideos";

            if (url.Contains("xhamster.com"))
                return "XHamster";

            if (url.Contains("spankbang.com") || url.Contains("fr.spankbang.com"))
                return "SpankBang";

            throw new Exception("Unsupported URL");
        }


    }
}
