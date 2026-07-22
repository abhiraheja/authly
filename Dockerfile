# syntax=docker/dockerfile:1
# Multi-stage build for Authly (ASP.NET Core MVC monolith)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Node.js for the SAARVIX Tailwind build (MSBuild runs `npm ci` + `npm run build:css`
# during publish, compiling wwwroot/css/saarvix.css so the image ships CSS offline).
# Pulled from the official Node image instead of `apt-get install nodejs npm`, so a flaky/mid-sync
# Ubuntu package mirror can never break the build (the recurring "File has unexpected size — Mirror
# sync in progress?" apt failure). glibc-compatible: Debian-bookworm node runs fine on the SDK image.
COPY --from=node:20-bookworm-slim /usr/local/bin/node /usr/local/bin/node
COPY --from=node:20-bookworm-slim /usr/local/lib/node_modules/npm /usr/local/lib/node_modules/npm
RUN ln -s /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
    && node --version && npm --version

# Copy solution + project files first for layer-cached restore
COPY Authly.slnx ./
COPY src/Authly.Core/Authly.Core.csproj            src/Authly.Core/
COPY src/Authly.Modules/Authly.Modules.csproj      src/Authly.Modules/
COPY src/Authly.Infrastructure/Authly.Infrastructure.csproj src/Authly.Infrastructure/
COPY src/Authly.Web/Authly.Web.csproj              src/Authly.Web/
RUN dotnet restore src/Authly.Web/Authly.Web.csproj

# Copy the rest and publish
COPY . .
RUN dotnet publish src/Authly.Web/Authly.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Authly.Web.dll"]
