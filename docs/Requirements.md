# StudyHub — Engineering Requirements (PRD) v2.0

> **Scope of this document**: *what* the system does and *why* each decision was made.
> For *how it is built*, see `docs/ARCHITECTURE.md`. For *code style*, see `docs/CODING_STANDARDS.md`. For *problems hit and fixed*, see `docs/TROUBLESHOOTING.md`.
>
> **v2.0 supersedes v1.1 entirely.** The data model changed fundamentally in M5: `Notes` and `Tasks` are no longer separate tables. Any statement in an older document that contradicts this one is stale.

---

## 1. Project Vision

StudyHub is a backend API for managing courses, study notes, and tasks, with AI-assisted extraction of actionable tasks from raw notes.

This is a **personal learning project**. The explicit goal is to practice professional .NET backend engineering — Clean Architecture, CQRS, domain modelling, schema design, and a disciplined verification habit — not to ship the fastest possible MVP. Where a decision has a cost, that cost is written down rather than hidden.

---

## 2. Core Use Cases

### Identity & Access Management
- **UC-01**: As a user, I can register and log in securely so that my data is protected.
- **UC-02**: As a user, my session stays active through refresh tokens without frequent manual logins, and a stolen token can be revoked.

### Content Management (Core Domain)
- **UC-03**: As a user, I can create, edit, and archive (soft-delete) courses. A course is always a top-level container and is never nested inside anything.
- **UC-04**: As a user, I can create notes and tasks that either stand alone, sit under a course, or nest under another note or task — in any combination, up to five levels deep. Tasks additionally carry a status (Pending, InProgress, Completed), a priority, and an optional due date.
- **UC-05**: As a system, archiving any node archives its entire subtree across every level, using soft delete so nothing is physically removed from the database.

> **UC-05 reverses v1.1 deliberately.** The previous wording specified *"Soft-Delete Cascade prevention"* — archiving a course was supposed to leave its tasks untouched. The current design does the opposite: archiving cascades down the whole tree. This is an intentional change of direction, not a correction of a bug. Rationale in ADR-08 and ADR-09.

### AI Integration & Cost Control
- **UC-06**: As a user, I can submit a note to an AI service to extract structured, actionable tasks. Extracted tasks are created as children of the source note, so the provenance is visible in the tree itself.
- **UC-07**: As a system, I enforce a **monthly** AI token quota per user to prevent abuse and control external API cost.

> **UC-07 clarifies v1.1.** The old text said "daily/monthly" while the code has only ever implemented a monthly quota (`MonthlyTokenQuota`, `TokensUsedThisMonth`, `LastTokenResetDate`). Monthly is the decision.

### Data Aggregation
- **UC-08**: As a frontend application, I can fetch a full user dashboard (statistics, active courses, urgent tasks) from a single optimized endpoint, with no N+1 queries.

---

## 3. Domain Model & Business Rules

This section is the authoritative description of the content tree. It is the part most likely to be misremembered later.

### 3.1 The three content types

| Type | Can be a parent? | Can be a child? | Storage |
|---|---|---|---|
| `Course` | Yes | **Never** | Own table, `Courses` |
| `Note` | Yes | Yes | `Items` table, `Kind = 0` |
| `TaskItem` | Yes | Yes | `Items` table, `Kind = 1` |

`Note` and `TaskItem` are separate C# classes sharing an abstract `Item` base. They live in one database table via EF Core's Table-Per-Hierarchy mapping (ADR-08).

### 3.2 Tree rules

