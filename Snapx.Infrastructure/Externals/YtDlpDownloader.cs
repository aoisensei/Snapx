using Snapx.Application.Interfaces;
using Snapx.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
                // Rely on ffmpeg available on PATH. Users can still set --ffmpeg-location via env by overriding _ytDlpPath wrapper if needed.
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

    }
}
