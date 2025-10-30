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

        public async Task<MediaAnalyzeResponseDto> AnalyzeAsync(MediaAnalyzeRequestDto request)
        {
            string url = request.Url.Trim();
            var (title, uploader, durationSeconds, formats) = await _downloader.GetFormatsAsync(url);
            var options = new List<MediaAnalyzeFormatDto>();
            // AUDIO
            var audio = formats
                .Where(f => !string.IsNullOrWhiteSpace(f.acodec) && f.acodec != "none" && (string.IsNullOrWhiteSpace(f.vcodec) || f.vcodec == "none"))
                .OrderByDescending(f => f.tbrKbps ?? 0)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(audio.formatId))
            {
                options.Add(new MediaAnalyzeFormatDto
                {
                    FormatId = audio.formatId,
                    FormatNote = "Audio MP3",
                    Ext = audio.ext,
                    Filesize = audio.filesize,
                    EstimatedSizeBytes = audio.filesize,
                    DisplaySize = FormatBytes(audio.filesize),
                });
            }
            // VIDEO: SD/HD/FHD
            (int, string)[] heights = { (480, "SD (≈480p)"), (720, "HD (720p)"), (1080, "Full HD (1080p)") };
            foreach (var (minHeight, label) in heights)
            {
                var f = formats
                    .Where(x => (x.height ?? 0) >= minHeight && !string.IsNullOrWhiteSpace(x.vcodec) && x.vcodec != "none" && !string.IsNullOrWhiteSpace(x.acodec) && x.acodec != "none")
                    .OrderBy(x => x.height)
                    .ThenByDescending(x => x.tbrKbps ?? 0)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(f.formatId) && options.All(opt => opt.FormatId != f.formatId))
                {
                    options.Add(new MediaAnalyzeFormatDto
                    {
                        FormatId = f.formatId,
                        FormatNote = label,
                        Ext = f.ext,
                        Filesize = f.filesize,
                        EstimatedSizeBytes = f.filesize,
                        DisplaySize = FormatBytes(f.filesize),
                    });
                }
            }
            return new MediaAnalyzeResponseDto
            {
                Title = title,
                Uploader = uploader,
                Formats = options
            };
        }

        private static string? FormatBytes(long? bytes)
        {
            if (!bytes.HasValue || bytes.Value <= 0) return null;
            double b = bytes.Value;
            string[] units = new[] { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (b >= 1024 && i < units.Length - 1)
            {
                b /= 1024;
                i++;
            }
            return $"{b:0.##} {units[i]}";
        }


        public async Task<MediaDownloadResponseDto> DownloadVideoAsync(MediaDownloadRequestDto request)
        {
            string url = request.Url.Trim();
            string platform = DetectPlatform(url);
            string fileType = request.FileType?.Trim().ToLower();
            string formatId = request.FormatId?.Trim() ?? string.Empty;

            string tempFile;
            if (fileType == "mp3")
            {
                tempFile = await _downloader.DownloadAsync(url, formatId, "mp3");
            }
            else
            {
                tempFile = await _downloader.DownloadAsync(url, formatId, "mp4");
            }

            string outputFile = tempFile;

            //if (platform == "TikTok")
            //    outputFile = await _processor.RemoveTikTokWatermarkAsync(tempFile);

            _cleaner.ScheduleCleanup(outputFile, TimeSpan.FromMinutes(5));

            var contentType = (Path.GetExtension(outputFile).ToLower()) switch
            {
                ".mp3" => "audio/mpeg",
                ".webm" => "video/webm",
                _ => "video/mp4"
            };

            return new MediaDownloadResponseDto
            {
                FilePath = outputFile,
                FileName = Path.GetFileName(outputFile),
                Platform = platform,
                ContentType = contentType
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
