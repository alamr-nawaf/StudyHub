# StudyHub

A backend API for managing courses, study notes, and tasks, with AI-suggested tasks extracted from raw notes and approved by the user (planned).

Built as a **personal learning project** — the goal is to practise professional .NET backend engineering (Clean Architecture, CQRS, rich domain models, real schema constraints) rather than to ship the fastest possible MVP.

**C# / .NET 10 · PostgreSQL 15 · EF Core 10 · MediatR · FluentValidation · xUnit**

---

## ⚠ Not deployable yet

Authentication is not implemented. Identity currently comes from an `X-User-Id` request header, which is a **complete authentication bypass** — anyone can name any user and become them. It exists so the content features could be built and verified before the auth milestone.

Do not deploy this or expose it on any network until M6 is complete.

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

1. Send request 1 to register a user.
2. Copy the returned `userId` into the `@userId` variable at the top of the file.
3. The remaining requests will work.

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

| Method | Route | Result |
|---|---|---|
| POST | `/api/auth/register` | 201 + userId |
| POST | `/api/courses` | 201 + courseId |
| DELETE | `/api/courses/{id}` | 204 — soft-deletes the course and its whole tree; not reversible through the API |
| POST | `/api/notes` | 201 + noteId |
| POST | `/api/tasks` | 201 + taskId |
| DELETE | `/api/items/{id}` | 204 — soft-deletes the item and its whole subtree; not reversible through the API |

Login and refresh arrive in M6; read queries and update endpoints in M7.

---

## Common problems

| Symptom | Cause |
|---|---|
| `SocketException (10061)` on port 5432 | The Postgres container isn't running — `docker compose up -d` |
| `Database connection string is not configured` at startup | User secrets not set (step 2) |
| `Jwt:Key must be at least 32 bytes` at startup | The signing key is missing or too short (step 3) |
| `STUDYHUB_DB_CONNECTION is not set` from `dotnet ef` | Environment variable missing in this terminal (step 4) |
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

M1–M5.2 complete: architecture, domain, schema, error handling, the content tree with cascade soft-delete, the M5.1 cleanup, and the M5.2 role foundation — a `Role` column with a range check constraint, and a permission map in the Domain.

M5.2 ships the role as data and as rules; nothing reads it yet. The `role` claim, the endpoint policies, the first administrator endpoint, and seeding the first administrator account all belong to M6, because a role only becomes provable once a request carries one.

**M6 (authentication & authorization) is in progress.** Two of its five sessions are closed. Built so far: JWT settings validated at startup, `ITokenService` over `JsonWebTokenHandler`, the refresh-token repository with an `xmin` concurrency token, and `POST /api/auth/login`, which returns an access token and a refresh token and stores only the hash of the latter.

Refresh rotation is live too: each refresh revokes the presented token, links it to its replacement, and issues a new pair. Presenting an already-revoked token is treated as a stolen chain — every token the user holds is revoked before the 401 comes back. Logout has a handler but no endpoint yet, because it needs an identity the API cannot read.

**Nothing is protected yet.** No endpoint requires a token, the API never reads one, and the `X-User-Id` bypass above is still the only identity it has. That is the next session.

Full roadmap: [`docs/Requirements.md`](docs/Requirements.md) §11.


