using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Snapx.Domain.DTOs
{
    public class MediaAnalyzeRequestDto
    {
        public string Url { get; set; } = string.Empty;
    }

    public class MediaAnalyzeFormatDto
    {
        public string FormatId { get; set; } = string.Empty;
        public string? FormatNote { get; set; }
        public string Ext { get; set; } = string.Empty;
        public long? Filesize { get; set; }
        public long? EstimatedSizeBytes { get; set; }
        public string? DisplaySize { get; set; }
    }

    public class MediaAnalyzeResponseDto
    {
        public string Title { get; set; } = string.Empty;
        public string Uploader { get; set; } = string.Empty;
        public List<MediaAnalyzeFormatDto> Formats { get; set; } = new();
    }

    public class MediaDownloadRequestDto
    {
        public string Url { get; set; } = string.Empty;
        public string FormatId { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty; // mp4 or mp3
    }

    public class MediaDownloadResponseDto
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }
}
