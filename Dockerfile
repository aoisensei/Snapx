# Use the official .NET 8 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the official .NET 8 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["Snapx.Api/Snapx.Api.csproj", "Snapx.Api/"]
COPY ["Snapx.Application/Snapx.Application.csproj", "Snapx.Application/"]
COPY ["Snapx.Domain/Snapx.Domain.csproj", "Snapx.Domain/"]
COPY ["Snapx.Infrastructure/Snapx.Infrastructure.csproj", "Snapx.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "Snapx.Api/Snapx.Api.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/Snapx.Api"
RUN dotnet build "Snapx.Api.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "Snapx.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Install yt-dlp and ffmpeg
FROM base AS final
WORKDIR /app

# Install required packages
RUN apt-get update && apt-get install -y \
    wget \
    python3 \
    python3-pip \
    && rm -rf /var/lib/apt/lists/*

# Install yt-dlp
RUN pip3 install yt-dlp

# Install ffmpeg
RUN apt-get update && apt-get install -y \
    ffmpeg \
    && rm -rf /var/lib/apt/lists/*

# Copy the published application
COPY --from=publish /app/publish .

# Create TempStorage directory
RUN mkdir -p /app/TempStorage

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port
EXPOSE 80

# Start the application
ENTRYPOINT ["dotnet", "Snapx.Api.dll"]
