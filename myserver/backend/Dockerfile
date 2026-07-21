FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src
COPY Backend/Telesale.Api/Telesale.Api.csproj Backend/Telesale.Api/
RUN dotnet restore Backend/Telesale.Api/Telesale.Api.csproj
COPY Backend/Telesale.Api/ Backend/Telesale.Api/
RUN dotnet publish Backend/Telesale.Api/Telesale.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime
WORKDIR /app
COPY --from=build --chown=app:app /app/publish/ ./
RUN mkdir -p /app/TempImports && chown app:app /app/TempImports
USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Telesale.Api.dll"]
