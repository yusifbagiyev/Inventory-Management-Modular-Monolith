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
| Audit | `AuditService` (one project) | `audit` | the audit log |

- `*.API` projects are **class libraries** holding the module's API controllers and a `XxxModule` class (`AddXxxModule()`, `Assemblies`). The host composes them in `InventoryManagement.Web/Extensions/ModuleHostExtensions.cs`.
- `SharedServices` is the shared kernel: contracts between modules, integration events, `DbSession` + `TransactionBehavior`, the background queue, `ImageStorage`, permissions and authorization, exceptions, `SearchHelper`, `ApiExceptionMiddleware`.
- Inside a module the old clean-architecture split remains (API → Application → Domain, Infrastructure implements Domain), CQRS via MediatR: `Features/<Aggregate>/{Commands,Queries}`, one file per command with nested `Command`/`Validator`/`Handler`. Entities have private setters; mutate through their methods.
- Mapping is explicit (`Mappings/*Mappings.cs` → `ToDto()`); AutoMapper was removed (unpatched advisory).

## How modules talk (read this before crossing a module boundary)

- **Never reference another module's Application/Infrastructure.** Use a contract in `SharedServices/Contracts` implemented by the owning module: `IProductCatalog` / `IProductTransfers` (products), `IApprovalRequests` + `IApprovalActionHandler` (approvals), `IUserDirectory` (identity).
- **Events** are MediatR `INotification`s in `SharedServices/Events` (`ProductCreated/Updated/DeletedEvent`, `RouteCompletedEvent`, `ApprovalRequest*Event`), published with `IPublisher` after the handler's `SaveChanges`. They carry image **URLs**, never bytes.
- **Transactions span modules.** All module DbContexts share one `NpgsqlConnection` per request (`DbSession`, registered via `AddModuleDbContext<T>(schema)`). A request implementing `ITransactionalRequest` is wrapped by `TransactionBehavior`; nested sends and event handlers join it (an enlistment interceptor calls `UseTransaction`). So product create + its route-history row, route completion + the product move, and approval + execution each commit or roll back together. Don't open transactions yourself.
- **Side effects outside the DB**: `DbSession.OnRollback(...)` for compensation (e.g. delete an uploaded file); `DbSession.AfterCommit(...)` for work that must only happen once committed. `AfterCommit` items run on the in-process background queue (`BackgroundWorkQueue`, lost on crash). Notification handlers only enqueue, so WhatsApp/SignalR never slow or fail a request.
- **Audit log.** `AddModuleDbContext` attaches `SharedServices.Auditing.AuditInterceptor` to every module context: each created/changed/deleted row is written to `audit.AuditEntries` (who, IP, when, action, `{field, old, new}` list) in the same transaction as the change — a save outside a request-wide transaction gets its own one there, so a change never commits without its record. The action is the first command of the request (`AuditActionBehavior`, e.g. `UpdateProduct`; nested commands such as an approval's execution are recorded under it), else `Controller.Action`. Sign-in, failed sign-in and sign-out are written by `AccountController`. Notifications, refresh tokens, sign-in stamps and concurrency columns are skipped; `PasswordHash` is recorded as "(changed)". A context registered with `audited: false` (the audit context itself) is not recorded. Page: `/Audit` (`audit.view`), one row per request.
- Savepoints: `ApproveRequest` runs the action behind `DbSession.SavepointAsync`; on failure it rolls back to the savepoint (also discarding that span's after-commit work and running its compensations) and records the request as `Failed`.
- Row versions (`xmin`) on `InventoryRoute` and `ApprovalRequest`: concurrent complete/approve fail with `DbUpdateConcurrencyException` → 409.

## Approvals (two-tier permissions)

Every write permission has `x` and `x.direct` (`product.create` / `product.create.direct`). `ProductManagementService` / `RouteManagementService` dispatch: `.direct` → run the command; `x` only → `IApprovalRequests.SubmitAsync` then throw `ApprovalRequiredException` (API → 202 `{approvalRequestId}`, UI → "submitted for approval"); neither → `InsufficientPermissionsException` (403). On approval `ActionExecutor` finds the `IApprovalActionHandler` whose `CanHandle(requestType)` matches and runs the owning module's command in-process. `ActionData` is stored JSON; old rows use PascalCase and nested `ProductData`/`UpdateData` — always read it through `SharedServices.Contracts.ApprovalActionData` helpers, which accept every shape. To add an approvable action: add a `RequestType` constant, build the ActionData in the management service, handle it in the module's `IApprovalActionHandler`.

`RequestType` values equal the non-direct permission string, **except** `product.transfer`, which is gated by `route.create`.

## Auth

- **UI**: cookie auth only (`AuthenticationExtensions`). Login validates in-process (`IAuthService.ValidateCredentialsAsync`), rate-limited per IP. Claims come from `UserPrincipalFactory` and are **re-read from the DB every 5 minutes** (`OnValidatePrincipal`), so role/permission changes and deactivation apply without a re-login.
- **/api**: a policy scheme picks `X-Api-Key` (ServiceDesk; keys in `ApiKeys:[{Key,ServiceName,ServiceId,Permissions}]`, env only — in `.env` the key is single-quoted because it contains `$`; ServiceDesk calls `http://<server>:5001`, which nginx serves with the product API only, as the old product-service port did), `Bearer` JWT (issued by `/api/auth/login` for external clients), else the cookie. Unauthenticated /api and AJAX calls get 401/403, not a login redirect.
- **CSRF**: cookie-authenticated unsafe `/api` calls must send the `RequestVerificationToken` header (JS: `AppConfig.antiforgeryHeaders()`); MVC POSTs use `[ValidateAntiForgeryToken]`.
- **Roles and permissions.** Two roles: **Admin** (bypasses every permission check on both sides) and **User**. A user holds their role's permissions (`RolePermissions`, edited by Admins on *Users → Role permissions*) plus their own (`UserPermissions`, the user's Edit page), so changing someone's role changes what they get from it. Both pages use `_PermissionEditor` + `permission-editor.js`, driven by `Models/ViewModels/PermissionCatalog.cs` (permissions by page in plain words; two-level actions as one No / With approval / Direct choice holding at most one of `x`, `x.direct`) — a new permission belongs there too. Every page and function has a permission in `SharedServices/Identity/AllPermissions.cs` (seeded in `IdentityDbContext`; a new one needs a seed row and a migration): products/routes (two-tier, below), `product.code.update`, `product.export`, `route.export`, `dashboard.view`, `category.*`, `department.*` (+ `.export` for Word), `approval.view`/`approval.decide`, `user.view`/`user.manage`, `audit.view`. Only Admins change roles and permissions, and only Admins can change an Admin account. Personal pages (profile, notifications, my requests) need no permission; `/` sends a user to the first page they may open. Gates: `[Permission("a", "b")]` (API, any of) and `[PermissionAuthorize("a", "b")]` / `User.HasPermission` (UI; hide the button too). Policies are resolved dynamically by `PermissionPolicyProvider` (`a|b` = any of). Notifications go only to users who can see the record (`IUserDirectory.GetActiveUserIdsWithPermissionAsync`); new approval requests go to `approval.decide` holders.
- Behind nginx: forwarded headers trust exactly one hop; client IP = `RemoteIpAddress`, never the raw `X-Forwarded-For`.

## UI (InventoryManagement.Web)

- MVC controllers call MediatR / module services directly. `BaseController.RunAsync` turns module outcomes (approval required, validation, not found, conflict…) into the `ApiResponse` shape the JS expects (`isSuccess`, `isApprovalRequest`, `approvalRequestId`, `message`); `HandleApiResponse` returns JSON for AJAX or redirects with a TempData toast.
- View models are filled from module DTOs with `ModelMapper` (Newtonsoft JToken round-trip — same semantics as when they were deserialized from HTTP JSON).
- Module API controllers are put in the `Api` **area** by a convention, so MVC link generation (`asp-action="Create"` on the Products page) never resolves to the same-named API action. Keep it that way when adding API controllers.
- **Live updates.** `LiveUpdateInterceptor` (`SharedServices/LiveUpdates`, attached by `AddModuleDbContext`) sends every committed change to a type registered with `services.TrackLiveEntity<T>("name")` to all browsers as SignalR `EntityChanged` `{changes:[{entity,id,action}], actorId, actorName, at}` — no record data, pages re-fetch with the viewer's permissions. Pages opt in with `LiveUpdates.watch({ entities, ids, regions, beforeRefresh, afterRefresh, isEditing, mode })` (`live-updates.js`): regions (elements with ids) are re-fetched from the same URL and swapped, deferred while a modal/dropdown is open, the user is typing in a region or the tab is hidden; edit pages use `mode: 'warn'` and never touch the form. Scripts in the fetched HTML do not run, so JS inside a region (DataTables, charts, captured row lists) is re-initialised in `afterRefresh`, and data such scripts need goes in `data-*` attributes (encoded: `@Json.Serialize(x).ToString()`). A new list/details page or tracked entity needs both sides.
- **Frontend libraries are self-hosted** in `wwwroot/lib` (no CDN: the app must work without internet). Files and exact versions are pinned in `InventoryManagement.Web/libman.json`; to update, edit it and run `libman restore` (`dotnet tool install -g Microsoft.Web.LibraryManager.Cli`), then commit the files. Current stack: jQuery 4, Bootstrap 5.3, DataTables 3 (jQuery API still works; the `dom` option is gone — use `layout`), Chart.js 4, SignalR 10, Font Awesome 7 + Bootstrap Icons, Air Datepicker behind `date-range.js` (`DateRange.attach/set/clear/parse/iso`; programmatic selection must pass `{ silent: true }` — Air Datepicker fires `onSelect` in a later tick, and a non-silent restore reloads the page in a loop).
- Client JS (`wwwroot/js`): everything is same-origin (`AppConfig.buildApiUrl`), SignalR at `/notificationHub` authenticates with the cookie, `AjaxHandler.handleForm` is the standard form path, `escapeHtml` (site.js) must wrap any API/user data put into HTML, and Razor values go into `data-*` attributes, not inline `onclick` strings.
- **Design system.** `design/` is the Claude Design package (tokens, component CSS, handoff notes, reference screens). `wwwroot/css/tokens.css` (copied unchanged) defines every colour, light and dark (`--ip-*`, switched by `data-bs-theme`; the sidebar is a `data-bs-theme="dark"` island); `ip-components.css` is the package's `.ip-*` component layer, with this app's additions at its end; `modern-ui.css` restyles the Bootstrap classes the older markup uses and maps its short names (`--accent`, `--ink`, `--surface`...) to the tokens. Load order: bootstrap -> tokens -> ip-components -> site -> modern-ui. New UI uses `.ip-*` classes and tokens, never hex colours; the accent is only for the primary action, focus, active tab and unread; status colours only for status; brand yellow only for the logo and the cover star. Lists end in `_Pagination` (range, rows per page, page numbers); `tr[data-href]` rows open their page (site.js). **Lists change in place:** status tabs, page numbers, rows per page, filters (`list-filters.js` → `ListNav.go`), GET filter forms and search-as-you-type (350 ms) fetch the page and swap only `[data-list-region]`, `[data-list-tabs]`, the count under the title, the page's `LiveUpdates.watch` regions (their `afterRefresh` runs) and the filter bar's buttons — never the filter controls; the URL follows (Back works) and `listnav:loaded` fires. **Phones (< 768px):** every `.ip-table` turns into cards ("Header: value" lines labelled by `MobileLayout` in site.js from the `<th>` texts — photo cells float right, `.actions`/button-only cells become the bottom strip, empty and icon-only cells hide), and filter bars fold behind a "Filters (n)" button; so a new list needs real header texts, nothing else. Page transitions: `@view-transition` (ip-components.css) with the rail/toolbar named only during the swap by the inline script in the page heads.
- **Language.** Azerbaijani is the default UI language, English via the top-bar toggle (`LanguageController`, culture cookie). Only the *UI* culture changes; the formatting culture stays en-US (decimal point in forms), and dates are always written explicitly as `dd.MM.yyyy[ HH:mm]` (JS: `formatDate()`; DataTables date cells carry `data-order="yyyy-MM-dd HH:mm"`). The English text is the key: views use `@L["Products"]` / `@L["{0} pending", n]`, scripts use `t('Products')` (site.js; table served by `/Language/Strings`), and every key goes into `Resources/i18n/az.json` (embedded — rebuild after editing). Missing keys show English. Server messages go through `Tr()` / `JsonStringLocalizer.TranslateMessage`, which also matches `{0}` pattern keys, so module exceptions, stored notifications and route-history notes are translated when shown (they stay English in the DB). Data annotations are localized by key too (`DefaultValidationMessages` gives bare `[Required]` a key).
- Exports: department inventory **Word** is server-side (`WordExportService`, brand color `#FFC000`, logo `wwwroot/logo.jpg`); product/route **PDF** is client-side print-to-PDF (`pdf-export.js`) — select columns by header name and escape with `escapePdfText()`.

## Conventions

- `SharedServices/Exceptions/ApprovalRequiredException.cs` declares three exception types (`ApprovalRequiredException`, `DuplicateEntityException`, `InsufficientPermissionsException`) — grep `class <Name>`.
- Search over user text goes through `SearchHelper.NormalizeForSearch` (folds Azerbaijani letters) or Azerbaijani records won't match.
- Images: `ImageStorage` writes `{ImageSettings:RootPath}/{products|routes}/{inventoryCode}/{unique}` and returns `/images/...` URLs; the root is the web root's `images` folder (bind-mounted to `./storage/images` in Docker, served by nginx too). Route history keeps its own copy of the product image.
- Products and routes have several images: `ImageUrls` (`text[]`, cover first) with `ImageUrl` kept equal to the cover for lists, exports, WhatsApp and API clients — always change them through `SetImages`. Edits send `ImageFiles` (added), `RemoveImageUrls`, `CoverImageUrl` (`SharedServices/Storage/ImageSet`); the legacy single `ImageFile` still means "replace all". Approval ActionData stores new files base64 under `images` (`ApprovalActionData.EncodeImagesAsync`/`GetImages`). Completing a transfer with photos replaces the product's images with copies of them. UI: `_ImageManager` partial + `image-manager.js` (picker), `_ImageGallery` (details).
- **Products point at their department/category by id** (a rename or move applies to the current product); **routes keep the names of the moment** (`FromDepartmentName`, `ToDepartmentName`, `CategoryName`) and are shown and filtered by those names — the route list's department filter and the dashboard's department figures group by the stored name, so renamed or deleted departments' history stays as it was. The ids on a route are only used to act on the product (complete = move to `ToDepartmentId`).
- **Inactive departments** are not offered in pick lists (`LookupExtensions.ToChoiceList`, which keeps a record's current value) and are refused on create/update/transfer and on completing a transfer; list filters still show them.
- Products have a free-text `Color` and `Specifications` (name/value list, `jsonb`), edited only on the product form. An update changes them only when `UpdateProductDto.ReplaceDetails` is set (the web form always sets it), so API clients and approval requests that predate these fields leave them alone.
- Notifications that go to everyone skip the user who caused them (actor read from the request in `NotificationEventHandlers`).
- Timestamps are `timestamp without time zone` with `DateTime.Now` (container `TZ`).
- Every request is logged with its duration (`UseSerilogRequestLogging`, visible in Seq). `StartupWarmup` runs the main pages' queries once after startup (a cold dashboard took ~3 s); add a page's new heavy query there.

## Deployment / data

- `deploy/CD.md` (Azerbaijani): runner setup, rollback, restoring a pre-deploy backup.
- `deploy/MIGRATION.md` (Azerbaijani) is the one-time cut-over runbook from the old 11-container stack. The *Prepare cut-over* workflow (`prepare-cutover.yml`, self-hosted runner) stages `next/` (compose, `deploy/`, pulled image) beside the running stack; `deploy/cutover.sh [check|rollback]`, run as root in `/opt/inventory166`, stops the old services, dumps everything, swaps the compose file and runs `deploy/migrate-data.sh`, which copies the per-service databases into the module schemas (single transaction, row-count check, sequence realignment). `deploy/sql/fix-operator-permissions.sql` optionally fixes the Operator role's off-by-one seed.
- Data-protection keys persist to `DataProtection:KeysPath` (`./storage/keys` in compose) — without it every redeploy signs everyone out.
- The container runs as uid 1654; bind-mounted `storage/keys` and `storage/images` must be writable by it.
