using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Snapx.Application.Interfaces
{
    public interface ITempCleaner
    {
        Task CleanupTemp(string filePath);
    }
}
