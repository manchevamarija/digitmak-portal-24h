# DigitMak Portal

Најновата целосна споредба со V1 PDF спецификацијата, функционалната состојба, познатите надворешни acceptance чекори и упатството за новиот account-change workflow се во [docs/AKTUELNA-SOSTOJBA-I-PDF-USOGLASENOST.md](docs/AKTUELNA-SOSTOJBA-I-PDF-USOGLASENOST.md).

Production-oriented implementation of the DigitMak technical specification. The solution is a modular-monolith ASP.NET Core API with a React/TypeScript frontend, PostgreSQL persistence, SignalR chat, background notifications and role-aware client, staff and administrator workspaces.

## Start on Windows

1. Double-click `START-BACKEND.cmd`.
2. Double-click `START-FRONTEND.cmd`.
3. Open `http://localhost:5173`.

The normal Windows start now stores local data persistently in `data/digitmak-dev.db`, so tickets remain available after an API restart. For a PostgreSQL-based environment, start Docker Desktop and run `START-FULL-SYSTEM.cmd`; the complete stack is then available at `http://localhost:3000`. `STOP-FULL-SYSTEM.cmd` stops the containers without deleting the PostgreSQL volume.

The scripts use `npm.cmd`/`npx.cmd`, so they also work when PowerShell blocks `npm.ps1`. Do not run `cd frontend` when the terminal already ends in `\frontend>`.

Development administrator (local environment only):

- E-mail: `admin@digitmak.mk`
- Password: `DigitMak!2026Admin`

Development client with an approved demo organization and an active 12-month subscription:

- E-mail: `client@digitmak.mk`
- Password: `DigitMak!2026Client`

The demo client is seeded only in Development (or when `DemoAccount:Enabled=true` is explicitly configured). It is never created by the Production configuration.

Change these values through protected environment variables before any real deployment.

## Other start options

- Frontend: `cd frontend`, then `npx.cmd -y pnpm@11 install` and `npm.cmd run dev`
- API: `dotnet run --project backend/src/DigitMak.Portal.Api`
- Full local stack: `docker compose up --build`

## Architecture

- `frontend/src/app` — routing and application composition
- `frontend/src/pages` — public, authentication, client, staff and admin screens
- `frontend/src/features` — authentication and reusable feature state
- `frontend/src/components` — layout and shared visual components
- `frontend/src/shared` — API client, types and common utilities
- `backend/src/DigitMak.Portal.Api/Domain` — entities and domain state
- `backend/src/DigitMak.Portal.Api/Application` — contracts and application services
- `backend/src/DigitMak.Portal.Api/Infrastructure` — database, storage, e-mail and initialization
- `backend/src/DigitMak.Portal.Api/Modules` — Identity, Public, Clients, Staff, Administration and Files endpoints

The detailed organisation and repository decision are documented in `docs/architecture.md`. The verified delivery scope is recorded in `docs/V16-PRODUCTION-READINESS-2026-07-22.md`, with the design sign-off checklist in `docs/BRANDING-APPROVAL.md` and the external-provider boundary in `docs/EXTERNAL-INTEGRATIONS.md`. The source specification is included at `docs/reference/digitmak-portal-v1-technical-spec.pdf`.

Code style is reproducible: run `dotnet tool restore`, then `dotnet csharpier check backend/src backend/tests` for handwritten C# sources, and run `npm.cmd run format:check` inside `frontend` for TypeScript, TSX and CSS.

## Verification

The current release was verified with:

- 37 passing backend integration/unit tests
- 4 passing frontend tests
- clean frontend lint
- successful frontend production build
- successful backend Release build with zero warnings/errors
- successful Docker production API image build
- isolated PostgreSQL, ClamAV and API startup
- successful Windows PostgreSQL/upload backup and restore rehearsal (27 restored application tables)
- 8 passing automated acceptance checks covering login, ticket, attachment, SignalR and notification queue

Development uses persistent SQLite when `ConnectionStrings__Portal` is absent. Docker and production use PostgreSQL. Production secrets, DNS, VM access, provider credentials and final legal/content approvals are external inputs and are intentionally not stored in the repository.

## Production preparation

1. Run `powershell -ExecutionPolicy Bypass -File deploy/setup-production.ps1`.
2. Supply the external owner credentials requested by the wizard.
3. Run `powershell -ExecutionPolicy Bypass -File deploy/validate-production.ps1`.
4. Follow `docs/operations.md` for deployment, health checks, acceptance and backup rehearsal.

## One-command automation

- Double-click `AUTOMATSKA-PROVERKA.cmd` for a safe local release verification.
- Run `powershell -ExecutionPolicy Bypass -File deploy/automate-production.ps1 -Mode Prepare` to generate/preserve and validate production configuration.
- Run `powershell -ExecutionPolicy Bypass -File deploy/automate-production.ps1 -Mode Deploy` only on the intended server after real credentials and DNS are configured. It builds and starts the stack, waits for readiness, runs acceptance checks and rehearses backup/restore.

Every run writes `outputs/production-automation-report.json`. The automation never invents provider credentials and never considers legal or branding approval complete without the responsible owner.
