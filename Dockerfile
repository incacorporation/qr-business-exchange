# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["QRBusinessExchange.slnx", "./"]
COPY ["src/QRBusinessExchange.API/QRBusinessExchange.API.csproj", "src/QRBusinessExchange.API/"]
COPY ["src/QRBusinessExchange.Core/QRBusinessExchange.Core.csproj", "src/QRBusinessExchange.Core/"]
COPY ["src/QRBusinessExchange.Infrastructure/QRBusinessExchange.Infrastructure.csproj", "src/QRBusinessExchange.Infrastructure/"]
COPY ["src/QRBusinessExchange.Web/QRBusinessExchange.Web.csproj", "src/QRBusinessExchange.Web/"]

# Restore dependencies
RUN dotnet restore "src/QRBusinessExchange.API/QRBusinessExchange.API.csproj"
RUN dotnet restore "src/QRBusinessExchange.Web/QRBusinessExchange.Web.csproj"

# Copy all source code
COPY . .

# Build API
WORKDIR "/src/src/QRBusinessExchange.API"
RUN dotnet build "QRBusinessExchange.API.csproj" -c Release -o /app/api/build

# Build Web
WORKDIR "/src/src/QRBusinessExchange.Web"
RUN dotnet build "QRBusinessExchange.Web.csproj" -c Release -o /app/web/build

# Publish API
WORKDIR "/src/src/QRBusinessExchange.API"
RUN dotnet publish "QRBusinessExchange.API.csproj" -c Release -o /app/api/publish

# Publish Web
WORKDIR "/src/src/QRBusinessExchange.Web"
RUN dotnet publish "QRBusinessExchange.Web.csproj" -c Release -o /app/web/publish

# Runtime stage for API
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api-runtime
WORKDIR /app
EXPOSE 80
EXPOSE 443
COPY --from=build /app/api/publish .
ENTRYPOINT ["dotnet", "QRBusinessExchange.API.dll"]

# Runtime stage for Web
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS web-runtime
WORKDIR /app
EXPOSE 80
EXPOSE 443
COPY --from=build /app/web/publish .
ENTRYPOINT ["dotnet", "QRBusinessExchange.Web.dll"]
