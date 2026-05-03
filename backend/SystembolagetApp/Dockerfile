# ── Bygg-steg ─────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SystembolagetApp.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Runtime-steg ───────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Skapa mapp för SQLite-databasen (monteras som volym)
RUN mkdir -p /app/data

COPY --from=build /app/publish .

# Peka ConnectionString mot volym-mappen
ENV ConnectionStrings__Default="Data Source=/app/data/systembolaget.db"
ENV ASPNETCORE_URLS="http://+:5000"

EXPOSE 5000

ENTRYPOINT ["dotnet", "SystembolagetApp.dll"]