1. **A course is always a root.** The `Courses` table has no parent column, so this is structurally impossible to violate — no constraint needed.
2. **Free nesting between items.** A task may be a child of a note, a note a child of a task. The parent's type is irrelevant because both are `Item`.
3. **Maximum depth is five levels** — `Depth` runs 0 to 4. Enforced in the entity (`Item.MaxDepth`) *and* by `CK_Item_Depth`.
4. **Depth is stored, not computed.** Because node moving is not supported (rule 7), an item's depth is fixed at creation and never changes. `CK_Item_RootDepth` enforces that `ParentItemId IS NULL` if and only if `Depth = 0`.
5. **A child inherits its parent's `CourseId`.** Passing a different course alongside a parent is rejected by the validator; the entity ignores it regardless. This is deliberate duplication (ADR-09).
6. **Ownership unity.** A child's `UserId` always equals its parent's. Enforced inside `Item.Initialize` — so a handler that forgets to check ownership still cannot produce a cross-owner link.
7. **Node moving is not supported.** An item's parent is fixed at creation. This is a deliberate deferral, and three consequences follow from it:
   - Cycles are impossible. A newly created child has no descendants, so it cannot enclose its own ancestor. No cycle detection code exists, and none is needed while this rule holds.
   - Depth is permanent, which is what makes rule 4 safe.
   - `CourseId` never changes, which is what makes rule 5 safe.

   **If node moving is ever added, all three collapse at once.** It would require cycle detection, depth recalculation for the entire moved subtree (`parent depth + subtree height ≤ max`), and a `CourseId` rewrite down every descendant.
8. **Deleting a node soft-deletes its whole subtree**, across every level, in a single transaction.
9. **Deleting a course soft-deletes the course and every item carrying its `CourseId`** — which, by rule 5, is the entire tree at every depth. No recursive traversal is needed.

### 3.3 Entity behaviour

Entities are rich: private setters, private constructors, static factory methods that enforce invariants at creation.

| Entity | Behaviour |
|---|---|
| `User` | `Create`, `ConsumeTokens`, `ResetQuotaIfNeeded`, `Deactivate`, `ChangePassword` |
| `Course` | `Create`, `UpdateDetails`, `MarkAsDeleted` |
| `Item` (abstract) | `Initialize` (protected), `UpdateContent`, `MarkAsDeleted` |
| `Note : Item` | `Create` |
| `TaskItem : Item` | `Create`, `UpdateStatus`, `UpdateSchedule` |
| `RefreshToken` | `Create`, `IsActive(utcNow)`, `Revoke(utcNow, replacedBy)` |
| `AiUsageLog` | `Create` (write-once; no mutators by design) |

**Base classes**: `BaseEntity` holds `Id` + `CreatedAt`. `AuditableEntity : BaseEntity` adds `UpdatedAt`. `AiUsageLog` inherits the former — a usage record is never modified, so an audit field on it would be a permanently null column.

**Value object**: `Email` owns normalization (`Trim().ToLowerInvariant()`) and format validation. It exists because that logic was previously duplicated between `User.Create` and `UserRepository`, and a divergence there means a duplicate account slipping past a unique index.

**Time as a parameter**: methods whose behaviour depends on the clock (`ResetQuotaIfNeeded`, `RefreshToken.IsActive`, `RefreshToken.Revoke`) take the current time as an argument, so they are testable without a clock abstraction. Everything else reads `DateTime.UtcNow` directly — a deliberate limit on how far that pattern is worth pushing.

---

## 4. Technology Stack by Layer

### Domain — `StudyHub.Domain`
C# / .NET 10 with **zero external dependencies**. No EF Core, no ASP.NET Core. This is persistence ignorance: the business rules can be unit-tested with nothing running.

### Application — `StudyHub.Application`
- **MediatR 14** — CQRS. One use case = one Command/Query + one Handler.
- **FluentValidation 12** — rules in dedicated `Validator` classes, run automatically by `ValidationBehavior` before any handler executes. A handler may assume its input is already valid.
- Repository and service **interfaces are declared here**, not in Infrastructure — Dependency Inversion: the Application declares what it needs, the Infrastructure provides it.
- Depends only on Domain.

### Infrastructure — `StudyHub.Infrastructure`
- **EF Core 10** (Code-First) with **Npgsql**.
- Fluent API only (`IEntityTypeConfiguration<T>`); no Data Annotations on domain entities.
- **BCrypt.Net-Next** for password hashing.
- Implements every interface declared in Application.

### API — `StudyHub.API`
- ASP.NET Core **Controllers** (ADR-14). Every action is 3–5 lines: build a command, send it, map the result.
- `IExceptionHandler` + `ProblemDetails` for centralized error translation (ADR-15).
- **OpenAPI** via `AddOpenApi()`.
- JWT Bearer authentication (M6).

### Database
PostgreSQL 15 in Docker Compose. Schema versioned through EF Core Migrations. Referential integrity enforced at the database level, not only in code.

### Testing
xUnit + Moq + FluentAssertions, across `StudyHub.Domain.Tests` and `StudyHub.Application.Tests`.

