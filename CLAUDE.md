# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Stack

.NET 10 modular monolith: ASP.NET Core MVC + Razor UI and a JSON `/api` in **one host** (`InventoryManagement.Web`), MediatR + FluentValidation, EF Core on PostgreSQL 15 (Npgsql), SignalR, Serilog → Seq. The system used to be five microservices behind Ocelot with RabbitMQ; the project names (`ProductService.*` etc.) are the old service names, now modules.

## Commands

```bash
dotnet build InventoryManagement.sln
dotnet run --project InventoryManagement.Web  # http://localhost:5051 (GET /health checks the DB)
```

Local run needs a PostgreSQL and two secrets. `appsettings.Development.json` holds a password-less `ConnectionStrings:DefaultConnection`; set the full string and the JWT key with user-secrets (or `ConnectionStrings__DefaultConnection` / `Jwt__Key` env vars):
```bash
dotnet user-secrets --project InventoryManagement.Web set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=inventory;Username=postgres;Password=..."
dotnet user-secrets --project InventoryManagement.Web set "Jwt:Key" "<32+ chars>"
```
Every module's migrations run at startup (`MigrateModulesAsync`), followed by an identity-sequence realignment.

Add a migration (each Infrastructure project has an `IDesignTimeDbContextFactory`, so it is its own startup project):
```bash
dotnet ef migrations add <Name> --project ProductService.Infrastructure --startup-project ProductService.Infrastructure
```

There are no test projects; `dotnet build` plus exercising the running app is the verification.

Docker: `docker compose up -d --build` (root `Dockerfile`, `docker-compose.yml`, `deploy/nginx/nginx.conf`; secrets from `.env`, see `.env.example`). The `app` image is `${APP_IMAGE:-inventory-app:local}` and has a `/health` healthcheck.

