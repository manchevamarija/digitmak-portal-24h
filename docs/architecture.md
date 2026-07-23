# DigitMak Portal architecture

## Style

The backend is a modular monolith. One deployable API process contains cohesive business modules, while PostgreSQL remains the transactional boundary. Modules share infrastructure deliberately but do not place business rules in `Program.cs`.

## Dependency direction

```text
Modules (HTTP endpoints)
        ↓
Application abstractions and services
        ↓
Domain entities and rules
        ↓
Infrastructure implementations (EF Core, Identity, SMTP, disk storage)
```

`Program.cs` is only the composition root: configuration, middleware, dependency injection and module registration. Endpoint classes are the HTTP/controller layer in this Minimal API design; they validate transport input and delegate reusable workflows to application services.

## Source layout

```text
DigitMak.Portal.Api/
├── Application/
│   ├── Abstractions/       service contracts
│   ├── Contracts/          request/response DTOs
│   └── Services/           use cases and entitlement rules
├── Domain/
│   └── Entities/           persistent domain entities
├── Infrastructure/
│   ├── DependencyInjection.cs
│   ├── DatabaseInitializer.cs
│   └── PortalHealthChecks.cs
├── Modules/
│   ├── Administration/
│   ├── Clients/
│   ├── Files/
│   ├── Identity/
│   ├── Public/
│   └── Staff/
├── Data/Migrations/        PostgreSQL EF Core migrations
├── GlobalUsings.cs
└── Program.cs              composition root only
```

## Repository decision

The project intentionally does not wrap EF Core in a generic `IRepository<T>`. `DbContext` already implements unit-of-work and repository semantics; another generic layer would hide useful EF Core features without adding a business boundary. Reusable workflows such as ticket entitlement, messages, meetings and public contact intake live behind focused application-service interfaces. Read-only administration queries remain in their feature module. A specialised repository should be introduced only when a complex aggregate query needs reuse outside its module.

## Module responsibilities

- Public: localized services and public contact intake with explicit six-value DMA classification.
- Identity: registration, login, refresh rotation, revocation, verification and password reset.
- Clients: organizations, subscriber tickets, chat and meeting requests.
- Staff: triage, assignment, internal notes and meeting decisions.
- Administration: approvals, subscriptions, users, audit and reports.
- Files: validated private upload/download through the storage abstraction.

## Cross-cutting infrastructure

- ASP.NET Identity and JWT bearer authentication, with persisted user lifecycle timestamps and an active-account middleware check on every authenticated request.
- Role policies plus server-side resource authorization.
- EF Core/PostgreSQL persistence and migrations.
- SignalR ticket groups.
- Brevo SMTP queue worker.
- VM disk storage behind `IFileStorage`, with extension/MIME binding, content-signature validation, root-path containment and ClamAV scanning through `IFileScanner`.
- Rate limiting, security headers, audit records and health checks.
- Trusted forwarded-header processing behind Nginx, dynamic production-domain rendering and systemd-managed backup/TLS schedules.

## Frontend structure

```text
frontend/src/
├── app/                    composition and view orchestration
├── components/layout/      shared header and footer
├── content/                localized portal copy and catalogue data
├── features/auth/          auth context, provider and hooks
├── pages/public/           public portal and contact intake
├── pages/client/           subscriber dashboard and resources
├── pages/admin/            administration dashboard
├── shared/                 shared types and API resource hooks
├── api.ts                  centralized authenticated HTTP client
└── styles                  application styling
```

Authentication state, API access and pages are separated. The shared API client serializes concurrent refresh attempts into one refresh-token rotation. Client, staff and admin dashboards are protected routes and load only authorised API resources. Their operational interface is localised in Macedonian, English and Albanian, including responsive workspace navigation.