---

## 5. Architectural Decisions (ADRs)

| # | Decision | Rationale | Cost accepted |
|---|---|---|---|
| 01 | Clean Architecture, 4 layers | Separation of concerns; business logic testable in milliseconds | More projects and indirection than a 3-layer app needs |
| 02 | MediatR (CQRS) | One file per use case; cross-cutting behaviour added once in the pipeline | An extra indirection between endpoint and logic |
| 03 | FluentValidation | Rules declarative, independently testable, run before every handler | A second place to look when tracing a rejection |
| 04 | EF Core Code-First | Entities are the source of truth; schema history versioned in git | Migration discipline required; the snapshot file is easy to get wrong |
| 05 | PostgreSQL 15 | Strong relational guarantees; identical in Docker and in most clouds | — |
| 06 | JWT + refresh token rotation | Stateless auth with revocable sessions | Refresh tokens must be stored, hashed, and rotated |
| 07 | BCrypt with `Enhanced*` variants | Configurable work factor; pre-hashing removes BCrypt's silent 72-byte truncation | **Irreversible**: standard and enhanced hashes are not interchangeable once stored |
| 08 | **Notes and tasks in one `Items` table (TPH)** | Distinct types that can nest inside each other is a contradiction the relational model cannot express cheaply. Three alternatives were costed — a polymorphic parent (no foreign key at all), an exclusive arc (six parent columns), a fully dissolved model — and each demanded a real sacrifice. Unifying the two types removes the contradiction instead of paying for it | `Status`, `Priority`, `DueDate` are nullable at the database level; conditional check constraints replace `NOT NULL` |
| 09 | **Child inherits `CourseId`** | Deliberate duplication. Makes "delete a course and its whole tree" a single non-recursive query at any depth | Safe **only** while node moving is forbidden. If moving is added, `CourseId` must be rewritten down every descendant |
| 10 | **`Depth` stored as a column** | Enables `CK_Item_Depth` and `CK_Item_RootDepth` at the database level, and removes the need for a depth-calculation service | Same dependency on rule 3.2.7 as ADR-09 |
| 11 | Level-by-level subtree fetch, not `WITH RECURSIVE` | "One query per level" only matters when levels are unbounded; depth is capped at five. Staying in LINQ keeps the soft-delete filter applied automatically and avoids a hand-maintained SQL string | At most five round trips instead of two |
| 12 | Soft delete + EF global query filters | Nothing is ever physically lost | Every table grows and never shrinks; raw SQL bypasses the filter entirely; correct restore is not possible (see §12) |
| 13 | Repository + Unit of Work over EF Core | Handler tests need no database; the pattern is worth learning | **Technically redundant** — `DbContext` is already a unit of work and `DbSet<T>` already a repository. An extra abstraction layer, and `IQueryable` composition is lost at the boundary |
| 14 | Controllers, not Minimal APIs | Attribute routing, filters, and `[Authorize]` are conventional and well documented | Slightly more ceremony per endpoint |
| 15 | `IExceptionHandler`, not custom middleware | The modern ASP.NET Core replacement; returns RFC-shaped `ProblemDetails` | `CODING_STANDARDS.md` §4 still says "middleware" and needs updating |
| 16 | Duplicate token accounting: `User.TokensUsedThisMonth` **and** `AiUsageLogs` | The counter answers "is there quota left" in one read; the log answers "what was it spent on" | They must be updated together, inside one method on `User`, or they drift |
| 17 | Manual DTO mapping via static extension methods | No extra dependency; explicit | Boilerplate per DTO; swappable for AutoMapper/Mapster later without touching Domain |

---

## 6. Application Layer Structure (Vertical Slices)

Each feature owns a folder containing everything it needs. The slice follows the **use case**, not the database table — `CreateNoteCommand` lives under `Notes/` even though it writes to `Items` through `IItemRepository`.

```
StudyHub.Application/
├── Common/
│   ├── Interfaces/   → ICourseRepository, IItemRepository, IUserRepository,
│   │                   IUnitOfWork, IPasswordHasher, ICurrentUserService
│   ├── Behaviors/    → ValidationBehavior.cs
│   └── Exceptions/   → ConflictException, NotFoundException, ForbiddenException
│
├── Courses/Commands/{CreateCourse, DeleteCourse}/
├── Notes/Commands/CreateNote/
├── Tasks/Commands/CreateTask/
├── Items/Commands/DeleteItem/
└── Users/Commands/RegisterUser/
```

