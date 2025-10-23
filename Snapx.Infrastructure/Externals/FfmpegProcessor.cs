using Snapx.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Snapx.Infrastructure.Externals
{
    public class FfmpegProcessor : IVideoProcessor
    {

        private readonly string _ffmpegPath;
        private readonly string _ffprobePath;

        public FfmpegProcessor()
        {
            var envFfmpeg = Environment.GetEnvironmentVariable("FFMPEG_PATH");
            var envFfprobe = Environment.GetEnvironmentVariable("FFPROBE_PATH");

            if (!string.IsNullOrWhiteSpace(envFfmpeg))
            {
                _ffmpegPath = envFfmpeg;
            }
            else if (OperatingSystem.IsWindows())
            {
                var baseDir = AppContext.BaseDirectory;
                var winFfmpeg = Path.Combine(baseDir, "Tools", "ffmpeg.exe");
                _ffmpegPath = File.Exists(winFfmpeg) ? winFfmpeg : "ffmpeg.exe";
            }
            else
            {
                _ffmpegPath = "ffmpeg";
            }

            if (!string.IsNullOrWhiteSpace(envFfprobe))
            {
                _ffprobePath = envFfprobe;
            }
            else if (OperatingSystem.IsWindows())
            {
                var baseDir = AppContext.BaseDirectory;
                var winFfprobe = Path.Combine(baseDir, "Tools", "ffprobe.exe");
                _ffprobePath = File.Exists(winFfprobe) ? winFfprobe : "ffprobe.exe";
            }
            else
            {
                _ffprobePath = "ffprobe";
            }
        }

        public async Task<string> RemoveTikTokWatermarkAsync(string inputPath)
        {
            string outputPath = Path.ChangeExtension(inputPath, ".clean.mp4");

            // Lấy duration
            double duration = await GetVideoDurationAsync(inputPath);
            double trimEnd = duration - 1.5; // watermark thường ở 1.5s cuối

            // Sử dụng filter đơn giản hơn và xử lý cả video + audio
            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-y -i \"{inputPath}\" -t {trimEnd} -vf \"crop=iw:ih-60:0:0\" -c:v libx264 -preset fast -crf 23 -c:a aac -b:a 128k \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                throw new InvalidOperationException("Failed to start ffmpeg process.");

            // ĐỌC OUTPUT ĐỒNG THỜI để tránh buffer đầy
            var outputTask = proc.StandardOutput.ReadToEndAsync();
            var errorTask = proc.StandardError.ReadToEndAsync();

            await proc.WaitForExitAsync();

            string stdOut = await outputTask;
            string stdErr = await errorTask;

            if (proc.ExitCode != 0)
            {
                throw new Exception($"FFmpeg failed: {stdErr}");
            }

            if (!File.Exists(outputPath))
                throw new Exception("Watermark removal failed.");

            return outputPath;
        }

        private async Task<double> GetVideoDurationAsync(string file)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{file}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                throw new InvalidOperationException("Failed to start ffprobe process.");

            // ĐỌC OUTPUT ĐỒNG THỜI
            var outputTask = proc.StandardOutput.ReadToEndAsync();
            var errorTask = proc.StandardError.ReadToEndAsync();

            await proc.WaitForExitAsync();

            string output = await outputTask;
            string stdErr = await errorTask;

            if (proc.ExitCode != 0)
            {
                throw new Exception($"FFprobe failed: {stdErr}");
            }

            return double.TryParse(output.Trim(), System.Globalization.NumberStyles.Any,
                                  System.Globalization.CultureInfo.InvariantCulture, out double d) ? d : 0;
        }

    }
}
