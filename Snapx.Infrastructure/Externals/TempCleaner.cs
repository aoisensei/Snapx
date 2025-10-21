using Microsoft.Extensions.Logging;
using Snapx.Application.Interfaces;
using System;
using System.IO;

namespace Snapx.Infrastructure.Externals
{
    public class TempCleaner : ITempCleaner
    {
        private readonly ILogger<TempCleaner> _logger;

        public TempCleaner(ILogger<TempCleaner> logger)
        {
            _logger = logger;
        }

        public async Task CleanupTemp(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted temporary file: {FilePath}", filePath);
                }
                else
                {
                    _logger.LogDebug("File not found (already deleted): {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete temporary file: {FilePath}", filePath);
            }
        }
    }
}