FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Create upload directory with proper permissions
RUN mkdir -p /var/www/uploads && \
    chown -R app:app /var/www/uploads && \
    chmod -R 755 /var/www/uploads

# Create non-root user for security
RUN groupadd -r app && useradd -r -g app app

COPY --from=build /app .

# Change ownership of app files
RUN chown -R app:app /app

# Switch to non-root user
USER app

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "WebApplication1.dll"]