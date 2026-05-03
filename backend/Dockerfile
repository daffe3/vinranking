FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/SystembolagetApp/SystembolagetApp.csproj backend/SystembolagetApp/
RUN dotnet restore backend/SystembolagetApp/SystembolagetApp.csproj
COPY backend/SystembolagetApp/ backend/SystembolagetApp/
RUN dotnet publish backend/SystembolagetApp/SystembolagetApp.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SystembolagetApp.dll"]
