# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

PilotsRUs gives a complete economic structure to MSFS 2024 (Microsoft Flight Simulator). It is split into
three parts:

- **API** — a RESTful API, the base for everything else
- **Admin interface** — an ASP.NET Razor Pages app
- **User interface** — an Avalonia MVVM desktop app

## Current status

The basic solution scaffold is in place: each project below exists with template-default content and a
matching `<ProjectName>.Tests` project, wired together and orchestrated through Aspire. Business logic,
AdminLTE styling, JWT auth, and the Postgres/EF data layer are not implemented yet. When adding new
projects, register them in `PilotsRUs.slnx` (the XML-based solution format — add `<Project Path="..." />`
entries, not a `.sln` file).

## Project structure

- `PilotsRUs.API.WebApi` — the API project; references `Shared.SDK` and `ServiceDefaults`
- `PilotsRUs.Shared.SDK` — class library with all DTOs; connects to the API via `IHttpClientFactory`
- `PilotsRUs.Admin.App` — ASP.NET Razor Pages admin interface, styled with AdminLTE 4, JWT-based login;
  references `Shared.SDK` and `ServiceDefaults`
- `PilotsRUs.User.App` — Avalonia MVVM desktop app for end users; references `Shared.SDK`. Not orchestrated
  by Aspire since it's a standalone installed app, not a hosted service
- `PilotsRUs.AppHost` — the Aspire app host; orchestrates `API.WebApi` and `Admin.App` for local
  development (resource names `api` and `admin`)
- `PilotsRUs.ServiceDefaults` — shared Aspire wiring (OpenTelemetry, health checks, service discovery,
  resilience) referenced by every hosted service project; each service calls `builder.AddServiceDefaults()`
  and `app.MapDefaultEndpoints()` in `Program.cs`

Every project above has a matching `<ProjectName>.Tests` xUnit project, except `ServiceDefaults` (pure
wiring, nothing to unit test). `AppHost.Tests` is an Aspire integration test project that boots the whole
distributed application via `DistributedApplicationTestingBuilder` and asserts against real resources —
run it to verify orchestration wiring after touching `AppHost.cs` or either hosted service's `Program.cs`.

## Architecture principles

- Modular monolith with vertical slice architecture, following SOLID principles
- Builder pattern, Options pattern, and repository pattern where applicable
- .NET Aspire as the place all logging occurs
- Prefer loading database/other packages if already available in the environment, rather than assuming
- Health checks wherever possible
- Unit, integration, and end-to-end tests wherever possible; test project folder structure should mirror
  the structure of the project under test

## Database

PostgreSQL via Entity Framework, accessed through `IDbContextFactory`.

## Deployment

- API and Admin interface run from Docker
- User interface is a standalone app with an installer (not containerized)

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
dotnet run --project PilotsRUs.AppHost                                   # run API + Admin locally via Aspire
```
