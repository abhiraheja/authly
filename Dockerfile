# syntax=docker/dockerfile:1
# Multi-stage build for Saarvix Identity (ASP.NET Core MVC monolith)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution + project files first for layer-cached restore
COPY Authly.slnx ./
COPY src/Saarvix.Identity.Core/Saarvix.Identity.Core.csproj            src/Saarvix.Identity.Core/
COPY src/Saarvix.Identity.Modules/Saarvix.Identity.Modules.csproj      src/Saarvix.Identity.Modules/
COPY src/Saarvix.Identity.Infrastructure/Saarvix.Identity.Infrastructure.csproj src/Saarvix.Identity.Infrastructure/
COPY src/Saarvix.Identity.Web/Saarvix.Identity.Web.csproj              src/Saarvix.Identity.Web/
RUN dotnet restore src/Saarvix.Identity.Web/Saarvix.Identity.Web.csproj

# Copy the rest and publish
COPY . .
RUN dotnet publish src/Saarvix.Identity.Web/Saarvix.Identity.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Saarvix.Identity.Web.dll"]
