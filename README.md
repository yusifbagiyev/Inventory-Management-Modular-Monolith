# Inventory Pro

An inventory management system for tracking an organisation's equipment: what each item is, which department and person has it, how it moved, and who approved each change. It has a web UI (Azerbaijani and English, light and dark) and a JSON API for other systems.

Built with **.NET 10** as a **modular monolith**: one ASP.NET Core host serves the MVC/Razor UI and the `/api`, on PostgreSQL.

## Features

- **Products.** Each item has an inventory code, category, department, worker, colour, free-form specifications (name/value) and several photos with a cover. There is a working / not working state and active / inactive availability. Lists have search, filters, a date range and column-selectable PDF export.
- **Transfers and history.** Moving a product between departments or workers creates a transfer that is completed later, optionally with photos. Every create, update, transfer and delete goes into the product's timeline. A transfer keeps the department and category *names of that moment*, so renaming or deleting a department never rewrites history.
- **Approvals (two-tier permissions).** Every write permission comes in two levels:
  - `x.direct` acts immediately.
  - `x` alone submits an approval request.
  
  An admin sees the proposed change field by field and approves or rejects it. Approval executes the action in the same transaction.
- **Live updates.** When someone changes a record, every open list and details page refreshes itself through SignalR. Edit forms only show a warning instead of refreshing.
- **Notifications.** Users get in-app notifications and a bell with an unread count. New products and completed transfers can also be posted, with a photo, to a WhatsApp group.
- **Users, roles and permissions.** Admin, Operator and User roles plus per-user permissions. Changes apply without signing in again.
- **Dashboard.** Transfer activity, categories and the busiest departments for 7 / 30 / 90 days or all time, plus a "needs attention" panel.
- **Exports.** Department inventory as a Word document, and lists and timelines as PDF.
- **Offline-friendly.** All frontend libraries are self-hosted, so the app works without internet access.

## Architecture

```
InventoryManagement.Web        host: MVC + Razor UI, /api, SignalR hub, composition root
├── IdentityService.*          users, roles, permissions, JWT            schema: identity
├── ProductService.*           products, categories, departments, images schema: product
├── RouteService.*             transfers and the product audit trail     schema: route
├── ApprovalService.*          approval requests and their execution     schema: approval
├── NotificationService.*      stored notifications, SignalR, WhatsApp   schema: notification
└── SharedServices             shared kernel: contracts, events, transactions, auth, storage
```

