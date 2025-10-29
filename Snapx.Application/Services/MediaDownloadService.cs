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
            var (title, uploader, formats) = await _downloader.GetFormatsAsync(url);

            // Build simplified choices for FE: Audio MP3, SD, HD, Full HD, and higher if available
            var simplified = new List<MediaAnalyzeFormatDto>();

            bool IsAudio((string formatId, string? formatNote, string ext, long? filesize) f)
            {
                var note = (f.formatNote ?? string.Empty).ToLower();
                return note.Contains("audio") || f.ext.Equals("m4a", StringComparison.OrdinalIgnoreCase) || f.ext.Equals("mp3", StringComparison.OrdinalIgnoreCase) || f.ext.Equals("webm", StringComparison.OrdinalIgnoreCase);
            }

            int? ParseHeight((string formatId, string? formatNote, string ext, long? filesize) f)
            {
                var note = f.formatNote ?? string.Empty;
                var digits = System.Text.RegularExpressions.Regex.Match(note, @"(\d{3,4})p");
                if (digits.Success && int.TryParse(digits.Groups[1].Value, out var h)) return h;
                // Special cases for sd/hd
                if (f.formatId.Equals("sd", StringComparison.OrdinalIgnoreCase)) return 480;
                if (f.formatId.Equals("hd", StringComparison.OrdinalIgnoreCase)) return 720;
                return null;
            }

            // AUDIO MP3
            var audio = formats.FirstOrDefault(IsAudio);
            if (!string.IsNullOrEmpty(audio.formatId))
            {
                simplified.Add(new MediaAnalyzeFormatDto
                {
                    FormatId = audio.formatId,
                    FormatNote = "Audio MP3",
                    Ext = "mp3",
                    Filesize = audio.filesize
                });
            }

            // Helper to pick best format by minimum height target
            MediaAnalyzeFormatDto? PickByMinHeight(int minHeight, string label)
            {
                var candidates = formats
                    .Select(f => new { F = f, H = ParseHeight(f) })
                    .Where(x => x.H.HasValue && x.H.Value >= minHeight)
                    .OrderBy(x => x.H!.Value)
                    .ToList();
                var picked = candidates.FirstOrDefault();
                if (picked == null) return null;
                return new MediaAnalyzeFormatDto
                {
                    FormatId = picked.F.formatId,
                    FormatNote = label,
                    Ext = "mp4",
                    Filesize = picked.F.filesize
                };
            }

            // SD (~480p)
            var sd = formats.FirstOrDefault(f => f.formatId.Equals("sd", StringComparison.OrdinalIgnoreCase))
                     ;
            if (!string.IsNullOrEmpty(sd.formatId))
            {
                simplified.Add(new MediaAnalyzeFormatDto { FormatId = sd.formatId, FormatNote = "SD (≈480p)", Ext = "mp4", Filesize = sd.filesize });
            }
            else
            {
                var sdPicked = PickByMinHeight(480, "SD (≈480p)");
                if (sdPicked != null) simplified.Add(sdPicked);
            }

            // HD (720p)
            var hd = formats.FirstOrDefault(f => f.formatId.Equals("hd", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(hd.formatId))
            {
                simplified.Add(new MediaAnalyzeFormatDto { FormatId = hd.formatId, FormatNote = "HD (720p)", Ext = "mp4", Filesize = hd.filesize });
            }
            else
            {
                var hdPicked = PickByMinHeight(720, "HD (720p)");
                if (hdPicked != null) simplified.Add(hdPicked);
            }

            // Full HD (1080p)
            var fhd = formats
                .Select(f => new { F = f, H = ParseHeight(f) })
                .Where(x => x.H == 1080)
                .OrderByDescending(x => x.F.filesize ?? 0)
                .FirstOrDefault();
            if (fhd != null)
            {
                simplified.Add(new MediaAnalyzeFormatDto { FormatId = fhd.F.formatId, FormatNote = "Full HD (1080p)", Ext = "mp4", Filesize = fhd.F.filesize });
            }

            // 2K (1440p), 4K (2160p), 8K (4320p)
            int[] heights = new[] { 1440, 2160, 4320 };
            string[] labels = new[] { "2K (1440p)", "4K (2160p)", "8K (4320p)" };
            for (int i = 0; i < heights.Length; i++)
            {
                var pick = formats
                    .Select(f => new { F = f, H = ParseHeight(f) })
                    .Where(x => x.H == heights[i])
                    .OrderByDescending(x => x.F.filesize ?? 0)
                    .FirstOrDefault();
                if (pick != null)
                {
                    simplified.Add(new MediaAnalyzeFormatDto { FormatId = pick.F.formatId, FormatNote = labels[i], Ext = "mp4", Filesize = pick.F.filesize });
                }
            }

            // De-duplicate by FormatNote label, keep first occurrence
            var dedup = simplified
                .GroupBy(x => x.FormatNote)
                .Select(g => g.First())
                .ToList();

            return new MediaAnalyzeResponseDto
            {
                Title = title,
                Uploader = uploader,
                Formats = dedup
            };
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
