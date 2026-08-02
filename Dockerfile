FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY RiskManagement/RiskManagement.csproj RiskManagement/
RUN dotnet restore RiskManagement/RiskManagement.csproj

COPY . .
WORKDIR /src/RiskManagement
RUN dotnet publish RiskManagement.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

# Uploads dizinleri:
#   /app/uploads          → ethics ve findings ekleri  (Docker volume ile persist)
#   /app/wwwroot/uploads  → logo ve görsel varlıklar   (ayrı Docker volume ile persist)
RUN mkdir -p /app/uploads/ethics \
 && mkdir -p /app/uploads/findings \
 && mkdir -p /app/wwwroot/uploads

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "RiskManagement.dll"]