CI/CD: `.github/workflows/ci.yml` (build, vulnerable-package check, image build) on every PR and master push. `cd.yml` runs after CI passes on master: it pushes `ghcr.io/<owner>/inventory-app:sha-<commit>`, then a self-hosted runner (label `inventory-prod`) on the production server runs `deploy/deploy.sh` (pg_dump backup → recreate `app` → wait for health → auto-rollback on failure → pin `APP_IMAGE` in the server's `.env`). Gated by the repo variable `CD_ENABLED`; setup and rollback in `deploy/CD.md`. Deploys copy `docker-compose.yml` and `deploy/` over the server's copies, so server-only changes belong in `docker-compose.override.yml`.

## Layout

| Module | Projects | Schema | Owns |
|---|---|---|---|
| Identity | `IdentityService.{Domain,Application,Infrastructure,API}` | `identity` | users, roles, permissions, JWT issuing |
| Products | `ProductService.*` | `product` | products, categories, departments, images |
| Routes | `RouteService.*` | `route` | transfers + the product audit trail |
| Approvals | `ApprovalService.*` | `approval` | approval requests and their execution |
| Notifications | `NotificationService.*` | `notification` | stored notifications, SignalR hub, WhatsApp |

- `*.API` projects are **class libraries** holding the module's API controllers and a `XxxModule` class (`AddXxxModule()`, `Assemblies`). The host composes them in `InventoryManagement.Web/Extensions/ModuleHostExtensions.cs`.
- `SharedServices` is the shared kernel: contracts between modules, integration events, `DbSession` + `TransactionBehavior`, the background queue, `ImageStorage`, permissions and authorization, exceptions, `SearchHelper`, `ApiExceptionMiddleware`.
- Inside a module the old clean-architecture split remains (API → Application → Domain, Infrastructure implements Domain), CQRS via MediatR: `Features/<Aggregate>/{Commands,Queries}`, one file per command with nested `Command`/`Validator`/`Handler`. Entities have private setters; mutate through their methods.
- Mapping is explicit (`Mappings/*Mappings.cs` → `ToDto()`); AutoMapper was removed (unpatched advisory).

## How modules talk (read this before crossing a module boundary)

- **Never reference another module's Application/Infrastructure.** Use a contract in `SharedServices/Contracts` implemented by the owning module: `IProductCatalog` / `IProductTransfers` (products), `IApprovalRequests` + `IApprovalActionHandler` (approvals), `IUserDirectory` (identity).
- **Events** are MediatR `INotification`s in `SharedServices/Events` (`ProductCreated/Updated/DeletedEvent`, `RouteCompletedEvent`, `ApprovalRequest*Event`), published with `IPublisher` after the handler's `SaveChanges`. They carry image **URLs**, never bytes.
- **Transactions span modules.** All module DbContexts share one `NpgsqlConnection` per request (`DbSession`, registered via `AddModuleDbContext<T>(schema)`). A request implementing `ITransactionalRequest` is wrapped by `TransactionBehavior`; nested sends and event handlers join it (an enlistment interceptor calls `UseTransaction`). So product create + its route-history row, route completion + the product move, and approval + execution each commit or roll back together. Don't open transactions yourself.
- **Side effects outside the DB**: `DbSession.OnRollback(...)` for compensation (e.g. delete an uploaded file); `DbSession.AfterCommit(...)` for work that must only happen once committed. `AfterCommit` items run on the in-process background queue (`BackgroundWorkQueue`, lost on crash). Notification handlers only enqueue, so WhatsApp/SignalR never slow or fail a request.
- Savepoints: `ApproveRequest` runs the action behind `DbSession.SavepointAsync`; on failure it rolls back to the savepoint (also discarding that span's after-commit work and running its compensations) and records the request as `Failed`.
- Row versions (`xmin`) on `InventoryRoute` and `ApprovalRequest`: concurrent complete/approve fail with `DbUpdateConcurrencyException` → 409.

## Approvals (two-tier permissions)

Every write permission has `x` and `x.direct` (`product.create` / `product.create.direct`). `ProductManagementService` / `RouteManagementService` dispatch: `.direct` → run the command; `x` only → `IApprovalRequests.SubmitAsync` then throw `ApprovalRequiredException` (API → 202 `{approvalRequestId}`, UI → "submitted for approval"); neither → `InsufficientPermissionsException` (403). On approval `ActionExecutor` finds the `IApprovalActionHandler` whose `CanHandle(requestType)` matches and runs the owning module's command in-process. `ActionData` is stored JSON; old rows use PascalCase and nested `ProductData`/`UpdateData` — always read it through `SharedServices.Contracts.ApprovalActionData` helpers, which accept every shape. To add an approvable action: add a `RequestType` constant, build the ActionData in the management service, handle it in the module's `IApprovalActionHandler`.

`RequestType` values equal the non-direct permission string, **except** `product.transfer`, which is gated by `route.create`.

## Auth

- **UI**: cookie auth only (`AuthenticationExtensions`). Login validates in-process (`IAuthService.ValidateCredentialsAsync`), rate-limited per IP. Claims come from `UserPrincipalFactory` and are **re-read from the DB every 5 minutes** (`OnValidatePrincipal`), so role/permission changes and deactivation apply without a re-login.
- **/api**: a policy scheme picks `X-Api-Key` (ServiceDesk; keys in `ApiKeys:[{Key,ServiceName,ServiceId,Permissions}]`, env only), `Bearer` JWT (issued by `/api/auth/login` for external clients), else the cookie. Unauthenticated /api and AJAX calls get 401/403, not a login redirect.
- **CSRF**: cookie-authenticated unsafe `/api` calls must send the `RequestVerificationToken` header (JS: `AppConfig.antiforgeryHeaders()`); MVC POSTs use `[ValidateAntiForgeryToken]`.
- Permissions: `[Permission("x")]` (API) and `[PermissionAuthorize]` / `User.HasPermission` (UI). Policies are resolved dynamically by `PermissionPolicyProvider`. **Admin role bypasses every permission check** on both sides.
- Behind nginx: forwarded headers trust exactly one hop; client IP = `RemoteIpAddress`, never the raw `X-Forwarded-For`.

## UI (InventoryManagement.Web)

- MVC controllers call MediatR / module services directly. `BaseController.RunAsync` turns module outcomes (approval required, validation, not found, conflict…) into the `ApiResponse` shape the JS expects (`isSuccess`, `isApprovalRequest`, `approvalRequestId`, `message`); `HandleApiResponse` returns JSON for AJAX or redirects with a TempData toast.
- View models are filled from module DTOs with `ModelMapper` (Newtonsoft JToken round-trip — same semantics as when they were deserialized from HTTP JSON).
- Module API controllers are put in the `Api` **area** by a convention, so MVC link generation (`asp-action="Create"` on the Products page) never resolves to the same-named API action. Keep it that way when adding API controllers.
- Client JS (`wwwroot/js`): everything is same-origin (`AppConfig.buildApiUrl`), SignalR at `/notificationHub` authenticates with the cookie, `AjaxHandler.handleForm` is the standard form path, `escapeHtml` (site.js) must wrap any API/user data put into HTML, and Razor values go into `data-*` attributes, not inline `onclick` strings.
- Exports: department inventory **Word** is server-side (`WordExportService`, brand color `#FFC000`, logo `wwwroot/logo.jpg`); product/route **PDF** is client-side print-to-PDF (`pdf-export.js`) — select columns by header name and escape with `escapePdfText()`.

## Conventions

- `SharedServices/Exceptions/ApprovalRequiredException.cs` declares three exception types (`ApprovalRequiredException`, `DuplicateEntityException`, `InsufficientPermissionsException`) — grep `class <Name>`.
- Search over user text goes through `SearchHelper.NormalizeForSearch` (folds Azerbaijani letters) or Azerbaijani records won't match.
- Images: `ImageStorage` writes `{ImageSettings:RootPath}/{products|routes}/{inventoryCode}/{unique}` and returns `/images/...` URLs; the root is the web root's `images` folder (bind-mounted to `./storage/images` in Docker, served by nginx too). Route history keeps its own copy of the product image.
- Timestamps are `timestamp without time zone` with `DateTime.Now` (container `TZ`).

## Deployment / data

- `deploy/CD.md` (Azerbaijani): runner setup, rollback, restoring a pre-deploy backup.
- `deploy/MIGRATION.md` (Azerbaijani) is the cut-over runbook from the old per-service databases; `deploy/migrate-data.sh` copies them into the module schemas (single transaction, row-count check, sequence realignment). `deploy/sql/fix-operator-permissions.sql` optionally fixes the Operator role's off-by-one seed.
- Data-protection keys persist to `DataProtection:KeysPath` (`./storage/keys` in compose) — without it every redeploy signs everyone out.
- The container runs as uid 1654; bind-mounted `storage/keys` and `storage/images` must be writable by it.
