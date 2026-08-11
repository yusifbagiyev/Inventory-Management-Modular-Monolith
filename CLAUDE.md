# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Stack & prerequisites

.NET 10 (`net10.0`), PostgreSQL 15, RabbitMQ 3, Seq (logging), Ocelot (gateway), MediatR + FluentValidation + AutoMapper, Serilog, SignalR (real-time), Entity Framework Core (Npgsql provider), Docker Compose for orchestration, optional Redis caching for `ProductService`. There are no test projects in the solution.

## Common commands

Build everything from the repo root:
```bash
dotnet build InventoryManagement.sln
```

Run a single service locally (ports below). Each service auto-runs `Database.MigrateAsync()` on startup, so ensure `.env` values are exported or `appsettings.Development.json` has valid `ConnectionStrings:DefaultConnection` / `RabbitMQ:*` / `Jwt:*`:
```bash
dotnet run --project ProductService.API
```

Add an EF Core migration for a service (run from that service's Infrastructure project directory; the DbContext lives in Infrastructure but the tools entrypoint is `*.API`):
```bash
dotnet ef migrations add <Name> --project ProductService.Infrastructure --startup-project ProductService.API
dotnet ef database update      --project ProductService.Infrastructure --startup-project ProductService.API
```

Run the full stack via Docker Compose — **caveat: `docker-compose.yml` sets `context: ./src` and each Dockerfile expects sibling project directories inside its build context, but the projects live at the repo root today.** Either move projects under `src/` or edit each `context:` to `.` before `docker compose up`.
```bash
docker compose up -d --build
docker compose logs -f product-service
```

Secrets/config are read from `.env` at the repo root (used by compose interpolation). Do not commit real values; the checked-in `.env` currently contains sample credentials.

## Service topology

Seven .NET web projects, all `net10.0`, all Serilog → Seq (`http://seq:80` in prod, `http://localhost:5342` in dev). Compose containers, host port → container port `:80`:

| Service | Local dev port | Compose port | Container name | DB |
|---|---|---|---|---|
| `ApiGateway` | — | `5000:80` | `inventory_api_gateway` | — |
| `ProductService.API` | 5001 | (internal) | `inventory_product_service` | `product_service` |
| `RouteService.API` | 5002 | (internal) | `inventory_route_service` | `route_service` |
| `IdentityService.API` | 5003 | (internal) | `inventory_identity_service` | `identity_service` |
| `ApprovalService.API` | 5004 | (internal) | `inventory_approval_service` | `approval_service` |
| `NotificationService.API` | 5005 | (internal) | `inventory_notification_service` | `notification_service` |
| `InventoryManagement.Web` (MVC) | 5051 / 7171 | (internal) | `inventory_web` | — (session-only) |

Front door: `nginx` (host 80/443) → `web` and `api-gateway`. Seq UI: `http://localhost:5342`. RabbitMQ UI: `http://localhost:15672`.

Dev ports referenced above come from `ApiGateway/ocelot.json`; prod routing (container→container) lives in `ApiGateway/ocelot.Production.json` and uses service names as hosts.

## Architecture

**Clean-architecture per service.** Each backend service ships four projects — `.API`, `.Application`, `.Domain`, `.Infrastructure` (Approval also has `.Shared`). Dependency direction is `API → Application → Domain`, with `Infrastructure` implementing `Domain` interfaces (repositories, message publisher, cache) and wired via `AddApplication()` / `AddInfrastructure(config)` extension methods in each service's `DependencyInjection.cs`.

**CQRS via MediatR.** Feature folders under `.Application/Features/<Aggregate>/{Commands,Queries}`. A single `ValidationBehavior<TRequest,TResponse>` pipeline runs all FluentValidation validators. Convention: one command/query per file, containing nested `record Command : IRequest<...>`, `class Validator : AbstractValidator<Command>`, and `class Handler : IRequestHandler<Command, ...>`.

**Domain-driven entities.** `Product`, `InventoryRoute`, `ApprovalRequest` etc. have private setters and mutate via named methods (`Product.Update(...)`, `InventoryRoute.CreateTransfer(...)`, `ApprovalRequest.Approve(...)`). Never assign properties directly from handlers — use the mutator, or add one.

**API Gateway is Ocelot + JWT.** `ApiGateway/ocelot.{env}.json` maps upstream `/api/{route}` templates to downstream services. All routes except `/api/auth/*` require Bearer auth. Polly retry (2 attempts, exponential) + circuit breaker (5 fails / 30s) are attached to the outbound HTTP client. The gateway strips CORS + adds `X-Forwarded-*` headers.

## Authentication & authorization (critical to understand before changing any controller)

**Login flow.** `InventoryManagement.Web` uses cookie auth (`.AspNetCore.Cookies`, 8-hour sliding expiry). On login, `AuthService` calls `IdentityService`, stashes the JWT and refresh token in the session, and `TokenRefreshBackgroundService` renews them in the background. Outbound API calls attach the JWT via `ApiService`. AJAX requests get 401/403 instead of a 302 to `/Account/Login` — see `AddCustomAuthentication` in `InventoryManagement.Web/Extensions/ServiceExtensions.cs`.

**JWT contents.** Issued by `IdentityService`, signed with the shared HMAC key from `.env` (`JWT_SECRET_KEY`). Carries `ClaimTypes.NameIdentifier`, `ClaimTypes.Name`, roles, and a **`permission` claim per granted permission** (`SharedServices/Identity/AllPermissions.cs`). Every downstream service validates against `Jwt:Issuer` / `Jwt:Audience` / `Jwt:Key`.

**Permission enforcement.** `SharedServices.Authorization.PermissionAttribute(perm)` extends `AuthorizeAttribute` with `Policy = perm`. Services register the policy list explicitly in `Program.cs` (`options.AddPolicy(AllPermissions.ProductView, …)`). `PermissionRequirement` / `PermissionHandler` (registered as `IAuthorizationHandler` singleton) check the `permission` claim — and **`Admin` role bypasses all permission checks**.

**Two-tier permissions.** Each write action has both `xxx` and `xxx.direct` (e.g. `product.create`, `product.create.direct`). `ProductManagementService`, `RouteManagementService`, etc. dispatch as follows:
- Holder of `.direct` → command handler runs immediately.
- Holder of the non-direct permission → an `ApprovalRequest` is enqueued and `ApprovalRequiredException` is thrown (controllers translate this to `202 Accepted` with the approval id).
- Neither → `InsufficientPermissionsException` → `403`.

**Approval execution (non-obvious).** When an admin approves a request, `ApprovalService.Infrastructure.Services.ActionExecutor` **forges a short-lived (5 min) JWT** using the shared HMAC key, stuffed with `Admin` role and every `.direct` permission, then re-calls the target service through the API gateway on `/api/products/approved`, `/api/products/{id}/approved/multipart`, `/api/inventoryroutes/{id}/approved`, etc. Those endpoints are marked `[ApiExplorerSettings(IgnoreApi = true)]` + `[Authorize(Roles = "Admin")]` and are the only path that skips the approval detour. When adding an approvable action:
1. Add a value to `SharedServices/Enum/RequestType.cs`.
2. Add an action-data DTO in `SharedServices/DTOs`.
3. Add an `Execute<X>` branch to `ActionExecutor.ExecuteAsync`.
4. Add an `/approved` sibling endpoint on the target service with `[Authorize(Roles = "Admin")]` + `[ApiExplorerSettings(IgnoreApi = true)]`.

**Product API-key auth.** `ProductService` additionally supports `X-Api-Key` for internal integrations (ServiceDesk) via `AddPolicyScheme("JWT_OR_APIKEY", …)` — see `ProductService.API/Authentication/ApiKeyAuthenticationHandler.cs`; keys are declared under `ApiKeys` in `appsettings.Production.json`.

## Messaging (RabbitMQ)

`ProductService`, `RouteService`, `ApprovalService`, `NotificationService` each ship a `RabbitMQPublisher : IMessagePublisher` (singleton) and a `RabbitMQConsumer : BackgroundService` (hosted). Exchange: `inventory-events` (topic). Events like `ProductCreatedEvent` are published from command handlers after `SaveChangesAsync`. Consumers include their own dead-letter queue plumbing (see `PermanentMessageException` in `ProductService.Infrastructure/Services/RabbitMQConsumer.cs` — a permanent failure is routed to `<queue>-dead` instead of nacked-and-requeued). Config lookup order in every RabbitMQ init: `IConfiguration["RabbitMQ:*"]` → `Environment.GetEnvironmentVariable("RabbitMQ__*")` → `localhost`/`guest`.

## Real-time (SignalR)

`NotificationService` hosts a `NotificationHub` at `/notificationHub` (mapped with `.RequireAuthorization()`). Because browsers can't set `Authorization` on the WebSocket upgrade, the JWT middleware reads it from the `?access_token=` query param when the path starts with `/notificationHub`. Clients are added to `user-{userId}` and `role-{roleName}` groups on connect for targeted push. `Web` opens the connection directly via `NotificationService__BaseUrl` — this is one of the few paths that does **not** go through the API gateway.

## Persistence & migrations

Every service owns its own PostgreSQL database (see table above). All DbContexts are registered in the service's Infrastructure `DependencyInjection.cs`. **Migrations are applied automatically at startup** — every service's `Program.cs` calls `Database.MigrateAsync()` followed by `Database.EnsureCreatedAsync()` inside a startup scope. Adding a migration therefore only requires running `dotnet ef migrations add …` locally; deployment picks it up on next boot.

## Frontend (InventoryManagement.Web)

MVC + Razor (runtime compilation in Dev only). Talks to backend via two config knobs: `ApiGateway:BaseUrl` (all `IApiService` / `IAuthService` / `IApprovalService` / `IUserManagementService` calls) and `NotificationService:BaseUrl` (SignalR only). Typed `HttpClient`s live in `Services/`; **do not double-register them with `AddScoped` after `AddHttpClient<T>` — it silently discards the pooled handler** (see the comment in `Extensions/ServiceExtensions.cs`). Cookie config, AJAX 401 handling, and the `HasPermission(ClaimsPrincipal, string)` helper also live in `ServiceExtensions.cs`.

## Cross-service conventions

- **Shared code lives in `SharedServices/`** — permissions, roles, authorization primitives, exception types (`ApprovalRequiredException`, `InsufficientPermissionsException`, `NotFoundException`, `DuplicateEntityException`), enums, and action-data DTOs.
- **Image storage.** Product / route images are written to `wwwroot/images/{products,routes}` inside their service container and bind-mounted to `./storage/images/{products,routes}` on the host; nginx serves them read-only. `ImageSettings:BaseUrl` (per-service `appsettings`) is the public URL prefix.
- **Redis is opt-in on ProductService.** Setting `Redis:Enabled = true` in `ProductService.API/appsettings.json` swaps `NoCacheService` for `RedisCacheService` (see `ProductService.Infrastructure/DependencyInjection.cs` and `REDIS_INTEGRATION_GUIDE.md`). Nothing else uses Redis today.
- **Rate limiting.** Only `IdentityService` uses `System.Threading.RateLimiting` — a per-IP `LoginPolicyPerIP` (5 attempts / 10 min) applied to the login endpoint, plus a global 100/min limiter.
- **Ocelot dev vs prod.** `ocelot.Development.json` points at `localhost` + the ports table above; `ocelot.Production.json` uses container hostnames on port 80. When adding a new gateway route, update **both** files.