**Request flow, end to end:**
1. A controller action builds a Command and calls `mediator.Send(...)`.
2. `ValidationBehavior` runs the matching validator. Invalid input throws before the handler exists.
3. The handler resolves ownership, calls a domain factory or mutator, and saves through `IUnitOfWork`.
4. Any exception is translated to a status code by `GlobalExceptionHandler`.

**Defence in depth is deliberate.** Ownership of a parent item is checked twice — once in the handler (to return a precise 403) and once inside `Item.Initialize` (because the entity trusts no caller). Course ownership is checked in the handler only, since the entity receives a `Guid` rather than an object; that asymmetry is known and accepted.

---

## 7. Database Schema (v2.0 — implemented and verified)

Five tables: `Users`, `Courses`, `Items`, `RefreshTokens`, `AiUsageLogs`.

> The v1.1 schema described `Notes` and `Tasks` as separate tables with `Tasks.CourseId` and `Tasks.SourceNoteId`. **None of those exist.** `SourceNoteId` in particular is now expressed as ordinary parenthood: an AI-extracted task is simply a child of its source note.

### Users
| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| FullName | varchar(150) | required |
| Email | varchar(150) | required, **unique index**; stored as text via a value converter |
| PasswordHash | text | required |
| MonthlyTokenQuota | int | |
| TokensUsedThisMonth | int | |
| LastTokenResetDate | timestamptz | |
| IsActive | bool | |
| CreatedAt / UpdatedAt | timestamptz | UpdatedAt nullable |

### Courses
| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| UserId | uuid | FK → Users (**Restrict**), indexed |
| Title | varchar(200) | required |
| Description | text | nullable |
| IsDeleted | bool | soft delete |
| CreatedAt / UpdatedAt | timestamptz | |

Check constraint: `CK_Course_Title` — `"Title" ~ '\S'` (at least one non-whitespace character).

### Items
| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| UserId | uuid | FK → Users (Restrict) |
| CourseId | uuid? | FK → Courses (Restrict), indexed |
| ParentItemId | uuid? | **FK → Items (Restrict)** — self-referencing, indexed |
| Depth | int | 0–4 |
| Title | varchar(250) | required |
| Content | text? | deliberately unbounded — it is the body |
| IsDeleted | bool | soft delete |
| Kind | int | TPH discriminator: 0 = Note, 1 = Task |
| Status | int? | tasks only |
| Priority | int? | tasks only |
| DueDate | timestamptz? | tasks only |
| CreatedAt / UpdatedAt | timestamptz | |

Check constraints:

| Name | Expression | Purpose |
|---|---|---|
| `CK_Item_Depth` | `"Depth" BETWEEN 0 AND 4` | maximum nesting |
| `CK_Item_RootDepth` | `("ParentItemId" IS NULL) = ("Depth" = 0)` | catches both directions: a root with depth, and a child without |
| `CK_Item_TaskFields` | `("Kind" = 1) = ("Status" IS NOT NULL AND "Priority" IS NOT NULL)` | task fields present only on tasks |
| `CK_Item_StatusValue` | `"Status" IS NULL OR "Status" BETWEEN 0 AND 2` | enum range — presence is not validity |
| `CK_Item_PriorityValue` | `"Priority" IS NULL OR "Priority" BETWEEN 0 AND 2` | enum range |
| `CK_Item_Title` | `"Title" ~ '\S'` | matches the domain's `IsNullOrWhiteSpace` rule |

Indexes: `(CourseId)`, `(ParentItemId)`, `(UserId, CourseId)`, and `(UserId, DueDate)` **filtered on `Kind = 1`** so a task query never scans note rows.

### RefreshTokens
| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| UserId | uuid | FK → Users (**Cascade**), indexed |
| TokenHash | varchar(64) | **unique index**; SHA-256 hex, never the raw token |
| ExpiresAt | timestamptz | |
| RevokedAt | timestamptz? | |
| ReplacedByTokenId | uuid? | rotation chain |
| CreatedAt / UpdatedAt | timestamptz | |

