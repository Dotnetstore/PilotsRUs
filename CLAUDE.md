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
login with rotating refresh tokens, role-based authorization (a single "Admin" role) with a full Users CRUD
management screen in `Admin.App`, and AdminLTE 4 styling are implemented — see "Authentication",
"Authorization and Users CRUD", and "Database" below. **Manufacturer** → **Aircraft Model** → **Country** →
**Airport** → **Software Developer** → **Aircraft** → **Schedule Template** → **Schedule** are all shipped.
`Schedule` (see "Schedules" below) is generated automatically from `ScheduleTemplate` recurrence patterns by
`ScheduleGenerationBackgroundService`, the solution's first background service — there's no manual
Create/Update/Delete for it. `User.App` now has its own real architecture (DI, EF Core/SQLite, MVVM) and its
first feature — end-user **Account** registration/login/logout (see "Accounts" and "User.App architecture"
below), a separate identity concept from Admin's `ApplicationUser`. Other domain entities (Pilot, Airline,
Job, etc.) still don't exist; add them only as their own scoped features, following the established CRUD
pattern where it fits. When adding new projects, register them in `PilotsRUs.slnx`
(the XML-based solution format — add `<Project Path="..." />` entries, not a `.sln` file).

## Project structure

- `PilotsRUs.API.WebApi` — the API project; references `Shared.SDK` and `ServiceDefaults`. Owns the EF Core
  model, `ApplicationDbContext`, and Identity/JWT wiring (see "Authentication")
- `PilotsRUs.Shared.SDK` — class library with all DTOs; connects to the API via `IHttpClientFactory`. Stays
  free of EF Core/Npgsql/ASP.NET Core-web dependencies since `User.App` (a desktop app) references it too
- `PilotsRUs.Admin.App` — ASP.NET Razor Pages admin interface, styled with AdminLTE 4, cookie-based browser
  session bridging an API-issued JWT; references `Shared.SDK` and `ServiceDefaults`
- `PilotsRUs.User.App` — Avalonia MVVM desktop app for end users; references `Shared.SDK`. Not orchestrated
  by Aspire since it's a standalone installed app, not a hosted service. Has its own DI host
  (`Microsoft.Extensions.Hosting`) and local SQLite database via EF Core (`Data/UserAppDbContext.cs`) — see
  "User.App architecture" below for the full setup
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

Backend-for-frontend pattern: `Admin.App` never validates a JWT itself. Access tokens (JWT, short-lived,
default 60 min) are paired with opaque refresh tokens (DB-backed, rotating, default 14 days) so a session
can survive well past the access token's expiry without re-prompting for credentials.

1. `POST /auth/login` on `API.WebApi` (`Features/Auth/AuthEndpoints.cs`) checks credentials via ASP.NET
   Core Identity (`SignInManager`/`UserManager`), mints a JWT (`Features/Auth/JwtTokenService.cs`, signed
   with `Jwt:Key` — set via `dotnet user-secrets`, never committed; `Jwt:Issuer`/`Jwt:Audience` are plain
   values in `appsettings.json`), and issues a refresh token (`Features/Auth/RefreshTokenService.cs`) tied
   to a fresh `FamilyId` lineage. Both come back in one `LoginResponse`.
2. `Admin.App`'s `Pages/Account/Login.cshtml.cs` posts to that endpoint via the named `"Api"` HttpClient,
   then stores both tokens (plus the access token's expiry) inside its own cookie auth session via
   `AuthenticationProperties.StoreTokens` (retrieved later with `HttpContext.GetTokenAsync("access_token"
   | "refresh_token" | "expires_at")`) — the standard ASP.NET Core mechanism for stashing external tokens
   inside a cookie, not bespoke claims.
3. `Admin.App`'s `Infrastructure/BearerTokenHandler.cs` (a `DelegatingHandler` on the `"Api"` HttpClient)
   attaches the access token as `Authorization: Bearer <token>` on every outgoing call. On a 401, it POSTs
   the stored refresh token to `/auth/refresh`, re-signs the cookie with the new pair (mid-request, via
   `IHttpContextAccessor` — safe as long as `Response.HasStarted` is still false), and retries the original
   request once. Concurrent 401s within the same session serialize onto one refresh call via a
   `SemaphoreSlim` keyed by user identity, to avoid two requests both trying to consume the same
   soon-to-be-rotated-away refresh token.
4. `POST /auth/refresh` and `POST /auth/logout` are both `.AllowAnonymous()` — the refresh token itself,
   validated against the DB, is the credential; requiring a valid (non-expired) access token to call them
   would defeat their purpose. `API.WebApi` validates the bearer token on protected endpoints via
   `AddJwtBearer` (wired in `Extensions/AuthServiceCollectionExtensions.cs`, with `ClockSkew = TimeSpan.Zero`
   — the 5-minute default would let an "expired" token keep working); protect new endpoints with
   `.RequireAuthorization()`. `GET /auth/me` is a minimal example.

**Refresh token rotation**: every `/auth/refresh` call (`RefreshTokenService.RotateAsync`) issues a new
token and revokes the presented one (`RevokedReason: "rotated"`, `ReplacedByTokenId` pointing forward).
Presenting an already-revoked token is treated as theft and revokes the *entire* `FamilyId` lineage
(`RevokedReason: "reuse-detected"`), forcing re-login — this is why `Admin.App` retries with the
*currently-stored* access token before calling `/auth/refresh` again (see `BearerTokenHandler`), so two
concurrent requests never both try to rotate the same token. Tokens are stored hashed (SHA-256 of the raw
opaque value) — never plaintext, since the raw token is a live bearer credential.

**Local dev login**: in Development, `API.WebApi` seeds a default admin user on startup
(`Data/DevelopmentDataSeeder.cs`) — Hans Sjödin, `hasse29@hotmail.com` / `admin`. There's no registration
endpoint yet (infra-only scope).

**User identity**: `ApplicationUser` (`Data/ApplicationUser.cs`) extends `IdentityUser<Guid>` with required
`FirstName`/`LastName` (surfaced via `GivenName`/`Surname` JWT claims and returned from `/auth/me`, so
`Admin.App` can greet the user by name instead of raw email — see `CurrentUserResponse`).
`IdentityOptions.User.RequireUniqueEmail = true` is set unconditionally in `AddApplicationIdentity`, and
every user's `UserName` is set equal to `Email` by convention at both current construction sites (the
seeder and the test factory) — there's no custom `IUserValidator<ApplicationUser>` enforcing this yet since
there's no registration endpoint for untrusted input to police; add one (registered additively alongside
Identity's built-in validator — Identity runs every registered `IUserValidator<TUser>`, not just one) when
registration ships. `IdentityOptions.Password` is separately relaxed (`RequiredLength = 4`, all complexity
requirements off) but **only inside an `if (builder.Environment.IsDevelopment())` guard** in
`AddApplicationIdentity` — it must stay Development-only since it exists purely to allow the weak seeded
dev password; don't hoist it out of that guard even though `RequireUniqueEmail` above it is unconditional.
Revisit once real user registration exists and needs to enforce a real policy against user-chosen
passwords.

