# StudyHub

A backend API for managing courses, study notes, and tasks, with AI-suggested tasks extracted from raw notes and approved by the user (planned).

Built as a **personal learning project** — the goal is to practise professional .NET backend engineering (Clean Architecture, CQRS, rich domain models, real schema constraints) rather than to ship the fastest possible MVP.

**C# / .NET 10 · PostgreSQL 15 · EF Core 10 · MediatR · FluentValidation · xUnit**

---

## ⚠ Not production-ready

Authentication and authorization are in place: every endpoint requires a JWT access token unless it is explicitly anonymous. What is still missing before public exposure is rate limiting, integration tests, and refresh-token cleanup (M10), and registration still reveals whether an email is in use (Requirements §9.4).

---

## Quick start

```bash
# 1. Start PostgreSQL
docker compose up -d

# 2. Store the connection string for the running app (one time only)
cd StudyHub.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=StudyHubDb;Username=postgres;Password=YourSecurePassword"
cd ..

# 3. Store the JWT signing key (one time only) — see the note below
#    The app refuses to start without a key of at least 32 bytes.

# 3b. Optional: seed the first administrator — see the note below

# 4. Apply the schema — see the note below about the environment variable
dotnet ef database update --project StudyHub.Infrastructure --startup-project StudyHub.API

# 5. Run
dotnet run --project StudyHub.API

# 6. Run the tests (no Docker needed)
dotnet test
```

### Step 3 — generating the key

The key is never written to `appsettings.json`. Generate 48 random bytes and store them in user secrets; this form works in both Windows PowerShell 5.1 and PowerShell 7:

```powershell
cd StudyHub.API
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
dotnet user-secrets set "Jwt:Key" ([Convert]::ToBase64String($bytes))
dotnet user-secrets list
```

`Jwt:Issuer`, `Jwt:Audience` and both lifetimes live in `appsettings.json`; only the key is a secret. A key shorter than 32 bytes fails validation **at startup**, not at first login.

### Step 3b — the first administrator

At startup, `AdminSeed:Email` names the administrator account. If it does not exist yet, it is created from `AdminSeed:FullName` and `AdminSeed:Password` (same password policy as registration) and promoted. If it already exists, it is only promoted — its password is never changed.

```powershell
cd StudyHub.API
dotnet user-secrets set "AdminSeed:Email" "admin@example.com"
dotnet user-secrets set "AdminSeed:FullName" "Administrator"
dotnet user-secrets set "AdminSeed:Password" "<a strong password>"
```

After the first start logs `Administrator ... created from configuration`, remove the password — it is no longer needed:

```powershell
dotnet user-secrets remove "AdminSeed:Password"
```

Without `AdminSeed:Email`, nothing is seeded and startup does not touch the database.

### Step 4 needs an environment variable

`dotnet ef` commands do **not** read user secrets. They go through `IDesignTimeDbContextFactory`, which reads `STUDYHUB_DB_CONNECTION`. Set it in the same terminal session:

```powershell
# PowerShell
$env:STUDYHUB_DB_CONNECTION = "Host=localhost;Port=5432;Database=StudyHubDb;Username=postgres;Password=YourSecurePassword"
```

```cmd
:: cmd.exe — quotes wrap the whole assignment, not just the value
set "STUDYHUB_DB_CONNECTION=Host=localhost;Port=5432;Database=StudyHubDb;Username=postgres;Password=YourSecurePassword"
```

These are two separate configuration paths. Migration commands succeeding proves nothing about whether the running app is configured, and vice versa.

---

## Trying the API

Open `StudyHub.API/StudyHub.API.http` in Visual Studio or VS Code with the REST Client extension.

1. Send `S0` once to register the session user (and `S2` for a second user, used by the isolation checks).
2. Send `S1` (and `S3`) at the start of every session to log in.
3. The remaining requests read the access token from those responses. The `ADM` requests also need a seeded administrator (step 3b).

### Inspecting the database

```bash
docker exec -it studyhub_postgres psql -U postgres -d StudyHubDb
```

This is the only view that does **not** pass through EF Core's soft-delete filter, so it is the way to confirm that a soft-deleted row still exists. Paste one statement per line.

---

## Project layout

```
StudyHub.Domain             Business entities & rules. Zero external dependencies.
StudyHub.Application        Use cases (Command + Handler). Depends only on Domain.
StudyHub.Infrastructure     EF Core, PostgreSQL, security implementations.
StudyHub.API                ASP.NET Core host. Thin — no business logic.
StudyHub.Domain.Tests       Entity rules.
StudyHub.Application.Tests  Handlers, dependencies mocked.
StudyHub.Infrastructure.Tests  Infrastructure code that needs no database.
```

Dependencies point inward only: `API → Infrastructure → Application → Domain`.

---

## Endpoints implemented

