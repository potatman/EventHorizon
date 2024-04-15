FROM mcr.microsoft.com/dotnet/aspnet:7.0-alpine
RUN apk add --no-cache tzdata
WORKDIR /app
COPY . .
ENTRYPOINT ["dotnet", "Insperex.EventHorizon.Tool.LegacyMigration.dll"]