The hash must be **deterministic** (SHA-256), not BCrypt: every refresh looks the token up by hash, and a per-row salt would make that lookup impossible. SHA-256 is safe here because the token is high-entropy random, unlike a password.

### AiUsageLogs
| Column | Type | Notes |
|---|---|---|
| Id | uuid | PK |
| UserId | uuid | FK → Users (Restrict) |
| OperationType | varchar(50) | required |
| TokensConsumed | int | |
| CreatedAt | timestamptz | **no `UpdatedAt`** — write-once |

Index: `(UserId, CreatedAt)` composite. No standalone `UserId` index — a composite already covers its leading column.

### Delete behaviours
`Restrict` everywhere except `RefreshTokens.UserId`, which cascades. Rationale: a session belongs to nobody but its user, while content must never disappear because of an accidental parent delete.

---

## 8. API Surface

### Implemented
| Method | Route | Auth | Returns |
|---|---|---|---|
| POST | `/api/auth/register` | anonymous | 201 + userId |
| POST | `/api/courses` | user | 201 + courseId |
| DELETE | `/api/courses/{id}` | user | 204 |
| POST | `/api/notes` | user | 201 + noteId |
| POST | `/api/tasks` | user | 201 + taskId |
| DELETE | `/api/items/{id}` | user | 204 |

Deletion has one route for both notes and tasks, because the operation does not distinguish them.

### Planned — M6
`POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`

### Planned — M7
`GET /api/courses`, `GET /api/courses/{id}/tree`, `GET /api/items/{id}`, `PATCH /api/items/{id}`, `PATCH /api/tasks/{id}/status`, `PUT /api/courses/{id}`

### Error contract
All errors return RFC 9457 `ProblemDetails`:

| Exception | Status |
|---|---|
| `ValidationException` | 400 + `errors` grouped by field |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| anything else | 500, with no internal detail leaked |

**Two different sources produce 400.** Model binding inside `[ApiController]` rejects malformed JSON *before* MediatR runs; its response carries a `traceId`. A `ValidationException` from a validator does not. Useful when diagnosing.

---

## 9. Security Model

### ⚠ Current state — authentication is not implemented

Identity comes from an `X-User-Id` request header read by a temporary `CurrentUserService`. **This is a complete authentication bypass**: anyone can name any user and become them. It exists solely so the content handlers could be built and tested before M6.

**This code must not be deployed or exposed on any network until M6 is complete.**

The design limits the blast radius of replacing it: the Application layer depends on `ICurrentUserService`, not on HTTP. When JWT arrives, only the API-layer implementation changes — not one line in Application.

### Target state (M6)
- Short-lived access token (JWT, HS256), signing key stored in **user secrets**, never in `appsettings.json`. HS256 requires a key of at least 32 bytes.
- Long-lived refresh token: random, stored **hashed**, rotated on every use, with the old token revoked and linked via `ReplacedByTokenId`.
- **Reuse detection**: presenting an already-revoked token indicates theft; the correct response is to revoke the user's entire token chain.
- Login must not distinguish "no such user" from "wrong password" — same status, same message, and **the same response time** (call `Verify` against a dummy hash when no user is found, or timing alone leaks which emails are registered).
- `IsActive` checked before any token is issued.

### Known accepted risks
- **`ConflictException` on registration names the email.** A friendlier message in exchange for account enumeration. Accepted for a learning project; revisit before any public deployment.
- **Course ownership is guarded in one layer only** (see §6).
- **Password length is unbounded.** Safe because `EnhancedHashPassword` pre-hashes the input, removing BCrypt's 72-byte truncation. Reverting to the standard variant would silently reintroduce it.

---

## 10. Testing Strategy

**Why Domain and Application first**: both are free of any dependency on a database, a web server, or the network. The full suite runs in about two seconds with Docker stopped.

| Project | Scope |
|---|---|
| `StudyHub.Domain.Tests` | Entity factories, invariants, tree rules |
| `StudyHub.Application.Tests` | Each handler in isolation, repositories mocked |

**Naming**: `MethodName_Scenario_ExpectedResult`. **Structure**: Arrange–Act–Assert, separated by blank lines. **Mocking**: only interfaces declared in Application; never the entity under test.

**Definition of done**: every new handler ships with at least one success test and one failure test.

