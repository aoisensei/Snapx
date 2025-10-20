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
    }
}
