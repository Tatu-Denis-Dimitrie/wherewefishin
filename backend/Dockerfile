# =============================================
# WhereWeFishin Backend API - Multi-stage build
# =============================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY WhereWeFishin.sln .
COPY WhereWeFishin.API/WhereWeFishin.API.csproj WhereWeFishin.API/
COPY WhereWeFishin.Core/WhereWeFishin.Core.csproj WhereWeFishin.Core/
COPY WhereWeFishin.Database/WhereWeFishin.Database.csproj WhereWeFishin.Database/
COPY WhereWeFishin.Tests/WhereWeFishin.Tests.csproj WhereWeFishin.Tests/

# Restore dependencies
RUN dotnet restore

# Copy remaining source code
COPY . .

# Build in Release mode
RUN dotnet publish WhereWeFishin.API/WhereWeFishin.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for Docker healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Create uploads directory
RUN mkdir -p /app/uploads

# Copy published output
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production



ENTRYPOINT ["dotnet", "WhereWeFishin.API.dll"]
