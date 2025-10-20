using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Snapx.Domain.DTOs
{
    public class MediaDownloadRequestDto
    {
        public string Url { get; set; } = string.Empty;
    }

    public class MediaDownloadResponseDto
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
    }
}
