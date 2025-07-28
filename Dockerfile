FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet tool install --global dotnet-ef
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copy the published app
COPY --from=build /app .

# Copy EF tools for migrations (optional - for manual migrations)
COPY --from=build /root/.dotnet/tools /root/.dotnet/tools
ENV PATH="$PATH:/root/.dotnet/tools"

ENTRYPOINT ["dotnet", "WebApplication1.dll"]