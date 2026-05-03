FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SystembolagetApp/SystembolagetApp.csproj SystembolagetApp/
RUN dotnet restore SystembolagetApp/SystembolagetApp.csproj
COPY SystembolagetApp/ SystembolagetApp/
WORKDIR /src/SystembolagetApp
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SystembolagetApp.dll"]
