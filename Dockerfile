# ----------------------------
# Build stage
# ----------------------------
    FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
    WORKDIR /src
    
    # Copy project files
    COPY ["Snapx.Api/Snapx.Api.csproj", "Snapx.Api/"]
    COPY ["Snapx.Application/Snapx.Application.csproj", "Snapx.Application/"]
    COPY ["Snapx.Domain/Snapx.Domain.csproj", "Snapx.Domain/"]
    COPY ["Snapx.Infrastructure/Snapx.Infrastructure.csproj", "Snapx.Infrastructure/"]
    
    # Restore dependencies
    RUN dotnet restore "Snapx.Api/Snapx.Api.csproj"
    
    # Copy all source code and build
    COPY . .
    WORKDIR "/src/Snapx.Api"
    RUN dotnet publish "Snapx.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false
    
    # ----------------------------
    # Runtime stage
    # ----------------------------
    FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
    WORKDIR /app
    
    # Install ffmpeg + latest yt-dlp
    RUN apt-get update && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        ffmpeg \
        && rm -rf /var/lib/apt/lists/*
    
    # Install latest yt-dlp directly from official GitHub
    RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp \
        && chmod a+rx /usr/local/bin/yt-dlp
    
    # Copy published app
    COPY --from=build /app/publish .
    
    # Prepare TempStorage folder
    RUN mkdir -p /app/TempStorage
    
    # Environment setup
    ENV ASPNETCORE_URLS=http://+:80
    ENV ASPNETCORE_ENVIRONMENT=Production
    
    EXPOSE 80
    
    # Optional: Auto-update yt-dlp on container start
    ENTRYPOINT bash -c "yt-dlp -U || true && dotnet Snapx.Api.dll"
    