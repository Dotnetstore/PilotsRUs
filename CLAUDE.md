# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

PilotsRUs gives a complete economic structure to MSFS 2024 (Microsoft Flight Simulator). It is split into
three parts:

- **API** — a RESTful API, the base for everything else
- **Admin interface** — an ASP.NET Razor Pages app
- **User interface** — an Avalonia MVVM desktop app

## Current status

The solution scaffold plus a working infra layer are in place: Postgres/EF Core (via Aspire), JWT-based
login, and AdminLTE 4 styling are implemented — see "Authentication" and "Database" below. Scope is
deliberately **infra-only**: no business/domain entities (Pilot, Airline, Job, Aircraft, etc.) exist yet.
Those come once that domain is defined; don't invent them speculatively. When adding new projects, register
them in `PilotsRUs.slnx` (the XML-based solution format — add `<Project Path="..." />` entries, not a
`.sln` file).

## Project structure

- `PilotsRUs.API.WebApi` — the API project; references `Shared.SDK` and `ServiceDefaults`. Owns the EF Core
  model, `ApplicationDbContext`, and Identity/JWT wiring (see "Authentication")
- `PilotsRUs.Shared.SDK` — class library with all DTOs; connects to the API via `IHttpClientFactory`. Stays
  free of EF Core/Npgsql/ASP.NET Core-web dependencies since `User.App` (a desktop app) references it too
- `PilotsRUs.Admin.App` — ASP.NET Razor Pages admin interface, styled with AdminLTE 4, cookie-based browser
  session bridging an API-issued JWT; references `Shared.SDK` and `ServiceDefaults`
- `PilotsRUs.User.App` — Avalonia MVVM desktop app for end users; references `Shared.SDK`. Not orchestrated
  by Aspire since it's a standalone installed app, not a hosted service
- `PilotsRUs.AppHost` — the Aspire app host; provisions a Postgres container (resource `postgres`, database
  `pilotsrus`) and orchestrates `API.WebApi` and `Admin.App` for local development (resource names `api`
  and `admin`); only `api` references the database — `admin` talks to `api`, never to Postgres directly
- `PilotsRUs.ServiceDefaults` — shared Aspire wiring (OpenTelemetry, health checks, service discovery,
  resilience) referenced by every hosted service project; each service calls `builder.AddServiceDefaults()`
  and `app.MapDefaultEndpoints()` in `Program.cs`

Every project above has a matching `<ProjectName>.Tests` xUnit project, except `ServiceDefaults` (pure
wiring, nothing to unit test). `AppHost.Tests` is an Aspire integration test project that boots the whole
distributed application via `DistributedApplicationTestingBuilder` and asserts against real resources —
run it to verify orchestration wiring after touching `AppHost.cs` or either hosted service's `Program.cs`.

## Authentication

Backend-for-frontend pattern: `Admin.App` never validates a JWT itself.

1. `POST /auth/login` on `API.WebApi` (`Features/Auth/LoginEndpoint.cs`) checks credentials via
   ASP.NET Core Identity (`SignInManager`/`UserManager`) and returns a JWT (`Features/Auth/JwtTokenService.cs`)
   signed with `Jwt:Key` (set via `dotnet user-secrets`, never committed — `Jwt:Issuer`/`Jwt:Audience` are
   plain values in `appsettings.json`).
2. `Admin.App`'s `Pages/Account/Login.cshtml.cs` posts to that endpoint via the named `"Api"` HttpClient,
   then stores the returned JWT inside its own cookie auth session via `AuthenticationProperties.StoreTokens`
   (retrieved later with `HttpContext.GetTokenAsync("access_token")`) — this is the standard ASP.NET Core
   mechanism for stashing an external token inside a cookie, not a bespoke claim.
3. `Admin.App`'s `Infrastructure/BearerTokenHandler.cs` (a `DelegatingHandler` on the `"Api"` HttpClient)
   reads that token via `IHttpContextAccessor` and attaches it as `Authorization: Bearer <token>` on every
   outgoing call to the API.
4. `API.WebApi` validates the bearer token via `AddJwtBearer` (wired in
   `Extensions/AuthServiceCollectionExtensions.cs`); protect new endpoints with `.RequireAuthorization()`.
   `GET /auth/me` is a minimal example of a protected endpoint.

**Local dev login**: in Development, `API.WebApi` seeds a default admin user on startup
(`Data/DevelopmentDataSeeder.cs`) — `admin@pilotsrus.local` / `P@ssw0rd123!`. There's no registration
endpoint yet (infra-only scope).