**Password hashing**: `Features/Auth/Argon2PasswordHasher.cs` replaces Identity's default PBKDF2-based
`PasswordHasher<TUser>` (registered via explicit non-`TryAdd` `AddSingleton` in `AddApplicationIdentity` —
singleton, not scoped, since it holds no per-instance state, same reasoning as `IJwtTokenService`). Argon2id,
parameters bound from configuration via `Argon2Options`/section `"Argon2"` (OWASP 2023 minimum baseline by
default: `m=19456` KiB, `t=2`, `p=1`), stored as a self-describing PHC string
(`$argon2id$v=19$m=...,t=...,p=...$<salt>$<hash>`) — every parameter that affects the computed hash,
*including the output hash length*, is parsed back out of the stored string on verify rather than assumed
from current config, so `VerifyHashedPassword` correctly detects drift and returns
`PasswordVerificationResult.SuccessRehashNeeded`, letting Identity transparently rehash on the user's next
successful login with no manual migration. `VerifyHashedPassword` never throws for a malformed/incompatible
`hashedPassword` (e.g. a legacy hash from Identity's old default hasher, or a corrupted value) — parsing
failures are caught and treated as `PasswordVerificationResult.Failed`, matching the contract
`IPasswordHasher<TUser>` callers (`UserManager`/`SignInManager`) rely on and Identity's own default hasher
upholds.

**Gotchas to remember**:
- Don't read `IConfiguration`/bind option values eagerly in extension methods called from `Program.cs`
  before `builder.Build()` (e.g. `builder.Configuration.GetSection(...).Get<T>()` captured in a closure) —
  that snapshot misses configuration sources added later (integration tests add `ConnectionStrings`/`Jwt`
  overrides via `WebApplicationFactory.ConfigureAppConfiguration`, which only takes effect after
  `Program.cs`'s own top-level code has already run). Resolve `IOptions<T>` lazily from DI instead, as
  `AddApplicationJwtAuth` does for both `JwtBearerOptions` and `RefreshTokenOptions`.
- `RefreshTokenService`'s family-revocation uses a load-then-`SaveChangesAsync` update, not
  `ExecuteUpdateAsync` — the latter isn't supported by EF Core's InMemory provider (used in
  `ApiFactory`-based tests), and a lineage only ever has a handful of rows, so the extra round-trip is
  free against Postgres too.

## Authorization and Users CRUD

A single "Admin" role (name defined once, in `Shared.SDK/Auth/AuthConstants.AdminRoleName`, since
`API.WebApi` and `Admin.App` don't share an EF/Identity dependency) gates user management. There is no
"member"-facing functionality yet — the role exists purely to protect the Users CRUD screen.

- **Seeding**: `Data/RoleSeeder.cs` creates the Admin role unconditionally (every environment, not just
  Development) on every startup, via a `RoleManager<ApplicationRole>` scope in `Program.cs` — must run
  *after* the Development-only migration block, since it queries `AspNetRoles`, which doesn't exist until
  migrations have applied. In Development, `DevelopmentDataSeeder.SeedDevelopmentAdminAsync` also assigns
  the role to the seeded dev admin (including on the already-exists branch, so re-running against an older
  DB still elevates the existing user rather than skipping).
- **JWT/cookie propagation**: `JwtTokenService` already added one `ClaimTypes.Role` claim per role to the
  access token. `LoginResponse` now also carries `Roles` directly (both `/auth/login` and `/auth/refresh`
  populate it from `UserManager.GetRolesAsync`), so `Admin.App`'s `Login.cshtml.cs` can add matching
  `ClaimTypes.Role` claims to the cookie principal at login time without an extra round-trip.
  `BearerTokenHandler`'s refresh path reuses the existing `httpContext.User` principal unchanged, so a
  role revoked mid-session only takes effect on the user's next full re-login.
- **Enforcement**: both sides register an `"AdminOnly"` policy (`policy.RequireRole(AuthConstants.AdminRoleName)`)
  via `AddAuthorizationBuilder()`. `API.WebApi`'s `/users/*` endpoints (`Features/Users/UserEndpoints.cs`)
  require it directly on the route group. `Admin.App` gates the whole `Pages/Users` folder via
  `AddRazorPages(options => options.Conventions.AuthorizeFolder("/Users", "AdminOnly"))` — a non-admin
  hitting any `/Users/*` URL directly gets redirected to `AccessDeniedPath` (`/Account/Login`), not just
  hidden from the nav.
- **Users CRUD** (`Features/Users/UserEndpoints.cs`, DTOs in `Shared.SDK/Users/`): standard list/get/create/
  update, plus "delete" implemented as **deactivate** (Identity's `LockoutEnabled`/`LockoutEnd`, reversible
  via a separate reactivate endpoint) rather than `UserManager.DeleteAsync` — no schema change needed since
  `AspNetUsers` already has lockout columns. Deactivating a user also calls
  `IRefreshTokenService.RevokeAllForUserAsync` so a still-valid refresh token can't silently renew their
  session; their already-issued JWT access token still works until its own (short) expiry, since JWTs
  aren't revocable — only refresh tokens are.
  - A **last-active-admin guard** (`CountOtherActiveAdminsAsync`, counting admins excluding the target who
    are *not* locked out) blocks both removing the Admin role via `PUT` and deactivating via `POST
    .../deactivate` when it would leave zero active admins — counting only *active* role-holders (not just
    role-holders) matters, since a role-holder who's already locked out can't exercise the role, so
    counting them as "remaining" would let the system reach a state where every Admin-role user is locked
    out and nobody can undo it.
  - Deactivating your own account is blocked separately (`ClaimsPrincipal` vs. route `id` comparison) —
    note the caller's user id claim is read via `ClaimTypes.NameIdentifier`, not
    `JwtRegisteredClaimNames.Sub`: ASP.NET Core's JWT bearer handler remaps the short `"sub"` claim to the
    long `ClaimTypes.NameIdentifier` name by default (`MapInboundClaims`), the same remapping `/auth/me`
    already relies on for `Email`/`GivenName`/`Surname`/`Role`.
  - Validation errors from `IdentityResult.Errors` are returned as a structured `UserValidationProblem`
    (`Field`/`Description` pairs, `IdentityError.Code` mapped to a field name via a small switch) rather
    than a flat string, so `Admin.App`'s Create/Edit pages can attach each error to the right form field via
    `asp-validation-for`. The last-active-admin conflict, by contrast, is a plain `Results.Conflict(string)`
    /`Results.BadRequest(string)` — which serializes as a JSON string literal, so callers must deserialize
    with `ReadFromJsonAsync<string>()`, not `ReadAsStringAsync()`, to avoid literal quote marks in the UI.
- **Admin.App pages** (`Pages/Users/Index|Create|Edit|Deactivate.cshtml(.cs)`): plain Bootstrap
  `.table`/`.card` markup, no DataTables or other JS grid plugin (none is vendored — see "Front-end assets"
  below). Edit only covers Email/FirstName/LastName/Admin-role — no password field (no self-service
  password reset flow exists yet). Reactivate is a same-page POST handler on `Index.cshtml`
  (`OnPostReactivateAsync`), not a separate confirm page, since it's fully reversible; Deactivate gets its
  own confirm-page precisely because it isn't.

## Manufacturers

The first business/domain entity in the solution — registering airplanes starts with registering their
manufacturers. `Data/Manufacturer.cs`: `Guid` PK (following `RefreshToken.Id`'s precedent — the only other
non-Identity entity in the codebase), required unique `Name`, optional `Code` (there's no official
standardized "manufacturer code" registry the way there is for ICAO/IATA airline/airport codes, so seeded
rows leave it null; admins fill it in later via Edit). Lives in the single `ApplicationDbContext` — no
separate DbContext for domain entities, same as `RefreshToken` (see "Database" below).

- **Seeding**: `Data/ManufacturerSeeder.cs` seeds 10 real-world manufacturers relevant to MSFS 2024 aircraft
  (Boeing, Airbus, Cessna, Cirrus, Embraer, Bombardier, Piper, Beechcraft, Mooney, Diamond Aircraft) on every
  startup, unconditionally (every environment, not just Development) and idempotently — same pattern as
  `RoleSeeder`, and for the same reason (reference data needed everywhere). Unlike `RoleSeeder`, this isn't
  an Identity concern, so it resolves `IDbContextFactory<ApplicationDbContext>` directly from `app.Services`
  (already a singleton registration) rather than going through `CreateScope()` — the first real use of the
  "future non-Identity repository code" convention called out in "Database" below. Must run after the
  `IsDevelopment()` migration block in `Program.cs`, same ordering constraint `RoleSeeder` has (querying a
  table before migrations create it fails startup).
- **Authorization**: `/manufacturers/*` endpoints (`Features/Manufacturers/ManufacturerEndpoints.cs`) use
  `.RequireAuthorization()` with **no policy name** — deliberately *not* `"AdminOnly"`, since manufacturers
  are reference/master data any authenticated user can manage, unlike Users. `Admin.App`'s
  `Pages/Manufacturers` folder is gated the same way (`AuthorizeFolder("/Manufacturers")`, no policy
  argument), and its nav link in `_Layout.cshtml` is conditioned on `User.Identity?.IsAuthenticated`, not
  `User.IsInRole("Admin")`.
- **CRUD shape**: list/get/create/update/**hard delete** — no deactivate/reactivate, unlike Users. There's no
  session/lockout state tied to a lookup entity the way there is to a user account, so a soft-delete flag
  would just be a boolean with no behavioral difference from a real delete. Deleting a Manufacturer that
  still has `AircraftModel` rows is now blocked (see "Aircraft Models" below) — the FK-`OnDelete` question
  this section originally deferred has been resolved: `DeleteBehavior.Restrict`, not `Cascade`.
- **Error handling**: no `IdentityResult` is involved here, so this doesn't reuse `UserValidationProblem`/
  `UserValidationError` (those are Identity-specific). The only validation rule (duplicate `Name`) uses
  `Results.Conflict(string)`, same as Users' last-active-admin guard — `Admin.App` reads it via
  `ReadFromJsonAsync<string>()` the same way `Edit.cshtml.cs` already does for Users. Create/Update also wrap
  `SaveChangesAsync` in a `catch (DbUpdateException)` returning the same `Conflict`, closing the TOCTOU gap
  between the pre-check `AnyAsync` and the actual insert/update (two concurrent requests for the same `Name`
  would otherwise surface as an unhandled 500 from the unique-index violation).
- **No repository/service abstraction** — endpoints inline directly against a per-request
  `ApplicationDbContext` from `IDbContextFactory<ApplicationDbContext>.CreateDbContextAsync()`, matching how
  Users' endpoints inline directly against `UserManager` (no extra layer for a single vertical slice).
- **Admin.App pages** (`Pages/Manufacturers/Index|Create|Edit|Delete.cshtml(.cs)`): same plain Bootstrap
  `.table`/`.card` style as Users. `Delete.cshtml(.cs)` mirrors `Deactivate.cshtml.cs`'s confirm-page shape
  but calls `DELETE` and is genuinely destructive/non-reversible, unlike a lockout toggle.

## Aircraft Models

Manufacturer's first dependent entity, and the first foreign-key relationship between two non-Identity
entities in the solution — each `AircraftModel` (e.g. "Boeing 737 MAX 8") belongs to exactly one
`Manufacturer`. `Data/AircraftModel.cs`: `Guid` PK, required `ManufacturerId` (mutable `{ get; set; }`, not
`init`-only, since reassigning a model to a different manufacturer is a legitimate Edit operation), required
`Name`, optional `IcaoTypeDesignator`. Unlike `Manufacturer.Code`, ICAO type designators (ICAO Doc 8643) ARE
an official standardized registry, so seeded rows carry real best-effort codes rather than staying null.

- **Naming**: the entity is `AircraftModel`, deliberately not bare `Model` — `@Model` is the pervasive Razor
  implicit reference to the current `PageModel` instance in every `.cshtml` file in this codebase. This
  isn't just a naming preference: a `foreach` loop variable literally named `model` (lowercase) in a
  `.cshtml` file breaks the Razor parser outright (`RZ2001`/`RZ2005`/`RZ1011` — Razor's `@model` directive
  grammar matches `@model.SomeProperty` anywhere in the file, not just the top-of-file directive), hit and
  fixed while building `Pages/AircraftModels/Index.cshtml`. Avoid `model`/`Model` as a loop variable or local
  identifier in any `.cshtml` file for this reason.
- **Uniqueness and FK behavior**: `Name` is unique **per manufacturer** (composite index on
  `(ManufacturerId, Name)`), not globally — two different manufacturers can reuse a model name.
  `IcaoTypeDesignator` has its own filtered unique index (`WHERE "IcaoTypeDesignator" IS NOT NULL`) since
  ICAO type designators are globally unique when present, but the column itself is nullable. The FK to
  `Manufacturer` uses `DeleteBehavior.Restrict` (not `RefreshToken.UserId`'s `Cascade` — an `AircraftModel`
  is itself meaningful reference data, not disposable session state), paired with an explicit pre-check in
  `ManufacturerEndpoints.cs`'s `DeleteManufacturer` (`AnyAsync` scoped to the target's `ManufacturerId`)
  that returns `Results.Conflict` before the delete, so a manufacturer with existing models can't be removed
  without either deleting or reassigning them first — turning what would otherwise be an unhandled 500 (FK
  violation) into the same clean 409 pattern used elsewhere.
- **Seeding**: `Data/AircraftModelSeeder.cs` seeds a handful of models per seeded manufacturer, keyed by
  manufacturer name (resolved to `ManufacturerId` via a dictionary lookup, skipping any manufacturer name
  that isn't found rather than failing startup). Must run **after** `ManufacturerSeeder` in `Program.cs` for
  exactly that reason — same unconditional/idempotent/every-environment pattern otherwise.
- **Authorization**: same as Manufacturers — `.RequireAuthorization()` with no policy name on
  `/aircraft-models/*`, `AuthorizeFolder("/AircraftModels")` with no policy argument on the Admin.App side,
  nav link gated on `User.Identity?.IsAuthenticated`.
- **Manufacturer-existence validation**: `POST`/`PUT` on `Features/AircraftModels/AircraftModelEndpoints.cs`
  look up the referenced `ManufacturerId` first and return `Results.BadRequest(string)` if it doesn't exist,
  before the duplicate-name check — a plain string body, read the same `ReadFromJsonAsync<string>()` way as
  every other conflict/bad-request message in this codebase.
- **Admin.App pages** (`Pages/AircraftModels/Index|Create|Edit|Delete.cshtml(.cs)`): same structure as
  Manufacturers, plus the first `<select>`/`asp-items`/`SelectListItem`-driven dropdown in the codebase (no
  earlier precedent existed) — `Create`/`Edit`'s PageModel populates a `List<SelectListItem>` from
  `GET /manufacturers` and binds it via `asp-items="Model.ManufacturerOptions"`. This is the reference
  pattern for any future FK-driven form (e.g. a later Aircraft → AircraftModel relationship).

## Countries

A standalone reference/lookup entity — no FK to anything yet (unlike `AircraftModel`). `Data/Country.cs`:
`Guid` PK, required unique `Name`, required unique `IsoAlpha2Code` (2 letters), required unique
`IsoAlpha3Code` (3 letters). Unlike `Manufacturer.Code`, ISO 3166-1 is a stable, authoritative registry, so
both codes are intended to always be populated — never left null the way `Manufacturer.Code` is.

- **Three independent uniqueness rules**: `Name`, `IsoAlpha2Code`, and `IsoAlpha3Code` each have their own
  unique index and their own pre-check in `Features/Countries/CountryEndpoints.cs` (`FindConflictAsync`,
  checked in that order, first match wins) — more than Manufacturer/AircraftModel's single-uniqueness-rule
  shape. Create/Update still fall back to a shared `catch (DbUpdateException)` closing the same TOCTOU gap.
- **Case normalization**: `IsoAlpha2Code`/`IsoAlpha3Code` are normalized via `.ToUpperInvariant()` before
  the uniqueness checks and the save on both Create and Update, so `"us"` and `"US"` can't end up as two
  distinct rows — the first genuinely new normalization concern versus Manufacturer/AircraftModel, whose
  single string fields didn't need it.
- **Seeding**: `Data/CountrySeeder.cs` follows the same unconditional/idempotent/every-environment pattern
  as `ManufacturerSeeder`/`AircraftModelSeeder`, called from `Program.cs` after `AircraftModelSeeder` (no
  ordering *requirement* — Country has no FK to anything — just grouped there for readability). Seeds 195
  sovereign states (Name/Alpha2/Alpha3) — the commonly-understood "countries of the world" list, not the
  full ~249-entry ISO 3166-1 registry with dependent territories (Puerto Rico, Hong Kong, etc.), which was
  the scope originally discussed but not what ended up populated.
- **Test data gotcha**: because the seeder now runs for every `ApiFactory`-backed test too, `CountryEndpointsTests`
  can't use real country names/codes (they'd collide with the 195 seeded rows) — it uses ISO 3166-1's
  reserved "user-assigned" code ranges (`QM`-`QZ`, `XA`-`XZ`), which are guaranteed by the standard itself to
  never be assigned to a real country. Follow the same convention for any future test that creates a
  `Country`.
- **Authorization**: same as Manufacturers/Aircraft Models — `.RequireAuthorization()` with no policy name
  on `/countries/*`, `AuthorizeFolder("/Countries")` with no policy argument on the Admin.App side, nav link
  gated on `User.Identity?.IsAuthenticated` (added as a third item in the same shared `@if` block that
  already wraps Manufacturers + Aircraft Models in `_Layout.cshtml`, not a new `@if`).
- **Hard delete, now guarded**: `DeleteCountry` blocks deletion (409) while any `Airport` still references
  the country (see "Airports" below) — the same `Restrict` + pre-check pattern `DeleteManufacturer` already
  had for `AircraftModel`. This closed the exact gap this section's comment used to flag as deferred.
- **Admin.App pages** (`Pages/Countries/Index|Create|Edit|Delete.cshtml(.cs)`): same structure as
  Manufacturers — no dropdown needed (no FK).

## Airports

`Country`'s first dependent entity. `Data/Airport.cs`: `Guid` PK, required `Name` (**not** unique — real
airport names collide often, e.g. many small airports are literally named "Municipal Airport"), required
unique `IcaoCode` (4 letters — the true natural key, always present for a real airport), optional
`IataCode` (3 letters, unique when present — many smaller/regional/private airports genuinely have none),
required `City` (plain string), required `CountryId` (FK to `Country`, `DeleteBehavior.Restrict`, chosen
via dropdown in Admin.App).

- **Seeding, and its data-quality lesson**: `Data/AirportSeeder.cs` follows the same
  unconditional/idempotent/every-environment pattern as the other seeders, called after `CountrySeeder`
  (resolves `CountryId` by the country's `IsoAlpha2Code` — a short, hand-typeable business key rather than a
  raw Guid, so whoever edits `SeedAirports` doesn't need to look up and paste GUIDs). The seed array is
  **hand-maintained data, not machine-generated** — when it was first populated, it contained 5 accidental
  duplicate ICAO codes (two real airports mistakenly given the same code, plus a few fully-duplicated rows)
  that would have thrown an unhandled `DbUpdateException` at startup (two rows with the same unique ICAO in
  one `AddRange`/`SaveChangesAsync` batch). Fixed both ways: the bad data was corrected, **and**
  `AirportSeeder.SeedAsync` itself was hardened to track ICAO/IATA codes already queued *within the same
  pass* (`HashSet<string>.Add(...)` returning `false` on a repeat), not just codes already committed to the
  DB — so a future accidental duplicate added by hand degrades to "one of the two rows silently skipped"
  instead of crashing the app. Keep this defensive pattern if `SeedAirports` grows further.
- **Authorization**: same as Manufacturers/Aircraft Models/Countries — `.RequireAuthorization()` with no
  policy name on `/airports/*`, `AuthorizeFolder("/Airports")` with no policy argument on the Admin.App
  side, nav link in the same shared authenticated-user `@if` block in `_Layout.cshtml`.
- **Country-existence validation**: `POST`/`PUT` on `Features/Airports/AirportEndpoints.cs` look up the
  referenced `CountryId` first and return `Results.BadRequest(string)` if it doesn't exist, mirroring
  `AircraftModelEndpoints`'s `ManufacturerId`-exists check.
- **Code normalization**: `IcaoCode`/`IataCode` are normalized via `.ToUpperInvariant()` before the
  uniqueness checks and the save, same as `Country`'s ISO codes.
- **Test data convention**: `AirportEndpointsTests` uses synthetic `ZZT`-prefixed ICAO/IATA codes (e.g.
  `ZZTA`), since `AirportSeeder` is expected to carry real airport data. Unlike `Country` (ISO 3166-1 has a
  formally reserved "user-assigned" range), airport codes have no equivalent official reserved block, so
  this is a best-effort convention, not a guarantee — follow it for any future test that creates an
  `Airport`.
- **Admin.App pages** (`Pages/Airports/Index|Create|Edit|Delete.cshtml(.cs)`): same structure as
  AircraftModels, with a Country `<select>` dropdown mirroring the Manufacturer picker exactly.

## Software Developers

A standalone reference/lookup entity introduced specifically to support "Aircraft" (below) — records which
company produced a specific airplane's MSFS add-on/software (e.g. PMDG, Fenix, FlyByWire, iniBuilds).
`Data/SoftwareDeveloper.cs`: `Guid` PK, required unique `Name` — deliberately minimal (no optional `Code`
field like `Manufacturer`, since nothing was asked for beyond a name).

- **No seed data** — unlike every other reference entity so far (Manufacturer/AircraftModel/Country/Airport),
  this one starts empty and stays that way per explicit user instruction; admins populate it via the UI as
  they register aircraft.
- **Authorization**: same as Manufacturers/Aircraft Models/Countries/Airports — `.RequireAuthorization()`
  with no policy name on `/software-developers/*`, `AuthorizeFolder("/SoftwareDevelopers")` with no policy
  argument on the Admin.App side, nav link in the same shared authenticated-user `@if` block in
  `_Layout.cshtml`.
- **Hard delete, guarded**: `DeleteSoftwareDeveloper` blocks deletion (409) while any `Aircraft` still
  references it — same `Restrict` + pre-check pattern as `DeleteManufacturer`/`DeleteCountry`.
- **Admin.App pages** (`Pages/SoftwareDevelopers/Index|Create|Edit|Delete.cshtml(.cs)`): same structure as
  Manufacturers minus the `Code` field — no dropdown needed (no FK).

## Aircraft

The entity that ties everything else together — a specific, individually registered airplane. `Data/Aircraft.cs`:
`Guid` PK, required unique `RegistrationNumber` (tail number, e.g. `N12345`, `G-ABCD` — the real-world
natural identity of a specific aircraft; normalized via `.ToUpperInvariant()` before uniqueness checks and
save, same convention as `Country`'s ISO codes/`Airport`'s ICAO/IATA codes, with a generous
`MaxLength(20)` since no single global tail-number format exists), required non-negative
`PassengerCapacityEconomy`/`PassengerCapacityBusiness`/`PassengerCapacityFirst` (`int`, defaulting to 0 at
the form level — e.g. a cargo-only aircraft has 0/0/0), required non-negative `CargoCapacityKg` (kilograms —
the standard aviation payload unit), required `AircraftModelId` (FK to `AircraftModel`, `Restrict`) and
required `SoftwareDeveloperId` (FK to `SoftwareDeveloper`, `Restrict`).

- **No seed data**, same as `SoftwareDeveloper` and for the same reason — Aircraft are real user-registered
  instances, not reference/master data, unlike Manufacturer/AircraftModel/Country/Airport.
- **Namespace/folder naming**: unlike every prior feature, the C# namespace/folder/route level uses the
  **plural `Aircrafts`** (`Features/Aircrafts/AircraftEndpoints.cs`, `Shared.SDK/Aircrafts/`,
  `Pages/Aircrafts/`, route `/aircrafts`) even though the entity type itself stays singular `Aircraft`. This
  is deliberate: "aircraft" is grammatically invariant (same singular/plural), so a namespace segment
  literally named `Aircraft` sharing a file with the type `Aircraft` risks a C# namespace-vs-type name
  collision — the same category of gotcha (not the same mechanism) as the `model`/`@model` Razor collision
  documented under "Aircraft Models" above. Using the plural namespace/folder — exactly the "singular type,
  plural namespace" pattern `AircraftModel`/`AircraftModels` already establishes safely — sidesteps it
  entirely. `ApplicationDbContext.Aircraft` (the `DbSet<Aircraft>` property) is fine as-is since properties
  aren't subject to this collision.
- **Response flattening**: `AircraftResponse` flattens `AircraftModelName` + `ManufacturerName` (via
  `.Include(a => a.AircraftModel).ThenInclude(m => m.Manufacturer)`) and `SoftwareDeveloperName`, mirroring
  how `AircraftModelResponse` already flattens `ManufacturerName`. The list endpoint materializes entities
  first (`.ToListAsync()`) and maps to `AircraftResponse` in-memory afterward — the flattening helper
  (`ToResponse`) isn't SQL-translatable, so it can't be used inside an EF `.Select()` the way inline
  `new AircraftModelResponse(...)` projections are elsewhere.
- **Two existence pre-checks**: `POST`/`PUT` on `Features/Aircrafts/AircraftEndpoints.cs` look up
  `AircraftModelId` and `SoftwareDeveloperId` first, both returning `Results.BadRequest(string)` if missing
  (mirroring `AircraftModelEndpoints`'s `ManufacturerId`-exists check), before the `RegistrationNumber`
  uniqueness check (409).
- **Authorization**: same as every other domain entity — `.RequireAuthorization()` with no policy name on
  `/aircrafts/*`, `AuthorizeFolder("/Aircrafts")` with no policy argument on the Admin.App side, nav link in
  the same shared authenticated-user `@if` block in `_Layout.cshtml`.
- **Retrofitted delete guards**: adding `Aircraft` resolved the two "once a future Aircraft entity
  references..." follow-ups this file used to flag as deferred — `DeleteAircraftModel`
  (`Features/AircraftModels/AircraftModelEndpoints.cs`) and `DeleteSoftwareDeveloper` both now block (409)
  while any `Aircraft` still references them, same `Restrict` + pre-check pattern as every other guarded
  delete in this codebase. `DeleteAircraft` itself is a genuine hard delete for now — nothing references
  `Aircraft` yet.
- **Admin.App pages** (`Pages/Aircrafts/Index|Create|Edit|Delete.cshtml(.cs)`): same structure as
  AircraftModels/Airports, but the first form with **two** `<select>`/`asp-items`/`SelectListItem` dropdowns
  in the same Create/Edit page — `AircraftModelOptions` (populated from `GET /aircraft-models`, label
  `"{ManufacturerName} {ModelName}"`) and `SoftwareDeveloperOptions` (populated from
  `GET /software-developers`, label = `Name`). `Delete.cshtml.cs` stays the plain "Delete failed." shape
  (no `Conflict`-message surfacing) since nothing depends on `Aircraft` yet.

## Schedule Templates

A reusable definition of a recurring flight — the first entity with **two foreign keys to the same table**
and the first with an **enum-typed field**. `Data/ScheduleTemplate.cs`: `Guid` PK, required `FlightNumber`
(max 10 chars, **not unique** — there's no `Airline` entity yet to scope uniqueness against, so this is the
first domain entity in the codebase with zero uniqueness rules and therefore no `Results.Conflict` path
anywhere in its Create/Update handlers), required `DepartureAirportId`/`ArrivalAirportId` (both FK to
`Airport`, `Restrict`, each with its own `HasOne(...).WithMany().HasForeignKey(...)` call and no inverse
collection navigation — same no-cascade-path concern as everywhere else, since nothing here uses `Cascade`),
required `AircraftId` (FK to `Aircraft`, `Restrict`), required `DistanceNauticalMiles` (`int`, manually
entered — `Airport` has no lat/long yet, so this can't be computed from the two chosen airports), required
`FlightTime` (`TimeSpan`, maps to Postgres' native `interval` type), required `Frequency`
(`ScheduleFrequency`, see below), required `StartDate` (`DateOnly`) — the anchor `ScheduleGenerator` (see
"Schedules" below) counts a template's recurrence pattern from: `EveryThirdDay` flies on `StartDate`,
`StartDate+3`, `StartDate+6`, etc. Added in a later migration (`AddScheduleTemplateStartDateAndSchedules`)
after `ScheduleTemplate` had already shipped without it — necessary once schedule generation needed a
concrete reference point for "every Nth day," which the original design left unspecified.

- **`ScheduleFrequency` lives in `Shared.SDK/ScheduleTemplates/`, not `Data/`**: `{ Daily, EverySecondDay,
  EveryThirdDay, EveryFourthDay, EveryFifthDay, EverySixthDay, Weekly }` — it has to be usable both by the
  EF entity (`API.WebApi` already references `Shared.SDK`) and directly by the DTOs
  (`CreateScheduleTemplateRequest`/`UpdateScheduleTemplateRequest`/`ScheduleTemplateResponse`) without
  duplicating the type, same "single definition shared via Shared.SDK" reasoning as
  `AuthConstants.AdminRoleName`.
- **First enum stored in the DB and returned over the wire**: `ApplicationDbContext` maps
  `entity.Property(s => s.Frequency).HasConversion<string>().HasMaxLength(20)` — stored as the member name
  (`"Daily"`), not the default int, so it survives future enum reordering and reads legibly straight out of
  the table. To match that on the API response side, `Program.cs` registers `JsonStringEnumConverter`
  globally via `ConfigureHttpJsonOptions` — safe since `ScheduleFrequency` is the only enum in any API DTO
  today, so this changes zero existing behavior.
  - **Gotcha**: this `ConfigureHttpJsonOptions` registration only affects the API's own request/response
    JSON pipeline (server-side minimal API model binding/`Results.Ok(...)`). Any `HttpClient`-side call —
    `ApiFactory`-based tests, and `Admin.App`'s `"Api"` named `HttpClient` — uses
    `System.Text.Json`'s *default* options and does **not** inherit it. Every
    `PostAsJsonAsync`/`ReadFromJsonAsync` call that sends or receives a `ScheduleTemplateResponse` or
    `Create`/`UpdateScheduleTemplateRequest` must pass an explicit
    `JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }` (a `private static readonly
    JsonOptions` field in each call site — `ScheduleTemplateEndpointsTests.cs` and every `Pages/
    ScheduleTemplates/*.cshtml.cs` PageModel) or deserializing the API's `"Daily"` string response throws.
    Follow this pattern for any future enum-bearing DTO.
- **Same-airport validation**: `POST`/`PUT` reject `DepartureAirportId == ArrivalAirportId` with
  `Results.BadRequest("Departure and arrival airport cannot be the same.")`, checked after both FK-existence
  checks and before the (also FK-existence-only) `AircraftId` check.
- **No seed data** — same reasoning as `SoftwareDeveloper`/`Aircraft`: only makes sense once real Airports
  and a real Aircraft exist to reference.
- **Delete guard**: `DeleteScheduleTemplate` blocks deletion (409) while any `Schedule` still references it
  — the same `Restrict` + pre-check pattern as every other guarded delete, retrofitted once `Schedule` (see
  below) shipped and closing the exact gap this section used to flag as deferred.
- **Authorization**: same as every other domain entity — `.RequireAuthorization()` with no policy name on
  `/schedule-templates/*`, `AuthorizeFolder("/ScheduleTemplates")` with no policy argument on the Admin.App
  side, nav link in the same shared authenticated-user `@if` block in `_Layout.cshtml`.
- **Admin.App pages** (`Pages/ScheduleTemplates/Index|Create|Edit|Delete.cshtml(.cs)`): the first form with
  **three** FK dropdowns in one Create/Edit page (`DepartureAirportOptions`/`ArrivalAirportOptions`, each
  its own separately-materialized `List<SelectListItem>` from `GET /airports` even though built from the
  same response — sharing one list instance across two independent `<select>` tag helpers risks one
  dropdown's selection state bleeding into the other; `AircraftOptions` from `GET /aircrafts`) plus a fourth,
  `FrequencyOptions`, built locally from `Enum.GetValues<ScheduleFrequency>()`/a small friendly-label map
  (`"Every 2 Days"`, not the raw member name) with no API round-trip needed. `InputModel.FlightTime` is
  typed `TimeOnly` (not `TimeSpan`) specifically so `<input asp-for="Input.FlightTime" type="time">` binds
  natively via ASP.NET Core's built-in `TimeOnly` support — the PageModel converts
  `Input.FlightTime.ToTimeSpan()` on submit and `TimeOnly.FromTimeSpan(response.FlightTime)` on `Edit`'s
  `OnGetAsync`. `StartDate` is a plain `DateOnly`-typed `<input type="date">` (no conversion needed, unlike
  `FlightTime`) — `Create`'s `InputModel.StartDate` defaults to today via a property initializer.
  `Delete.cshtml.cs` stays the plain "Delete failed." shape.

## Schedules

A specific, dated flight instance — one row per `ScheduleTemplate` per date it actually flies.
`Data/Schedule.cs`: `Guid` PK, required `ScheduleTemplateId` (FK to `ScheduleTemplate`, `Restrict`, no
inverse collection navigation), required `FlightDate` (`DateOnly`). A composite unique index on
`(ScheduleTemplateId, FlightDate)` backs this defensively, though `ScheduleGenerator`'s watermark logic
should make it practically unreachable — see below. **Entirely system-generated**: there is no
Create/Update/Delete endpoint or Admin.App form for `Schedule` at all, only a read-only API
(`Features/Schedules/ScheduleEndpoints.cs`, `GET /schedules`/`GET /schedules/{id}`,
`.RequireAuthorization("AccountOrAdmin")` — see "Accounts" below for what this policy is and why it's
needed here specifically) and a browse-only `Pages/Schedules/Index.cshtml(.cs)` in Admin.App
(`AuthorizeFolder("/Schedules")` no policy, nav link in the same shared authenticated-user `@if` block).

- **`GET /schedules` supports optional search/filter query parameters** — `departureIcao`, `arrivalIcao`,
  `minDistanceNauticalMiles`, `maxDistanceNauticalMiles`, `minFlightTimeMinutes`, `maxFlightTimeMinutes` —
  the first `GET` endpoint in the codebase with query-string filtering. Bound directly as nullable minimal-API
  method parameters (no wrapper DTO); each supplied filter conditionally chains another `.Where(...)` onto the
  `IQueryable<Schedule>` before materializing, rather than fetching everything and filtering client-side —
  `Schedule` rows accumulate weekly via `ScheduleGenerator`, so an unbounded full-table fetch is the wrong
  default as the table grows. ICAO parameters are `.ToUpperInvariant()`-normalized before comparison, same as
  every other ICAO-code comparison in this codebase. Flight time is expressed in **minutes** (not hours or a
  `TimeSpan` string) as the simplest unambiguous query-string representation, converted to `TimeSpan`
  server-side for the comparison against `ScheduleTemplate.FlightTime`. Added for `User.App`'s flight search
  screen (see "Flight Assignments" below) but usable by any caller.

- **First `IHostedService`/`BackgroundService` in the solution** — `Features/Schedules/
  ScheduleGenerationBackgroundService.cs`, registered via `builder.Services.AddHostedService<...>()` in
  `API.WebApi/Program.cs` (the only project with DB access — `Admin.App` never talks to Postgres directly,
  per "Project structure"). It's a thin timer wrapper: `do { ... } while (await
  periodicTimer.WaitForNextTickAsync(stoppingToken))` with a 7-day `PeriodicTimer` — the `do/while` shape
  matters, since it runs the body immediately on startup (not waiting the full 7-day period for a first
  tick) before settling into a weekly cadence.
- **Generation logic lives separately from the timer, for testability**: `Features/Schedules/
  ScheduleGenerator.cs` is a static class. `GenerateDueSchedulesAsync(IDbContextFactory<ApplicationDbContext>,
  TimeProvider, CancellationToken)` does the DB work; `ComputeFlightDates(templateStartDate, intervalDays,
  windowStart, windowEnd)` is a pure, directly unit-testable date calculator the former calls per template
  per week. Both are callable straight from tests without waiting on a real timer.
- **First use of `TimeProvider`** (the .NET 8+ testable "now" abstraction) instead of raw
  `DateTime.UtcNow`. `Program.cs` registers `builder.Services.AddSingleton(TimeProvider.System);` so
  `ScheduleGenerationBackgroundService` takes it as a normal constructor dependency. Tests substitute a
  hand-rolled `FakeTimeProvider : TimeProvider` (override `GetUtcNow()` only, a few lines) rather than
  pulling in the `Microsoft.Extensions.TimeProvider.Testing` package for one test class.
- **The generation algorithm — per-`ScheduleTemplate` watermark, not global**:
  ```
  for each ScheduleTemplate:
      lastGeneratedDate = MAX(Schedule.FlightDate) for this template, or (today - 1) if none exist yet
      while (lastGeneratedDate < today + 6):        // less than a week of buffer remains
          windowStart = lastGeneratedDate + 1
          windowEnd   = lastGeneratedDate + 7
          for each date in [windowStart, windowEnd] matching the template's StartDate/Frequency pattern:
              create Schedule row
          lastGeneratedDate = windowEnd
  ```
  **The watermark MUST be computed per template, not as one global `MAX(Schedule.FlightDate)` across every
  template** — an earlier version of this code used a single global watermark, which silently starves any
  newly-created `ScheduleTemplate` of generation forever once an older template's watermark has run weeks or
  months ahead (the global `while` condition would already be satisfied before the new template's own dates
  are ever considered). This bug was caught by `ScheduleGeneratorTests`' integration tests failing when run
  together in the same `ApiFactory`-backed test class (each test's own template was starved by an earlier
  test's already-advanced global watermark) — the same sharing-one-database behavior would have manifested
  in production the moment a second `ScheduleTemplate` was added after the first had been running a while.
  Keep the watermark scoped per template if this logic is ever touched again.
  - **Idempotent by construction**: since each template's watermark only ever advances in whole-week jumps,
    a date is never processed twice for that template across calls — this is what makes the defensive unique
    index above practically unreachable in normal operation.
  - **Catches up automatically**: if the service was offline for 3 weeks, a template's stale watermark makes
    the `while` loop iterate 3 times in one call (still one week of work per iteration) before the buffer is
    topped back up. Steady-state (buffer already topped up) needs one real week to pass before the condition
    is true again, which combined with the weekly timer naturally produces "generate one week, once a week."
  - `IntervalDays(Frequency)` is `ScheduleFrequencyExtensions.ToIntervalDays()` in `Shared.SDK` (`Daily`=1,
    ..., `Weekly`=7) — lives there rather than private to the generator since it's a property of the enum's
    meaning, reusable anywhere `ScheduleFrequency` is used.
- **Response flattening**: `ScheduleResponse` flattens `FlightNumber`, `DepartureAirportIcaoCode`/`Name`,
  `ArrivalAirportIcaoCode`/`Name`, `AircraftId`/`RegistrationNumber`, `DistanceNauticalMiles`, `FlightTime`,
  and the four `Aircraft` passenger/cargo capacity fields
  (`PassengerCapacityEconomy`/`Business`/`First`/`CargoCapacityKg`) from the related `ScheduleTemplate`/
  `Aircraft` (via `.Include(s => s.ScheduleTemplate).ThenInclude(...)` chains) — same flattening pattern as
  `AircraftResponse`/`ScheduleTemplateResponse`. The capacity/distance/time fields were added alongside the
  search filters above, purely so `User.App` has everything it needs to generate a random passenger/cargo
  assignment locally once a flight is selected, without a second API round-trip (see "Flight Assignments"
  below). The list endpoint materializes via `.ToListAsync()` first, then maps to `ScheduleResponse`
  in-memory, since the flattening helper isn't SQL-translatable inside an EF `.Select()` (same reason
  `AircraftEndpoints.cs`'s list endpoint does this).
- **No seed data** — same reasoning as `SoftwareDeveloper`/`Aircraft`/`ScheduleTemplate`: entirely
  system-generated from whatever `ScheduleTemplate`s exist.
- **Test-data safety**: `ScheduleGeneratorTests`/`ScheduleEndpointsTests` share a `ScheduleTestData` helper
  class (`Features/Schedules/ScheduleTestData.cs`) that builds a full Airport→Aircraft→ScheduleTemplate
  chain, taking explicit ICAO/ISO-alpha-2 codes per call (not derived from the test qualifier string) to
  avoid accidental collisions across test methods sharing one `ApiFactory` instance.

## Accounts

End-user (player) identities for `User.App` — deliberately separate from `ApplicationUser` (Admin/staff
Identity accounts): a player has no roles, no admin-role gating, and self-service registration with no
approval step, which don't fit Identity's model the way Admin's accounts do. `Data/Account.cs`: `Guid` PK,
required unique `Email` (max 256), required `PasswordHash`, required `DisplayName` (max 50, **not**
required to be unique — a flight-sim callsign-style name, not a login identifier), `CreatedAtUtc`. Lives in
the same single `ApplicationDbContext` as every other entity (no separate DbContext for domain entities).
No cross-check against `ApplicationUser.Email` — Accounts and Admin users are fully independent by design.

- **Dual JWT audience/scheme separation is the key design decision here.** Both `ApplicationUser` and
  `Account` tokens are signed with the same key/issuer (`JwtTokenService`, generalized below), which means
  without further scoping, an Account-issued token would satisfy every existing `.RequireAuthorization()`
  endpoint with no policy (Manufacturers/Airports/Aircraft/ScheduleTemplates CRUD, etc.) — those only check
  "is this a valid token," not "is this specifically an ApplicationUser token." Fixed by giving Account
  tokens a distinct audience (`JwtOptions.AccountAudience`, `"PilotsRUs.User"` vs. Admin's
  `"PilotsRUs.Admin"`) and a second registered JWT bearer scheme, `"AccountBearer"`
  (`AuthServiceCollectionExtensions.AddApplicationJwtAuth`). Every existing endpoint still uses the
  **default** scheme (`"Bearer"`, Admin-audience-only) via plain `.RequireAuthorization()`, so it
  automatically rejects Account tokens — the audience mismatch fails ASP.NET Core's default JWT bearer
  validation closed, with **zero changes needed to any already-shipped endpoint**. Only
  `GET /account/me` opts in via a new `"Account"` policy
  (`.AddPolicy("Account", p => p.AddAuthenticationSchemes("AccountBearer").RequireAuthenticatedUser())`).
  `AccountEndpointsTests.AccountToken_CannotAccessAdminScopedEndpoint` is the regression guard proving this
  actually works (calls `GET /manufacturers` with an Account token, asserts it's rejected) — if this test
  ever starts failing, the audience scoping has broken.
  - A third policy, **`"AccountOrAdmin"`**, exists for the opposite case — an endpoint both `Admin.App`
    (default-scheme, Admin-audience tokens) and `User.App` (`"AccountBearer"`-scheme, Account-audience
    tokens) both need to call. `.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme,
    "AccountBearer").RequireAuthenticatedUser()` — ASP.NET Core authorizes against a multi-scheme policy if
    *any* listed scheme validates the presented token, so this is purely additive: it doesn't loosen what
    either scheme accepts on its own. `GET /schedules`/`GET /schedules/{id}` is the first (and so far only)
    endpoint using it (see "Schedules" above); `ScheduleEndpointsTests.AccountToken_CanAccessSchedules` is
    the positive-direction complement to `AccountToken_CannotAccessAdminScopedEndpoint`. Reuse this policy
    for any future endpoint both apps need to share, rather than inventing a fourth.
- **`IJwtTokenService.CreateToken` is generalized** away from `ApplicationUser`-specific parameters to
  `(Guid subjectId, string email, string audience, IEnumerable<Claim> additionalClaims, IEnumerable<string>
  roles)`, since both Admin and Account logins need identical signing mechanics (key/issuer/expiry) and only
  differ in audience + claim set. `AuthEndpoints.cs`'s two call sites pass `jwtOptions.Audience` +
  `[GivenName, Surname]`; `AccountEndpoints.cs` passes `jwtOptions.AccountAudience` + a single `Name` claim
  (`DisplayName`) and an empty roles list.
- **`Argon2Hasher` was extracted from `Argon2PasswordHasher`** (`Features/Auth/Argon2Hasher.cs`) — the
  PHC-string hash/verify logic is now `IArgon2Hasher`, Identity-agnostic, returning a new
  `Argon2VerificationResult { Failed, Success, SuccessRehashNeeded }` enum.
  `Argon2PasswordHasher : IPasswordHasher<ApplicationUser>` is now a thin adapter over it. This is a
  refactor of existing code, not a duplication — it's the only way `Account` registration/login can reuse
  the exact same Argon2id parameters/PHC-string format without going through `UserManager<ApplicationUser>`
  (which `Account` doesn't have, since it isn't an Identity user type).
- **`AccountRefreshToken`/`AccountRefreshTokenService` are a deliberate duplication of `RefreshToken`/
  `RefreshTokenService`, not a generalization of them** — same rotation/reuse-detection shape, same
  `IOptions<RefreshTokenOptions>` expiry config, but operating on `Account` instead of `ApplicationUser`.
  `RefreshTokenService`'s behavior is already carefully documented and relied upon for Admin auth; a
  polymorphic version supporting two different principal types was judged riskier than a second,
  independent ~120-line implementation. Unlike `RefreshToken.UserId` (also `Cascade`),
  `AccountRefreshToken.AccountId` uses `Cascade` too — refresh tokens are disposable session state, not
  reference data, for both principal types.
- **Registration does not auto-login** — `POST /account/register` returns just `AccountResponse` (no
  tokens); the client (`User.App`) navigates to the Login screen next, not straight into the app.
- **Password validation**: `[MinLength(8)]`-equivalent manual check in `AccountEndpoints.cs`'s register
  handler, enforced unconditionally (not dev-only relaxed like `ApplicationUser`'s policy) — this is the
  first genuinely public, always-on self-service registration endpoint in the system, unlike the
  seeded-only Admin accounts.
- **DTOs**: `Shared.SDK/Accounts/` (`RegisterAccountRequest`, `AccountResponse`, `AccountLoginRequest`,
  `AccountLoginResponse`, `CurrentAccountResponse`) — independent of `Shared.SDK/Auth/`'s Admin-specific
  DTOs (`LoginResponse` carries an Admin-only `Roles` field), except reusing the existing, genuinely generic
  `Shared.SDK.Auth.RefreshTokenRequest` (`string RefreshToken`) for `/account/refresh`/`/account/logout`
  request bodies rather than adding a single-field duplicate.
- **Known follow-ups, not built yet**: no password reset/email confirmation flow (no email-sending
  infrastructure anywhere in this codebase); no brute-force/lockout protection on `/account/login` (Identity
  gives `ApplicationUser` this via `SignInManager.CheckPasswordSignInAsync(..., lockoutOnFailure: true)`;
  `Account` has no equivalent since it doesn't go through Identity at all).

## User.App architecture

`User.App` had no real architecture before this — just the unmodified Avalonia template. This section
covers what was stood up to support Account registration/login/logout, and is the reference pattern for
every future `User.App` feature.

- **DI host**: `Program.cs`'s `Main` builds a `Host.CreateApplicationBuilder(args)` (registering
  `IHttpClientFactory`, `IDbContextFactory<UserAppDbContext>`, `IAuthSessionService`, `IAccountApiClient`,
  `IScheduleApiClient`, `IFlightAssignmentService`, `BearerTokenHandler`, and every ViewModel) **before**
  calling
  `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`. Avalonia constructs `App` itself with no
  constructor-injection hook, so the built `IServiceProvider` is stashed on a static `App.Services`
  property that `OnFrameworkInitializationCompleted` reads to resolve `MainWindowViewModel`. `appsettings.json`
  (`Api:BaseAddress`, default `https://localhost:7182`) is loaded automatically by `Host.CreateApplicationBuilder`
  the same way `WebApplication.CreateBuilder` does — no extra configuration wiring needed, just
  `CopyToOutputDirectory` in the `.csproj`.
- **Local SQLite via EF Core**: `Data/UserAppDbContext.cs`, registered via
  `AddDbContextFactory<UserAppDbContext>` (matching `API.WebApi`'s `IDbContextFactory<T>` convention for
  non-Identity contexts, even though there's no Identity concern here at all), DB file at
  `%LOCALAPPDATA%/PilotsRUs/pilotsrus.db` (`UserAppDbContext.GetDefaultDbPath()`), migrated unconditionally
  at startup (`dbContext.Database.Migrate()` — no `IsDevelopment()` gate, a desktop app has no such
  distinction). `Data/DesignTimeDbContextFactory.cs` mirrors `API.WebApi`'s, needed for
  `dotnet ef migrations add --project PilotsRUs.User.App` tooling support.
  - **Started with zero entities** (`InitialCreate` migration is near-empty) — nothing auth-related is
    persisted locally, the access/refresh token pair lives only in `IAuthSessionService` (in-memory
    singleton), and User.App always shows the login screen on launch (no "remember me"/session restore).
    `FlightAssignment` (`AddFlightAssignments` migration) is the first real entity — see "Flight
    Assignments" below.
- **`BearerTokenHandler`** (`Infrastructure/BearerTokenHandler.cs`) mirrors `Admin.App`'s handler of the
  same name, but simpler: no `HttpContext`/cookie involved, just reads/writes `IAuthSessionService`
  directly, and uses a single `static readonly SemaphoreSlim` for the 401→refresh→retry serialization
  (not `Admin.App`'s `ConcurrentDictionary<string, SemaphoreSlim>` keyed per browser session) since exactly
  one session ever exists in a desktop process. Attached to the named `"Api"` `HttpClient`; the refresh call
  itself uses a plain unnamed client (with `BaseAddress` set manually from `IOptions<ApiOptions>`) to avoid
  recursing back into itself.
- **MVVM navigation — no router/navigation library.** `MainWindowViewModel` is DI-resolved (gets the real
  `IAccountApiClient`/`IAuthSessionService` from the container) and holds an observable `CurrentViewModel`
  property; it manually constructs each leaf screen ViewModel (`LoginViewModel`/`RegisterViewModel`/
  `ShellViewModel`) via small private factory methods, passing plain `Action` callbacks for "navigate to X"
  (e.g. `LoginViewModel`'s `onLoginSucceeded` sets `CurrentViewModel = CreateShellViewModel()`).
  `MainWindow.axaml`'s content is `<ContentControl Content="{Binding CurrentViewModel}"/>`, resolved to the
  matching View by the pre-existing `ViewLocator` (registered globally in `App.axaml`'s
  `Application.DataTemplates`, string-replaces `ViewModel`→`View` in the type's full name — already worked
  for any future `XViewModel`/`XView` pair with zero changes). This is deliberately not a generic navigation
  service, since navigation only happens in this one place so far.
- **Testing ViewModels without HTTP mocking**: `LoginViewModel`/`RegisterViewModel`/`ShellViewModel`/
  `FlightSearchViewModel`/`MyFlightsViewModel` all depend on interfaces (`IAccountApiClient`/
  `IScheduleApiClient`/`IFlightAssignmentService`), not `HttpClient`/`DbContext` directly, so
  `PilotsRUs.User.App.Tests/ViewModels/*Tests.cs` use hand-rolled fakes (`Services/FakeAccountApiClient.cs`,
  `FakeScheduleApiClient.cs`, `FakeFlightAssignmentService.cs`) instead of mocking `HttpMessageHandler`.
  `IAuthSessionService` isn't faked at all — its real `AuthSessionService` implementation is trivial
  in-memory state, so tests just instantiate it directly and assert against its real behavior.
  `FlightAssignmentServiceTests` (the one test class that legitimately needs to exercise a real `DbContext`,
  since it's testing `FlightAssignmentService` itself, not something that depends on it) uses EF Core's
  `Microsoft.EntityFrameworkCore.InMemory` provider via a `PooledDbContextFactory<UserAppDbContext>` — the
  same InMemory-for-tests approach `ApiFactory` already uses on the API side.

## Flight Assignments

`User.App`'s first real feature past the Shell screen: search generated `Schedule` rows, pick one, and get
randomly assigned passenger/cargo numbers for it, stored locally. `Data/FlightAssignment.cs` is
`UserAppDbContext`'s first real entity (see "User.App architecture" above) — deliberately denormalized, not
FK'd to anything, since the local SQLite database and the API's Postgres database are entirely separate;
`ScheduleId` is kept only for reference/traceability, and every field the "My Flights" list needs to render
is copied in directly so reading it back never needs an API round-trip.

- **Random generation is a separately-testable pure function**, mirroring `ScheduleGenerator`'s pure-logic/
  wrapping-I/O-service split on the API side: `Services/FlightAssignmentGenerator.cs` is a static class,
  `Generate(economyCapacity, businessCapacity, firstCapacity, cargoCapacityKg, Random? random = null)`.
  Each of Economy/Business/First/Cargo is independently randomized to **40-100% of that value's capacity** —
  a deliberate "realistic load factor" choice over a flat 0-100% range, which could produce an empty flight.
  A capacity of 0 always yields 0. The optional `Random` parameter lets tests pass a seeded instance for
  deterministic bounds-checking. `Services/IFlightAssignmentService.cs`/`FlightAssignmentService.cs` wraps
  it with the actual SQLite write (`AssignAsync`, called when a search result is selected) and the read-back
  `GetAllAsync` (newest-first) that "My Flights" uses.
- **`ApiResult<T>` was generalized from `IAccountApiClient`'s private `AccountApiResult<T>`**
  (`Services/ApiResult.cs`) once `IScheduleApiClient` needed the exact same success/value-or-failure/message
  shape. This is a pure data-shape wrapper with zero business logic in it, so — unlike
  `AccountRefreshTokenService`'s *deliberate* duplication of `RefreshTokenService` (see "Accounts" above,
  judged too risky to make polymorphic) — sharing this one carries none of that risk. `IAccountApiClient`'s
  behavior is unchanged, it just returns the renamed type now.
- **`Services/IScheduleApiClient.cs`/`ScheduleApiClient.cs`** wraps `GET /schedules` with the new search
  query parameters (see "Schedules" above), building the query string by hand
  (`Uri.EscapeDataString`) rather than pulling in `Microsoft.AspNetCore.WebUtilities.QueryHelpers` — that
  package belongs to the ASP.NET Core-web SDK, which this plain desktop-app project doesn't reference, and
  isn't worth adding for one helper. `FlightSearchCriteria` is the request shape, every field nullable since
  every filter is optional.
- **Three new screens, extending `MainWindowViewModel`'s existing no-router navigation pattern**:
  `FlightSearchViewModel`/`View` (filters + results, a "Select" button per row calling
  `IFlightAssignmentService.AssignAsync` then navigating away), `FlightAssignmentResultViewModel`/`View` (a
  **separate confirmation screen**, not an inline panel on the search results — a deliberate choice over the
  alternative), `MyFlightsViewModel`/`View` (read-only list of previously-made assignments).
  `ShellViewModel` gained two more buttons/callbacks ("Search Flights"/"My Flights") alongside the existing
  Logout, wired the same way `onLoggedOut` already was. `MainWindowViewModel`'s constructor gained
  `IScheduleApiClient`/`IFlightAssignmentService` so its private factory methods can construct the three new
  ViewModels.
  - **`MyFlightsViewModel`'s data load is triggered by its factory method**, not a page-navigation lifecycle
    event (none exists in this app's navigation model): `CreateMyFlightsViewModel()` fires the generated
    `LoadCommand` immediately after construction (`_ = viewModel.LoadCommand.ExecuteAsync(null);`). Follow
    this same pattern for any future screen that needs to load data as soon as it becomes current.
  - Search filter inputs (`FlightSearchView.axaml`) are plain `TextBox`es bound to `string` properties,
    parsed to `int?` inside `SearchAsync` (invalid/empty input → no filter for that field) — not Avalonia's
    `NumericUpDown`, whose `Value` is `decimal?` and would need a converter to bind against `int?` for no
    real benefit here.
  - A per-row "Select" button inside `ItemsControl.ItemTemplate` invoking a command on the *parent*
    ViewModel (not the templated item) is the first instance of this pattern in `User.App` — with
    `AvaloniaUseCompiledBindingsByDefault` on, it needs
    `Command="{Binding $parent[ItemsControl].((vm:FlightSearchViewModel)DataContext).SelectFlightCommand}"
    CommandParameter="{Binding}"` (see `FlightSearchView.axaml`) rather than a plain `{Binding
    SelectFlightCommand}`, which would resolve against the templated `ScheduleResponse` item's own
    (nonexistent) `DataContext` instead. Reuse this exact binding shape for any future per-row action button.
- **Known follow-ups, not built yet**: `FlightAssignment` rows aren't scoped per-`Account` — since there's
  no local Account caching at all (see "Started with zero entities" above), two different players sharing
  one machine/local database would see each other's rows in "My Flights"; revisit if local Account caching
  is ever added. No delete/cancel action on a `FlightAssignment` from "My Flights" yet — read-only for now.

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
  `Data/ApplicationUser.cs` (adds required `FirstName`/`LastName`), `Data/ApplicationRole.cs`,
  `Data/RefreshToken.cs`, `Data/Manufacturer.cs`, `Data/AircraftModel.cs`, `Data/Country.cs`,
  `Data/Airport.cs`, `Data/SoftwareDeveloper.cs`, `Data/Aircraft.cs`, `Data/ScheduleTemplate.cs`,
  `Data/Schedule.cs`, `Data/Account.cs`, `Data/AccountRefreshToken.cs`. Identity's own tables (`AspNetUsers`,
  `AspNetRoles`, etc.) plus `RefreshTokens`, `Manufacturers`, `AircraftModels` (the first FK relationship
  between two non-Identity entities — see "Aircraft Models" above), `Countries`, `Airports` (FK to
  `Countries`), `SoftwareDevelopers`, `Aircraft` (FK to both `AircraftModels` and `SoftwareDevelopers` — see
  "Aircraft" above), `ScheduleTemplates` (FK to `Airports` twice plus `Aircraft` once — see "Schedule
  Templates" above), `Schedules` (FK to `ScheduleTemplates` — see "Schedules" above), `Accounts`, and
  `AccountRefreshTokens` (FK to `Accounts` — see "Accounts" above).
- Migrations live in `Data/Migrations/` (`InitialCreate`, `AddRefreshTokens`, `AddUserNames`,
  `AddManufacturers`, `AddAircraftModels`, `AddCountries`, `AddAirports`,
  `AddSoftwareDevelopersAndAircraft`, `AddScheduleTemplates`, `AddScheduleTemplateStartDateAndSchedules`,
  `AddAccounts`).
  `AddSoftwareDevelopersAndAircraft` is a single combined migration rather than two separate ones — both
  entities were added to `ApplicationDbContext` in the same edit before either `migrations add` call, so a
  first `AddSoftwareDevelopers` migration would have captured both tables' diff anyway, leaving a follow-up
  `AddAircraft` migration empty; add entities across separate edits+`migrations add` calls if a genuinely
  separate migration per entity is wanted (as `AddScheduleTemplates` did, being the only entity added in its
  edit). `AddScheduleTemplateStartDateAndSchedules` similarly bundles two changes made in one edit
  (`ScheduleTemplate.StartDate` + the new `Schedule` entity) into one migration — adding a required column
  to an already-shipped table is safe here since the dev `ScheduleTemplates` table is expected to be empty
  at migration-apply time (Postgres would otherwise reject a `NOT NULL` column add with no default against
  a non-empty table; EF generated a `DefaultValueSql`-free `AddColumn` with an implicit default here, so it
  would still succeed even against existing rows, just backfilling them with `0001-01-01`). `dotnet ef
  migrations add <Name> --project PilotsRUs.API.WebApi --output-dir Data/Migrations` (needs
  `Data/DesignTimeDbContextFactory.cs` since the context is registered via
  `AddNpgsqlDbContext`/`AddDbContextFactory` rather than the classic `AddDbContext<T>(options => ...)`
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
