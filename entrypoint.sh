#!/bin/bash
set -e

echo "🔄 Updating yt-dlp to latest version..."
yt-dlp -U || echo "⚠️ yt-dlp update failed, continuing..."

echo "🚀 Starting Snapx API..."
exec dotnet Snapx.Api.dll
