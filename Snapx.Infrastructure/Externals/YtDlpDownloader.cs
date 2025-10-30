using Snapx.Application.Interfaces;
using Snapx.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
                if (File.Exists("/usr/local/bin/yt-dlp")) _ytDlpPath = "/usr/local/bin/yt-dlp";
                else if (File.Exists("/usr/bin/yt-dlp")) _ytDlpPath = "/usr/bin/yt-dlp";
                else _ytDlpPath = "yt-dlp";
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
                if (File.Exists("/usr/bin/ffmpeg")) _ffmpegPath = "/usr/bin/ffmpeg";
                else if (File.Exists("/usr/local/bin/ffmpeg")) _ffmpegPath = "/usr/local/bin/ffmpeg";
                else _ffmpegPath = "ffmpeg";
            }

            // Log paths for debug visibility
            Console.WriteLine($"[YtDlp] yt-dlp path = {_ytDlpPath}");
            Console.WriteLine($"[YtDlp] ffmpeg path = {_ffmpegPath}");
        }

        public async Task<string> DownloadAsync(string url)
        {
            if (!Directory.Exists(_tempFolder))
                Directory.CreateDirectory(_tempFolder);

            var outputFile = Path.Combine(_tempFolder, $"download_{Guid.NewGuid():N}.%(ext)s");

            var psi = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = $"--ffmpeg-location \"{_ffmpegPath}\" -o \"{outputFile}\" \"{url}\"",
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

            var downloadedFiles = Directory.GetFiles(_tempFolder)
                                           .OrderByDescending(File.GetCreationTimeUtc)
                                           .ToList();

            if (!downloadedFiles.Any())
                throw new Exception("No file was downloaded.");

            return downloadedFiles.First();
        }

        public async Task<string> DownloadAsync(string url, string formatId, string fileType)
        {
            if (!Directory.Exists(_tempFolder)) Directory.CreateDirectory(_tempFolder);
            string extension = (fileType.ToLower() == "mp3") ? "mp3" : "mp4";
            var outputTemplate = Path.Combine(_tempFolder, $"download_{Guid.NewGuid():N}.%(ext)s");

            string TryFormatSelector(string selector)
            {
                return $"-f {selector} -o \"{outputTemplate}\" --merge-output-format mp4 --ffmpeg-location \"{_ffmpegPath}\" --no-playlist --retries 10 --fragment-retries 10 --force-ipv4 \"{url}\"";
            }

            string args;
            if (extension == "mp3")
            {
                args = !string.IsNullOrWhiteSpace(formatId)
                    ? $"-f {formatId} -x --audio-format mp3 --audio-quality 0 -o \"{outputTemplate}\" --ffmpeg-location \"{_ffmpegPath}\" --no-playlist --retries 10 --fragment-retries 10 --force-ipv4 \"{url}\""
                    : $"-x --audio-format mp3 --audio-quality 0 -o \"{outputTemplate}\" --ffmpeg-location \"{_ffmpegPath}\" --no-playlist --retries 10 --fragment-retries 10 --force-ipv4 \"{url}\"";

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
                if (process == null) throw new InvalidOperationException("Failed to start yt-dlp process.");
                string stdOut = await process.StandardOutput.ReadToEndAsync();
                string stdErr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    var files = Directory.GetFiles(_tempFolder).OrderByDescending(File.GetCreationTimeUtc).ToList();
                    if (!files.Any()) throw new Exception("No file was downloaded.");
                    var matched = files.FirstOrDefault(f => Path.GetExtension(f).Equals($".mp3", StringComparison.OrdinalIgnoreCase)) ?? files.First();
                    return matched;
                }

                // Fallback logic...
                var formatProbe = await GetFormatsAsync(url);
                var fBest = formatProbe.Item4
                    .Where(f => !string.IsNullOrWhiteSpace(f.acodec) && f.acodec != "none" && !string.IsNullOrWhiteSpace(f.vcodec) && f.vcodec != "none" && (f.height ?? 0) >= 144)
                    .OrderBy(x => x.height ?? int.MaxValue)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(fBest.formatId))
                    throw new Exception($"yt-dlp failed: {stdErr}");

                var outputVideo = Path.Combine(_tempFolder, $"fallback_{Guid.NewGuid():N}.mp4");
                var videoArgs = $"-f {fBest.formatId} -o \"{outputVideo}\" --merge-output-format mp4 --ffmpeg-location \"{_ffmpegPath}\" --no-playlist --retries 10 --fragment-retries 10 --force-ipv4 \"{url}\"";

                var vpsi = new ProcessStartInfo
                {
                    FileName = _ytDlpPath,
                    Arguments = videoArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var procVid = Process.Start(vpsi))
                {
                    if (procVid == null) throw new InvalidOperationException("yt-dlp video fallback fail");
                    await procVid.StandardOutput.ReadToEndAsync();
                    await procVid.StandardError.ReadToEndAsync();
                    await procVid.WaitForExitAsync();
                }

                if (!File.Exists(outputVideo)) throw new Exception("Video fallback download failed");

                var outputMp3 = Path.Combine(_tempFolder, $"{Guid.NewGuid():N}.mp3");
                var ffpsi = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = $"-y -i \"{outputVideo}\" -vn -acodec libmp3lame -q:a 2 \"{outputMp3}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var ff = Process.Start(ffpsi))
                {
                    if (ff == null) throw new InvalidOperationException("ffmpeg fallback fail");
                    await ff.StandardOutput.ReadToEndAsync();
                    await ff.StandardError.ReadToEndAsync();
                    await ff.WaitForExitAsync();
                }

                if (!File.Exists(outputMp3)) throw new Exception("FFmpeg convert fallback failed");
                return outputMp3;
            }
            else
            {
                args = !string.IsNullOrWhiteSpace(formatId)
                    ? TryFormatSelector(formatId)
                    : TryFormatSelector("\"bestvideo[acodec!=none][vcodec!=none]/best[acodec!=none][vcodec!=none]/best\"");

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
                if (process == null) throw new InvalidOperationException("Failed to start yt-dlp process.");
                string stdOut = await process.StandardOutput.ReadToEndAsync();
                string stdErr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0 && stdErr.Contains("Requested format is not available"))
                {
                    var fallbackArgs = TryFormatSelector("best");
                    psi.Arguments = fallbackArgs;
                    using var retryProc = Process.Start(psi);
                    if (retryProc == null) throw new InvalidOperationException("Retry failed to start yt-dlp process.");
                    stdOut = await retryProc.StandardOutput.ReadToEndAsync();
                    stdErr = await retryProc.StandardError.ReadToEndAsync();
                    await retryProc.WaitForExitAsync();
                    if (retryProc.ExitCode != 0)
                        throw new Exception($"yt-dlp failed (retry): {stdErr}");
                }
                else if (process.ExitCode != 0)
                {
                    throw new Exception($"yt-dlp failed: {stdErr}");
                }

                var files = Directory.GetFiles(_tempFolder).OrderByDescending(File.GetCreationTimeUtc).ToList();
                if (!files.Any()) throw new Exception("No file was downloaded.");
                var matched = files.FirstOrDefault(f => Path.GetExtension(f).Equals($".{extension}", StringComparison.OrdinalIgnoreCase))
                             ?? files.First();
                return matched;
            }
        }

        public async Task<(string title, string uploader, double? durationSeconds, List<(string formatId, string? formatNote, string ext, long? filesize, double? tbrKbps, string? vcodec, string? acodec, int? height)>)> GetFormatsAsync(string url)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = $"--ffmpeg-location \"{_ffmpegPath}\" --dump-json \"{url}\"",
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

            double? durationSeconds = null;
            if (root.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number)
            {
                if (d.TryGetDouble(out var dur)) durationSeconds = dur;
            }

            var results = new List<(string, string?, string, long?, double?, string?, string?, int?)>();
            if (root.TryGetProperty("formats", out var formats) && formats.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in formats.EnumerateArray())
                {
                    var formatId = f.TryGetProperty("format_id", out var fid) ? fid.GetString() ?? string.Empty : string.Empty;
                    var formatNote = f.TryGetProperty("format_note", out var fn) ? fn.GetString() : null;
                    var ext = f.TryGetProperty("ext", out var fe) ? fe.GetString() ?? string.Empty : string.Empty;
                    var vcodec = f.TryGetProperty("vcodec", out var vc) ? vc.GetString() : null;
                    var acodec = f.TryGetProperty("acodec", out var ac) ? ac.GetString() : null;
                    int? height = null;
                    if (f.TryGetProperty("height", out var h) && h.ValueKind == JsonValueKind.Number)
                    {
                        if (h.TryGetInt32(out var hh)) height = hh;
                    }
                    long? filesize = null;
                    if (f.TryGetProperty("filesize", out var fs) && fs.ValueKind == JsonValueKind.Number)
                    {
                        if (fs.TryGetInt64(out var size)) filesize = size;
                    }
                    else if (f.TryGetProperty("filesize_approx", out var fsa) && fsa.ValueKind == JsonValueKind.Number)
                    {
                        if (fsa.TryGetInt64(out var sizeApprox)) filesize = sizeApprox;
                    }
                    double? tbrKbps = null;
                    if (f.TryGetProperty("tbr", out var tbr) && tbr.ValueKind == JsonValueKind.Number)
                    {
                        if (tbr.TryGetDouble(out var tbrVal)) tbrKbps = tbrVal;
                    }

                    if (!string.IsNullOrWhiteSpace(formatId) && !string.IsNullOrWhiteSpace(ext))
                    {
                        results.Add((formatId, formatNote, ext, filesize, tbrKbps, vcodec, acodec, height));
                    }
                }
            }

            return (title, uploader, durationSeconds, results);
        }
    }
}