**Not automated yet**: EF Core queries and HTTP round-trips need integration testing against a containerized database. Scheduled for M10. Until then, manual verification through `StudyHub.API.http` and direct `psql` inspection is the substitute — and `psql` is the stronger of the two, because it is the only view that does not pass through EF's soft-delete filter.

### Three verification rules learned the hard way
1. **A successful build proves nothing.** Every change is verified by its own specific effect: an HTTP status code, a row in the database, a passing test.
2. **A constraint is proven by trying to break it *and* by inserting a row that should pass.** One without the other is half an answer. And read *which* constraint the error names — a malformed statement produces a red error and no inserted row, which is exactly what a working constraint produces.
3. **A change with no externally observable behaviour is verified by reading the file.** When no code path calls the changed method yet, the build passes, the tests pass, and the endpoints behave identically whether or not the change was ever applied. Nothing else will catch it.

---

## 11. Roadmap

| # | Scope | Status |
|---|---|---|
| M1 | PRD & requirements | ✅ |
| M2 | Solution & Clean Architecture layers | ✅ |
| M3 | Domain entities, DbContext, relational integrity | ✅ |
| M4 | Application foundation: MediatR, FluentValidation, `ValidationBehavior`, test projects | ✅ |
| **M5** | **Hardening & the content tree**: global exception handling, domain and security fixes, `Email` value object, base-class split, clean schema with six check constraints, the `Items` TPH restructure, create/delete handlers, controllers | ✅ |
| **M6** | **Authentication (UC-01, UC-02)**: login, JWT issuance, refresh rotation, removal of the `X-User-Id` bypass | ⏳ Next |
| M7 | Content completion (UC-03, UC-04, UC-05): read queries, DTOs, update handlers, quota to configuration | Pending |
| M8 | AI integration & quota enforcement (UC-06, UC-07) | Pending |
| M9 | Dashboard aggregation (UC-08) | Pending |
| M10 | Integration tests, rate limiting, API containerization | Pending |

> **Renumbering note.** v1.1 listed M5 as authentication and M6 as content management. What was actually built in that slot was hardening plus the content tree. The roadmap has been rewritten to match what happened rather than what was planned — which also keeps every `(M5)` tag in the troubleshooting log accurate.

---

## 12. Deliberately Deferred

Each of these is a decision, not an oversight.

| Item | Why deferred | What it will cost later |
|---|---|---|
| **Node moving** | No use case requires it; it is the single largest source of complexity in a free-nesting tree | Cycle detection, subtree depth recalculation, `CourseId` rewrite down every descendant. Invalidates ADR-09 and ADR-10 |
| **Unarchive / restore** | Not requested | **Cannot be done correctly as designed.** After a cascade delete, nothing distinguishes a child you deleted last month from one deleted by cascade yesterday. Fixing it means `DeletedAt` (or a delete-batch id) instead of `IsDeleted` — cheap now, expensive once real data exists |
| **Many-to-many between tasks and notes** | Parenthood covers the known cases | A join table and a second relationship model |
| **Integration tests** | Domain and Application carry the business logic | A containerized test database and a slower suite |
| **API containerization** | `dotnet run` is sufficient locally | A Dockerfile and compose changes |
| **Rate limiting** | No public exposure yet | Middleware plus a policy per endpoint group |
| **`Content` length limit** | It is the note body; an arbitrary cap would be guesswork | A migration if a limit is ever chosen |

---

## 13. Open Questions

Most of v1.1's open questions are now closed: API style is **Controllers** (ADR-14); DTO mapping is **manual extension methods** (ADR-17); repository granularity is **one per aggregate** — `IUserRepository`, `ICourseRepository`, `IItemRepository` — with `Item` covering both notes and tasks since they share a table and a lifecycle.

Still open:

1. **Access token lifetime** — 15 minutes is the common default. Shorter means more refresh traffic; longer means a stolen token stays useful longer.
2. **Refresh token lifetime and concurrent session policy** — is one active token per user enough, or should multiple devices each hold their own?
3. **Should the registration conflict message stop naming the email?** (§9)
4. **`DeletedAt` instead of `IsDeleted`** — worth doing pre-emptively, before restore is ever requested? (§12)
5. **AI provider abstraction** — is `IAiService` enough, or should the prompt/response contract be modelled explicitly before M8?
