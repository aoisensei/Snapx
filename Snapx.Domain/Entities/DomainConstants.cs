using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Snapx.Domain.Entities
{
    public static class DomainConstants
    {
        public static readonly string[] AllowedHosts = new[]
        {
            "youtube.com", "youtu.be",
            "tiktok.com", "vm.tiktok.com",
            "twitter.com", "x.com"
        };
    }
}