- **Module layout.** Each module keeps a clean-architecture split: `Domain`, `Application`, `Infrastructure`, and `API` (a class library with the module's controllers). Inside a module, CQRS goes through **MediatR** and validation through **FluentValidation**.
- **No cross-references.** Modules never reference each other's internals. They talk through contracts in `SharedServices/Contracts` and through in-process MediatR events.
- **One transaction per request.** All module `DbContext`s share one connection per request, and a transactional request spans modules. For example, an approval and the action it executes commit or roll back together.
- **History.** The project names (`ProductService`, …) come from an earlier version that ran as five microservices behind an API gateway with RabbitMQ.

**Tech:**
- ASP.NET Core 10, EF Core 10 + Npgsql (PostgreSQL 15), MediatR, FluentValidation, SignalR, Serilog → Seq.
- UI: Bootstrap 5.3, jQuery, DataTables, Chart.js and Font Awesome, with its own design system in `design/` and `wwwroot/css/tokens.css`.

## Getting started

### Run locally

Requirements: the .NET 10 SDK and a PostgreSQL server.

```bash
dotnet user-secrets --project InventoryManagement.Web set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=inventory;Username=postgres;Password=<password>"
dotnet user-secrets --project InventoryManagement.Web set "Jwt:Key" "<at least 32 characters>"
dotnet run --project InventoryManagement.Web
```

Open http://localhost:5051. `GET /health` reports whether the database is reachable.

- The database and every module's schema are created by migrations at startup. Roles and permissions are seeded, along with an initial Admin account (`IdentityService.Infrastructure/Data/IdentityDbContext.cs`).
- Change that account's password after the first sign-in, or replace the seed before deploying.

### Run with Docker

```bash
cp .env.example .env        # fill in DB_PASSWORD, JWT_SECRET_KEY, SEQ_ADMIN_PASSWORD, ...
docker compose up -d --build
```

The stack runs:

| Service | Purpose | Port |
|---|---|---|
| `postgres` | the database | 5432 |
| `app` | the application | — |
| `nginx` | reverse proxy | 80 / 443; 5001 for API-key clients (product API only) |
| `seq` | logs | 5342 |
| `cloudflared` | optional public access through a Cloudflare Tunnel (`COMPOSE_PROFILES=tunnel`) | — (outbound only) |

- Uploaded images go to `./storage/images` and data-protection keys to `./storage/keys`. Both must be writable by the container user (uid 1654).
- Without the keys folder, every redeploy signs everyone out.

### Configuration

| Setting | Environment variable | Notes |
|---|---|---|
| Database | `ConnectionStrings__DefaultConnection` | compose builds it from `DB_*` |
| JWT signing key | `Jwt__Key` (`JWT_SECRET_KEY`) | 32+ characters |
| API keys | `ApiKeys__0__Key`, `…ServiceName`, `…Permissions__0` | for system-to-system clients |
| WhatsApp | `WHATSAPP_API_TOKEN`, `WHATSAPP_GROUP_ID` | optional |
| Time zone | `TZ` | timestamps are stored in local time |

## API

The JSON API lives under `/api` and accepts three kinds of authentication:
- `Authorization: Bearer <jwt>`, issued by `POST /api/auth/login`;
- `X-Api-Key` for configured services;
- the UI's cookie, which also needs the anti-forgery header for unsafe methods.

| Area | Endpoints |
|---|---|
| Products | `GET/POST /api/products`, `GET/PUT/DELETE /api/products/{id}`, `GET /api/products/search/inventory-code/{code}`, `PUT /api/products/{id}/inventory-code` |
| Categories, departments | `GET/POST /api/categories`, `/api/departments` (+ `/paged`, `/{id}`) |
| Transfers | `POST /api/inventoryroutes/transfer`, `PUT /api/inventoryroutes/{id}/complete`, `GET /api/inventoryroutes/product/{productId}` |
| Approvals | `GET /api/approvalrequests`, `POST /api/approvalrequests/{id}/approve` / `reject` |
| Auth and users | `POST /api/auth/login`, `/refresh`, `GET /api/auth/me`, user and role management |
| Notifications | `GET /api/notifications`, `/unread-count`, `POST /api/notifications/mark-all-read` |

The API applies the same two-tier permission rule as the UI:
- with only the base permission, a write returns **202** with `{ approvalRequestId }`;
- without any permission it returns **403**;
- a concurrent change returns **409**.

## Development

```bash
dotnet build InventoryManagement.sln

# add a migration (each Infrastructure project is its own startup project)
dotnet ef migrations add <Name> --project ProductService.Infrastructure --startup-project ProductService.Infrastructure
```

- **Translations.** The UI text is keyed by its English wording. Azerbaijani translations are in `InventoryManagement.Web/Resources/i18n/az.json`.
- **Frontend libraries.** They are pinned in `InventoryManagement.Web/libman.json` (`libman restore`).
- **Architecture notes.** `CLAUDE.md` has detailed notes on the conventions: module boundaries, transactions, approvals, live updates and localisation.

## CI/CD

GitHub Actions:
- `ci.yml` builds the solution, checks NuGet packages for known vulnerabilities and builds the Docker image on every pull request and every push to `master`.
- `cd.yml` publishes the image to GHCR after a green build on `master`. A self-hosted runner then deploys it with `deploy/deploy.sh`, which takes a database backup, recreates the app, waits for the health check and rolls back automatically on failure.
