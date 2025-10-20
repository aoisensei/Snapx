using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Snapx.Application.Interfaces
{
    public interface IVideoProcessor
    {
        Task<string> RemoveTikTokWatermarkAsync(string inputPath);
    }
}
