# DigitMak Portal

DigitMak Portal is a web platform for managing public service information, contact requests, organizations, subscriptions, help-desk tickets, real-time communication, meetings, and administrative reporting.

## Technologies

### Backend

- .NET 10 Web API
- Entity Framework Core
- PostgreSQL
- ASP.NET Core Identity
- JWT authentication
- SignalR

### Frontend

- React
- TypeScript
- Vite
- React Router
- i18next

## Project Structure

```text
backend/      Backend API and tests
frontend/     React frontend application
deploy/       Deployment configuration and scripts
docs/         Technical documentation
```

## Local Development

### Start the Backend

Run:

```text
START-BACKEND.cmd
```

The backend API will be available at:

```text
http://localhost:5241
```

### Start the Frontend

Run:

```text
START-FRONTEND.cmd
```

The frontend will be available at:

```text
http://localhost:5173
```

To start both applications together, run:

```text
START-FULL-SYSTEM.cmd
```

## Local Demo Accounts

The following accounts are created automatically when the application runs in the Development environment.

### Administrator

- Email: `admin@digitmak.mk`
- Password: `DigitMak!2026Admin`

### Client

- Email: `client@digitmak.mk`
- Password: `DigitMak!2026Client`

The client account includes an approved demo organization and an active subscription.

> These credentials are intended only for local development and demonstration.

Production administrator credentials must be provided through the following environment variables:

- `ADMIN_BOOTSTRAP_EMAIL`
- `ADMIN_BOOTSTRAP_PASSWORD`

## Manual Start

### Backend

```powershell
dotnet run --project backend/src/DigitMak.Portal.Api
```

### Frontend

```powershell
cd frontend
npm install
npm run dev
```

## Docker

Start the complete local environment with:

```powershell
docker compose up --build
```

## Documentation

Technical documentation is available in the `docs` directory.

Additional setup and project handover information is available in `HANDOFF-MK.md`.

## Security

Passwords, API keys, SMTP credentials, and other production secrets must not be committed to the repository.
