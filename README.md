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

# 3. Apply the schema — see the note below about the environment variable
dotnet ef database update --project StudyHub.Infrastructure --startup-project StudyHub.API

# 4. Run
dotnet run --project StudyHub.API

# 5. Run the tests (no Docker needed)
dotnet test
```

### Step 3 needs an environment variable

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
| `STUDYHUB_DB_CONNECTION is not set` from `dotnet ef` | Environment variable missing in this terminal (step 3) |
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

M1–M5 complete: architecture, domain, schema, error handling, and the content tree with cascade soft-delete.
**M5.1 (cleanup) is in progress; M6 (authentication) is next.**

Full roadmap: [`docs/Requirements.md`](docs/Requirements.md) §11.
