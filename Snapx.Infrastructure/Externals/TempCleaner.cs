using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Snapx.Application.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Snapx.Infrastructure.Externals
{
    public class TempCleaner : BackgroundService, ITempCleaner
    {
        private readonly ConcurrentQueue<(string FilePath, DateTime DeleteAt)> _cleanupQueue = new();
        private readonly ILogger<TempCleaner> _logger;

        public TempCleaner(ILogger<TempCleaner> logger)
        {
            _logger = logger;
        }

        public void ScheduleCleanup(string filePath, TimeSpan delay)
        {
            var deleteAt = DateTime.UtcNow.Add(delay);
            _cleanupQueue.Enqueue((filePath, deleteAt));
            _logger.LogInformation("Scheduled cleanup for {FilePath} at {DeleteAt}", filePath, deleteAt);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TempCleaner background service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var itemsToRetry = new List<(string FilePath, DateTime DeleteAt)>();

                    while (_cleanupQueue.TryDequeue(out var item))
                    {
                        if (now >= item.DeleteAt)
                        {
                            try
                            {
                                if (File.Exists(item.FilePath))
                                {
                                    File.Delete(item.FilePath);
                                    _logger.LogInformation("Deleted temporary file: {FilePath}", item.FilePath);
                                }
                                else
                                {
                                    _logger.LogWarning("File already deleted or not found: {FilePath}", item.FilePath);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to delete file: {FilePath}", item.FilePath);
                            }
                        }
                        else
                        {
                            itemsToRetry.Add(item);
                        }
                    }

                    // Đưa các item chưa đến giờ xóa trở lại queue
                    foreach (var item in itemsToRetry)
                    {
                        _cleanupQueue.Enqueue(item);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in TempCleaner background service");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }

            _logger.LogInformation("TempCleaner background service stopped");
        }
    }


}
