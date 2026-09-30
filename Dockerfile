# The modular monolith: one image, built from the repository root.
#   docker build -t inventory-app .

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# libgssapi: Npgsql probes for Kerberos at startup and logs an error without it.
# curl: the compose healthcheck calls /health.
# keys/images: written by the non-root app user (bind-mounted in compose; see deploy/MIGRATION.md).
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/keys /app/wwwroot/images/products /app/wwwroot/images/routes \
    && chown -R $APP_UID:$APP_UID /app/keys /app/wwwroot/images
USER $APP_UID
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Project files first so the restore layer is cached until a dependency changes.
COPY ["SharedServices/SharedServices.csproj", "SharedServices/"]
COPY ["IdentityService.Domain/IdentityService.Domain.csproj", "IdentityService.Domain/"]
COPY ["IdentityService.Application/IdentityService.Application.csproj", "IdentityService.Application/"]
COPY ["IdentityService.Infrastructure/IdentityService.Infrastructure.csproj", "IdentityService.Infrastructure/"]
COPY ["IdentityService.API/IdentityService.API.csproj", "IdentityService.API/"]
COPY ["ProductService.Domain/ProductService.Domain.csproj", "ProductService.Domain/"]
COPY ["ProductService.Application/ProductService.Application.csproj", "ProductService.Application/"]
COPY ["ProductService.Infrastructure/ProductService.Infrastructure.csproj", "ProductService.Infrastructure/"]
COPY ["ProductService.API/ProductService.API.csproj", "ProductService.API/"]
COPY ["RouteService.Domain/RouteService.Domain.csproj", "RouteService.Domain/"]
COPY ["RouteService.Application/RouteService.Application.csproj", "RouteService.Application/"]
COPY ["RouteService.Infrastructure/RouteService.Infrastructure.csproj", "RouteService.Infrastructure/"]
COPY ["RouteService.API/RouteService.API.csproj", "RouteService.API/"]
COPY ["ApprovalService.Domain/ApprovalService.Domain.csproj", "ApprovalService.Domain/"]
COPY ["ApprovalService.Application/ApprovalService.Application.csproj", "ApprovalService.Application/"]
COPY ["ApprovalService.Infrastructure/ApprovalService.Infrastructure.csproj", "ApprovalService.Infrastructure/"]
COPY ["ApprovalService.API/ApprovalService.API.csproj", "ApprovalService.API/"]
COPY ["NotificationService.Domain/NotificationService.Domain.csproj", "NotificationService.Domain/"]
COPY ["NotificationService.Application/NotificationService.Application.csproj", "NotificationService.Application/"]
COPY ["NotificationService.Infrastructure/NotificationService.Infrastructure.csproj", "NotificationService.Infrastructure/"]
COPY ["NotificationService.API/NotificationService.API.csproj", "NotificationService.API/"]
COPY ["InventoryManagement.Web/InventoryManagement.Web.csproj", "InventoryManagement.Web/"]
RUN dotnet restore "InventoryManagement.Web/InventoryManagement.Web.csproj"

COPY . .
RUN dotnet publish "InventoryManagement.Web/InventoryManagement.Web.csproj" \
    -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InventoryManagement.Web.dll"]
