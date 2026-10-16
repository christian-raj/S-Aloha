FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# L'image aspnet:10.0 écoute déjà sur 8080 (HTTP_PORTS) : pas d'ASPNETCORE_URLS, qui ferait
# journaliser un avertissement de surcharge au démarrage.
EXPOSE 8080
ENTRYPOINT ["dotnet", "SAloha.Api.dll"]
