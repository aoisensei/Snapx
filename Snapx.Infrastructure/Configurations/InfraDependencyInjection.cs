using Microsoft.Extensions.DependencyInjection;
using Snapx.Application.Interfaces;
using Snapx.Application.Services;
using Snapx.Infrastructure.Externals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Snapx.Infrastructure.Configurations
{
    public static class InfraDependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddScoped<IMediaDownloadService, MediaDownloadService>();
            services.AddScoped<IVideoDownloader, YtDlpDownloader>();
            services.AddScoped<IVideoProcessor, FfmpegProcessor>();
            services.AddSingleton<ITempCleaner, TempCleaner>();
            services.AddHostedService<TempCleaner>(sp => (TempCleaner)sp.GetRequiredService<ITempCleaner>());
            return services;
        }
    }
}