**Gotcha to remember**: don't read `IConfiguration`/build option values eagerly in extension methods called
from `Program.cs` before `builder.Build()` (e.g. `builder.Configuration.GetSection(...).Get<T>()` captured
in a closure) — that snapshot misses configuration sources added later (integration tests add
`ConnectionStrings`/`Jwt` overrides via `WebApplicationFactory.ConfigureAppConfiguration`, which only takes
effect after `Program.cs`'s own top-level code has already run). Resolve `IOptions<T>` lazily from DI
instead, as `AddApplicationJwtAuth` does for `JwtBearerOptions`.

## Architecture principles

- Modular monolith with vertical slice architecture, following SOLID principles
- Builder pattern, Options pattern, and repository pattern where applicable
- .NET Aspire as the place all logging occurs
- Prefer loading database/other packages if already available in the environment, rather than assuming
- Health checks wherever possible
- Unit, integration, and end-to-end tests wherever possible; test project folder structure should mirror
  the structure of the project under test

## Database

PostgreSQL via EF Core, provisioned as an Aspire-managed container (see "Project structure"). Identity's
`UserStore`/`RoleStore` require a scoped `DbContext`, which has no supported way to run through
`IDbContextFactory` — so `AuthServiceCollectionExtensions.AddApplicationIdentity` registers
`ApplicationDbContext` two ways against the Aspire-provided connection string (`"pilotsrus"`): a scoped
context via `builder.AddNpgsqlDbContext` (for Identity, plus it gets Aspire's connection resiliency and an
auto-registered health check under `/health`) and `IDbContextFactory<ApplicationDbContext>` for
CLAUDE.md's factory convention, for any future non-Identity repository code. Both coexist without
conflict since they resolve via different service types — don't collapse them into one.

- Model: `Data/ApplicationDbContext.cs` (`IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`),
  `Data/ApplicationUser.cs`, `Data/ApplicationRole.cs`. Only Identity's own tables exist (`AspNetUsers`,
  `AspNetRoles`, etc.) — no business tables yet.
- Migrations live in `Data/Migrations/`. `dotnet ef migrations add <Name> --project PilotsRUs.API.WebApi
  --output-dir Data/Migrations` (needs `Data/DesignTimeDbContextFactory.cs` since the context is registered
  via `AddNpgsqlDbContext`/`AddDbContextFactory` rather than the classic `AddDbContext<T>(options => ...)`
  pattern EF tools auto-discover).
- Applied automatically at startup, gated to `IsDevelopment()` (`Program.cs`) — no separate migrator
  resource; revisit if/when a real deployment pipeline exists.

## Deployment

- API and Admin interface run from Docker
- User interface is a standalone app with an installer (not containerized)

## Front-end assets (Admin.App)

Third-party front-end libraries are vendored (not CDN-linked) into `wwwroot/lib/<name>/`, matching the
ASP.NET Core template's default LibMan-style layout — `bootstrap/`, `jquery/`, `jquery-validation/`,
`jquery-validation-unobtrusive/`, and `adminlte/` (AdminLTE 4, fetched via `npm pack admin-lte@4` and
copying only `dist/css` + `dist/js`, no image/demo assets). AdminLTE 4 requires Bootstrap 5 (already
vendored at 5.3.3) and ships no icon library of its own — none is vendored here yet, so avoid `<i class="bi
...">`/Font Awesome-style icon markup until one is deliberately added. `_Layout.cshtml` uses AdminLTE's
`app-wrapper`/`app-header`/`app-sidebar`/`app-main`/`app-footer` shell; `Pages/Account/Login.cshtml` sets
`Layout = null` and uses AdminLTE's `login-page`/`login-box`/`login-card-body` classes directly, since the
authenticated sidebar shell doesn't belong on the login screen.

## Build configuration

`Directory.Build.props` applies these settings to every project in the solution:

- Target framework: `net10.0`
- Nullable reference types and implicit usings are enabled by default
- Debug builds treat warnings as errors (`WarningLevel` 9999, `TreatWarningsAsErrors` true) — fix warnings
  rather than suppress them when building in Debug
- Every project `Foo` automatically grants `InternalsVisibleTo` to `Foo.Tests` and to
  `Dotnetstore.Intranet.AppHost.Tests` — follow the `<ProjectName>.Tests` naming convention for test
  projects so this works without extra configuration
- A `UserSecretsId` is already defined at the directory level for user-secrets-based local configuration

## Commands

```
dotnet build PilotsRUs.slnx
dotnet test PilotsRUs.slnx
dotnet test path/to/Project.Tests -filter "FullyQualifiedName~TestName"   # run a single test
dotnet run --project PilotsRUs.AppHost                                   # run API, Admin + Postgres via Aspire
dotnet ef migrations add <Name> --project PilotsRUs.API.WebApi --output-dir Data/Migrations
```
