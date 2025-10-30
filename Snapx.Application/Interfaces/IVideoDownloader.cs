using Snapx.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Snapx.Application.Interfaces
{
    public interface IVideoDownloader
    {
        Task<string> DownloadAsync(string url);
        Task<string> DownloadAsync(string url, string formatId, string fileType);
        Task<(string title, string uploader, double? durationSeconds, List<(string formatId, string? formatNote, string ext, long? filesize, double? tbrKbps, string? vcodec, string? acodec, int? height)>)> GetFormatsAsync(string url);
    }
}
