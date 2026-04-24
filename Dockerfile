FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY RiskManagement/RiskManagement.csproj RiskManagement/
RUN dotnet restore RiskManagement/RiskManagement.csproj

COPY . .
WORKDIR /src/RiskManagement
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Uploads dizini
RUN mkdir -p /app/uploads/ethics

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "RiskManagement.dll"]
