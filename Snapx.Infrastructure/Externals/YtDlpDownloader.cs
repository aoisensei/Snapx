using Snapx.Application.Interfaces;
using Snapx.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Snapx.Infrastructure.Externals
{
    public class YtDlpDownloader : IVideoDownloader
    {
        private readonly string _tempFolder = Path.Combine(AppContext.BaseDirectory, "TempStorage");
        private readonly string _ytDlpPath;
        private readonly string _ffmpegPath;

        public YtDlpDownloader()
        {
            // Allow override via environment variables
            var envYtDlp = Environment.GetEnvironmentVariable("YTDLP_PATH");
            var envFfmpeg = Environment.GetEnvironmentVariable("FFMPEG_PATH");

            // Fallbacks: prefer PATH tools (Linux: yt-dlp / ffmpeg), then Windows Tools folder
            if (!string.IsNullOrWhiteSpace(envYtDlp))
            {
                _ytDlpPath = envYtDlp;
            }
            else if (OperatingSystem.IsWindows())
            {
                var baseDir = AppContext.BaseDirectory;
                var winToolPath = Path.Combine(baseDir, "Tools", "yt-dlp.exe");
                _ytDlpPath = File.Exists(winToolPath) ? winToolPath : "yt-dlp.exe";
            }
            else
            {
                _ytDlpPath = "yt-dlp";
            }

            if (!string.IsNullOrWhiteSpace(envFfmpeg))
            {
                _ffmpegPath = envFfmpeg;
            }
            else if (OperatingSystem.IsWindows())
            {
                var baseDir = AppContext.BaseDirectory;
                var winFfmpegPath = Path.Combine(baseDir, "Tools", "ffmpeg.exe");
                _ffmpegPath = File.Exists(winFfmpegPath) ? winFfmpegPath : "ffmpeg.exe";
            }
            else
            {
                _ffmpegPath = "ffmpeg";
            }
        }

        public async Task<string> DownloadAsync(string url)
        {
            if (!Directory.Exists(_tempFolder))
            {
                Directory.CreateDirectory(_tempFolder);
            }

            var outputFile = Path.Combine(_tempFolder, $"download_{Guid.NewGuid():N}.%(ext)s");

            var psi = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = $"-o \"{outputFile}\" \"{url}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException("Failed to start yt-dlp process.");

            string stdOut = await process.StandardOutput.ReadToEndAsync();
            string stdErr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception($"yt-dlp failed: {stdErr}");

            // Tìm file vừa tải về
            var downloadedFiles = Directory.GetFiles(_tempFolder)
                                           .OrderByDescending(File.GetCreationTimeUtc)
                                           .ToList();

            if (!downloadedFiles.Any())
                throw new Exception("No file was downloaded.");

            return downloadedFiles.First();
        }

        public async Task<string> DownloadAsync(string url, string formatId, string fileType)
        {
            if (!Directory.Exists(_tempFolder))
            {
                Directory.CreateDirectory(_tempFolder);
            }

            string extension = fileType.ToLower() == "mp3" ? "mp3" : "mp4";
            var outputTemplate = Path.Combine(_tempFolder, $"download_{Guid.NewGuid():N}.%(ext)s");

            string args;
            if (extension == "mp3")
            {
                // If user supplies a formatId, try to use it, otherwise bestaudio
                var formatSelector = string.IsNullOrWhiteSpace(formatId) ? "bestaudio" : formatId;
                args = $"-o \"{outputTemplate}\" -f {formatSelector} --extract-audio --audio-format mp3 --audio-quality 0 --ffmpeg-location \"{_ffmpegPath}\" \"{url}\"";
            }
            else
            {
                var formatSelector = string.IsNullOrWhiteSpace(formatId) ? "bestvideo*+bestaudio/best" : formatId;
                args = $"-o \"{outputTemplate}\" -f {formatSelector} --ffmpeg-location \"{_ffmpegPath}\" \"{url}\"";
            }

            var psi = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException("Failed to start yt-dlp process.");

            string stdOut = await process.StandardOutput.ReadToEndAsync();
            string stdErr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception($"yt-dlp failed: {stdErr}");

            var files = Directory.GetFiles(_tempFolder)
                                 .OrderByDescending(File.GetCreationTimeUtc)
                                 .ToList();
            if (!files.Any())
                throw new Exception("No file was downloaded.");

            // Prefer file with the desired extension
            var matched = files.FirstOrDefault(f => Path.GetExtension(f).Equals($".{extension}", StringComparison.OrdinalIgnoreCase))
                         ?? files.First();
            return matched;
        }

        public async Task<(string title, string uploader, List<(string formatId, string? formatNote, string ext, long? filesize)>)> GetFormatsAsync(string url)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = $"--dump-json \"{url}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException("Failed to start yt-dlp process.");

            string stdOut = await process.StandardOutput.ReadToEndAsync();
            string stdErr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdOut))
                throw new Exception($"yt-dlp analyze failed: {stdErr}");

            using var doc = JsonDocument.Parse(stdOut);
            var root = doc.RootElement;
            string title = root.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            string uploader = root.TryGetProperty("uploader", out var u) ? u.GetString() ?? string.Empty : string.Empty;

            var results = new List<(string, string?, string, long?)>();
            if (root.TryGetProperty("formats", out var formats) && formats.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in formats.EnumerateArray())
                {
                    var formatId = f.TryGetProperty("format_id", out var fid) ? fid.GetString() ?? string.Empty : string.Empty;
                    var formatNote = f.TryGetProperty("format_note", out var fn) ? fn.GetString() : null;
                    var ext = f.TryGetProperty("ext", out var fe) ? fe.GetString() ?? string.Empty : string.Empty;
                    long? filesize = null;
                    if (f.TryGetProperty("filesize", out var fs) && fs.ValueKind == JsonValueKind.Number)
                    {
                        if (fs.TryGetInt64(out var size)) filesize = size;
                    }
                    else if (f.TryGetProperty("filesize_approx", out var fsa) && fsa.ValueKind == JsonValueKind.Number)
                    {
                        if (fsa.TryGetInt64(out var sizeApprox)) filesize = sizeApprox;
                    }

                    if (!string.IsNullOrWhiteSpace(formatId) && !string.IsNullOrWhiteSpace(ext))
                    {
                        results.Add((formatId, formatNote, ext, filesize));
                    }
                }
            }

            return (title, uploader, results);
        }

    }
}
