# StudyHub

A backend API for managing courses, study notes, and tasks, with AI summaries of a note and AI-suggested tasks extracted from it — suggestions the user approves before anything is created.

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

# 3c. Optional: the AI provider key — see the note below.
#     Without it the application runs with the fake provider and both AI endpoints work.

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

### Step 3c — the AI provider

**The application runs without an AI key.** When `Ai:ApiKey` is absent it registers a fake provider that answers deterministically from the note itself, so `POST /api/notes/{id}/summarize` and `POST /api/notes/{id}/extract-tasks` work with no key, no network and no cost. The choice is made once at startup, never as a fallback after a failed call, and the startup log names the implementation in use:

```
info: StudyHub.API[0]
      AI provider in use: FakeAiService.
```

To call the real provider, store the key and the model name — the key is a secret, and the model is never guessed, so a key without a model stops startup:

```powershell
cd StudyHub.API
dotnet user-secrets set "Ai:ApiKey" "<your provider key>"
dotnet user-secrets set "Ai:Model" "<the model name to call>"
```

Everything else is non-secret and lives in `appsettings.json`:

| Key | Meaning |
|---|---|
| `Ai:BaseUrl` | The provider's API root. A trailing `/` is added if it is missing |
| `Ai:Model` | The model to call. Required as soon as `Ai:ApiKey` is set |
| `Ai:TimeoutSeconds` | Explicit request timeout; the `HttpClient` default of 100 seconds is not a decision |
| `Ai:MaxOutputTokens` | Cap on what the provider may generate. **On a reasoning model this budget is spent on the model's thinking before the answer starts**, so it is 2048 rather than a size chosen for three sentences |
| `Ai:CharsPerToken`, `Ai:ResponseReserveTokens` | The pre-call quota estimate: `characters / CharsPerToken + ResponseReserveTokens` |
| `Ai:MaxSuggestions` | Most suggestions one extraction may return |
| `Ai:ThinkingBudget` | Optional. Tokens the model may spend thinking, passed straight to the provider. `null` sends nothing; `0` tells a model that supports it not to think at all |
| `Ai:ThinkingLevel` | Optional. The provider's own name for how hard to think, passed straight through. Empty sends nothing |
| `Ai:FakeFailure` | `true` makes the fake provider fail every call, so the 502 path is testable |
| `UserQuota:DefaultMonthlyTokens` | The monthly token quota a new account starts with |

A missing or non-positive value in that section stops startup with a message naming the key. `Ai:ThinkingBudget` and `Ai:ThinkingLevel` are the two exceptions: both are optional, and `0` is a meaningful budget rather than a missing one.

**Reasoning models.** Neither thinking value is guessed, because a model that does not know the field refuses the whole request — leave both unset unless your model documents them. With both unset the request carries no thinking configuration at all, and the code still copes with a model that thinks anyway: the thinking comes back as extra response parts, which are skipped rather than returned as the answer. If a call answers 502 and the log says the provider stopped at the output limit, the model spent the whole budget thinking — raise `Ai:MaxOutputTokens`, or cap the thinking with the two keys above. Remember that `Ai:ResponseReserveTokens` feeds the pre-call quota estimate, so a model that thinks a lot wants a larger reserve too.

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
| POST | `/api/notes/{id}/summarize` | user | 200 + `{ summary, tokensUsed }` — empty body; the summary is returned, never stored |
| POST | `/api/notes/{id}/extract-tasks` | user | 200 + `{ suggestions, tokensUsed }` — empty body; nothing is created until the user approves a suggestion with `POST /api/tasks` |
| PATCH | `/api/admin/users/{id}/deactivate` | administrator | 204 — the account can no longer log in or refresh |

"user" means any valid access token (`Authorization: Bearer ...`); without one the response is 401. Another user's course or item returns 403, for reads and writes alike. Lists take `page` (from 1) and `pageSize` (20 by default, at most 100); a value out of range is 400. A null `description`, `content` or `dueDate` in an update body clears the value.

**The two AI endpoints are separate calls and separate charges**, and a request never does both. Each one checks the caller's remaining monthly quota against an estimate before calling the provider — 429 when it does not fit — and records what the call cost afterwards, visible in `GET /api/auth/me` as `tokensUsedThisMonth`. A provider that fails or answers with something unusable is 502, never 500. Extraction alone returns 409 when the note is already at the maximum nesting depth, because an approved task could not be created under it.

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
| `Ai:ApiKey is set, so Ai:Model must name the model to call` at startup | A provider key was stored without a model name (step 3c) |
| `Ai:CharsPerToken must be configured with a positive number` at startup | A value under `Ai:` or `UserQuota:` is missing or not positive — check `appsettings.json` |
| 502 from an AI endpoint while no key is configured | `Ai:FakeFailure` is `true`; set it back to `false` |
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

M8 (in progress) adds the two AI endpoints behind one `IAiService`, with a fake provider chosen at startup when no key is configured, the pre-call quota estimate, the atomic usage record, and the default quota moved to configuration. It is marked done once the owner has made one real provider call.

**Next: M9** — the dashboard aggregation.

Full roadmap: [`docs/Requirements.md`](docs/Requirements.md) §11.