| Method | Route | Access | Result |
|---|---|---|---|
| POST | `/api/auth/register` | anonymous | 201 + userId |
| POST | `/api/auth/login` | anonymous | 200 + access and refresh tokens |
| POST | `/api/auth/refresh` | anonymous | 200 + a new token pair; the old refresh token is revoked |
| POST | `/api/auth/logout` | user | 204 — revokes the presented refresh token |
| GET | `/api/auth/me` | user | 200 + the caller's profile and AI quota |
| POST | `/api/courses` | user | 201 + courseId |
| GET | `/api/courses?page=1&pageSize=20` | user | 200 + a page of the caller's courses |
| GET | `/api/courses/{id}/tree` | user | 200 + every note and task of the course, as a flat list |
| PUT | `/api/courses/{id}` | user | 204 — replaces title and description |
| DELETE | `/api/courses/{id}` | user | 204 — soft-deletes the course and its whole tree; not reversible through the API |
| POST | `/api/notes` | user | 201 + noteId |
| POST | `/api/tasks` | user | 201 + taskId |
| PATCH | `/api/tasks/{id}/status` | user | 204 — sets the status |
| PATCH | `/api/tasks/{id}/schedule` | user | 204 — replaces priority and due date (UTC only) |
| GET | `/api/items?page=1&pageSize=20` | user | 200 + a page of standalone notes and tasks (no parent, no course) |
| GET | `/api/items/{id}` | user | 200 + one note or task |
| GET | `/api/items/{id}/tree` | user | 200 + the item and all its descendants, as a flat list |
| PATCH | `/api/items/{id}` | user | 204 — replaces title and content |
| DELETE | `/api/items/{id}` | user | 204 — soft-deletes the item and its whole subtree; not reversible through the API |
| PATCH | `/api/admin/users/{id}/deactivate` | administrator | 204 — the account can no longer log in or refresh |

"user" means any valid access token (`Authorization: Bearer ...`); without one the response is 401. Another user's course or item returns 403, for reads and writes alike. Lists take `page` (from 1) and `pageSize` (20 by default, at most 100); a value out of range is 400. A null `description`, `content` or `dueDate` in an update body clears the value.

---

## Common problems

| Symptom | Cause |
|---|---|
| `SocketException (10061)` on port 5432 | The Postgres container isn't running — `docker compose up -d` |
| `Database connection string is not configured` at startup | User secrets not set (step 2) |
| `Jwt:Key must be at least 32 bytes` at startup | The signing key is missing or too short (step 3) |
| `STUDYHUB_DB_CONNECTION is not set` from `dotnet ef` | Environment variable missing in this terminal (step 4) |
| `No account exists for AdminSeed:Email, so AdminSeed:FullName and AdminSeed:Password are required` at startup | The seed names a new account but the password was already removed — set both again, or remove `AdminSeed:Email` (step 3b) |
| `ValidationException ... Password` at startup | `AdminSeed:Password` fails the registration password policy (step 3b) |
| MediatR license warning at startup | Expected and harmless — the project is inside the free tier |

Every problem hit during development, with its root cause, is recorded in [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md).

---

## Documentation

| Document | Answers |
|---|---|
| [`docs/Requirements.md`](docs/Requirements.md) | **What** the system does and **why** — use cases, business rules, the tree design, schema, architectural decisions, roadmap |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | **How** it is built — technology choices, request workflows, the `Items` table explained |
| [`docs/CODING_STANDARDS.md`](docs/CODING_STANDARDS.md) | Rules for writing code in this repository |
| [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md) | Every problem hit, its fix, and its root cause |
| [`docs/TROUBLESHOOTING_GUIDE.md`](docs/TROUBLESHOOTING_GUIDE.md) | How to add an entry to the log above |
| [`docs/database/databaseERD`](docs/database/databaseERD) | Entity-relationship diagram (Mermaid) |

**This file describes how to run the project. It deliberately does not repeat the requirements** — duplicated documents always drift, and this one used to be a verbatim copy of the PRD.

---

## Progress

M1–M6 complete: architecture, domain, schema, error handling, the content tree with cascade soft-delete, the M5.1 cleanup, the M5.2 role foundation, and M6 authentication & authorization.

M6 delivered login with JWT access tokens and hashed refresh tokens; refresh rotation with an `xmin` concurrency token and reuse detection (replaying a rotated token revokes every session the user has); logout; endpoints protected by default; permission policies driven by the Domain's role map; the first administrator endpoint; and seeding the first administrator from configuration. The `X-User-Id` bypass is gone.

M7 (in progress, awaiting review) adds DTOs, read queries for courses, items, trees and the current user, update endpoints for courses, items and tasks, and pagination.

**Next: M8** — AI task suggestions with user approval, the quota split, and the quota moved to configuration.

Full roadmap: [`docs/Requirements.md`](docs/Requirements.md) §11.


