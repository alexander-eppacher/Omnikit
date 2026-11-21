# Omnikit-Server. Wird von docker-compose.yml gebaut (Kontext: Ordner des Repos).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Omnikit.Server/Omnikit.Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Ordner für die Datenbank-Datei; das Volume übernimmt beim ersten Start diesen Besitzer
RUN mkdir /data && chown app:app /data
# Nicht als root laufen: Das Image bringt den Benutzer „app“ mit
USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Omnikit.Server.dll"]
