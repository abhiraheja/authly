# syntax=docker/dockerfile:1
# Multi-stage build for Authly (ASP.NET Core MVC monolith)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

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
