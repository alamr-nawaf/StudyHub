# StudyHub — Engineering Requirements (PRD) v3.0

> **Scope of this document**: *what* the system does and *why* each decision was made.
> For *how it is built*, see `docs/ARCHITECTURE.md`. For *code style*, see `docs/CODING_STANDARDS.md`. For *problems hit and fixed*, see `docs/TROUBLESHOOTING.md`.
>
> **v3.0 supersedes v2.0.** No behaviour already built has changed. What changed is that fourteen decisions previously left open — or never written down at all — are now decided. Sections 1–13 keep their v2.0 numbers so that every cross-reference in `ARCHITECTURE.md` and `TROUBLESHOOTING.md` stays valid; the two new sections are appended as §14 and §15.
>
> **What is new in v3.0**
> - §14 — Cross-Cutting Rules: time, pagination, logging. Rules that belong to no single milestone, which is exactly why they were missing.
> - §15 — AI Integration Model: the quota ordering problem, the depth conflict, and the failure model.
> - §9 — the six authentication decisions that M6 cannot start without.
> - §2 — `UC-03` no longer promises archiving, because restore is not possible as designed.
> - §5 — ADR-18 through ADR-25.

---

## 1. Project Vision

StudyHub is a backend API for managing courses, study notes, and tasks, with AI-assisted extraction of actionable tasks from raw notes.

This is a **personal learning project**. The explicit goal is to practise professional .NET backend engineering — Clean Architecture, CQRS, domain modelling, schema design, and a disciplined verification habit — not to ship the fastest possible MVP. Where a decision has a cost, that cost is written down rather than hidden.

---

## 2. Core Use Cases

### Identity & Access Management
- **UC-01**: As a user, I can register and log in securely so that my data is protected.
- **UC-02**: As a user, my session stays active through refresh tokens without frequent manual logins, and a stolen token can be revoked.

### Content Management (Core Domain)
- **UC-03**: As a user, I can create, edit, and delete courses. Deletion is a soft delete — the row stays in the database — but it is not reversible through the API. A course is always a top-level container and is never nested inside anything.
- **UC-04**: As a user, I can create notes and tasks that either stand alone, sit under a course, or nest under another note or task — in any combination, up to five levels deep. Tasks additionally carry a status (Pending, InProgress, Completed), a priority, and an optional due date.
- **UC-05**: As a system, deleting any node deletes its entire subtree across every level, using soft delete so nothing is physically removed from the database.

> **UC-03 no longer says "archive" (v3.0).** v2.0 used the word *archive*, which promises the user that what was archived can be seen and restored. §12 states that correct restore is impossible with the current `IsDeleted` design. A document that promises what the design cannot deliver is worse than one that admits the gap. The word is now *delete*. The `DeletedAt` + `DeletedBatchId` change that would make restore possible is scheduled in M7 — see §12 and ADR-25.

> **UC-05 reverses v1.1 deliberately.** The v1.1 wording specified *"Soft-Delete Cascade prevention"* — deleting a course was supposed to leave its tasks untouched. The current design does the opposite: deletion cascades down the whole tree. This is an intentional change of direction, not a correction of a bug. Rationale in ADR-08 and ADR-09.

### AI Integration & Cost Control
- **UC-06**: As a user, I can submit a note to an AI service to extract structured, actionable tasks. Extracted tasks are created as children of the source note, so the provenance is visible in the tree itself. A note already at maximum depth cannot be used as an extraction source — see §15.2.
- **UC-07**: As a system, I enforce a **monthly** AI token quota per user to prevent abuse and control external API cost. The quota is checked *before* the external call, never only after it — see §15.1.

> **UC-07 clarifies v1.1.** The old text said "daily/monthly" while the code has only ever implemented a monthly quota (`MonthlyTokenQuota`, `TokensUsedThisMonth`, `LastTokenResetDate`). Monthly is the decision.

### Data Aggregation
- **UC-08**: As a frontend application, I can fetch a full user dashboard from a single optimized endpoint, with no N+1 queries. The dashboard's exact contents are defined in §15.4 — a use case whose output is not enumerable cannot be finished, only extended.

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
8. **An item's `Kind` is permanent.** A note cannot become a task, or the reverse. This is not a policy choice that could be relaxed later without cost: EF Core does not permit changing the discriminator value of an existing row. Converting between types means creating a new item and deleting the old one, which changes its id and orphans nothing but its children. `PATCH /api/items/{id}` therefore never touches `Kind`. **(New in v3.0 — the rule existed in the tooling but was written nowhere.)**
9. **Deleting a node soft-deletes its whole subtree**, across every level, in a single transaction.
10. **Deleting a course soft-deletes the course and every item carrying its `CourseId`** — which, by rule 5, is the entire tree at every depth. No recursive traversal is needed.

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

> **`User.ConsumeTokens` changes in M8.** It currently throws when the quota would be exceeded. That is correct for a pre-check and wrong for post-call accounting, because throwing after a paid external call discards a result that has already been paid for. It will be split into a non-mutating `HasQuotaFor(estimate)` and a `RecordTokenUsage(actual)` that never throws. See §15.1 and ADR-24. Its existing tests change with it.

**Base classes**: `BaseEntity` holds `Id` + `CreatedAt`. `AuditableEntity : BaseEntity` adds `UpdatedAt`. `AiUsageLog` inherits the former — a usage record is never modified, so an audit field on it would be a permanently null column.

**Value object**: `Email` owns normalization (`Trim().ToLowerInvariant()`) and format validation. It exists because that logic was previously duplicated between `User.Create` and `UserRepository`, and a divergence there means a duplicate account slipping past a unique index.

**Two ways to build an `Email`, and the difference is deliberate.** `Email.Create` validates and is used at every entry point. `Email.FromPersisted` does not validate and is used only by the EF Core value converter when reading a row. A converter is a mapping, not a gate: if it validated, a single corrupt row would turn every read of that user — including the login lookup — into a 500 instead of a rejected credential. This is the same failure shape as `D4`, in a different place.

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
- **BCrypt.Net-Next** for password hashing, `Enhanced*` variants only.
- **`System.IdentityModel.Tokens.Jwt`** for token generation (M6).
- Implements every interface declared in Application.

### API — `StudyHub.API`
- ASP.NET Core **Controllers** (ADR-14). Every action is 3–5 lines: build a command, send it, map the result.
- `IExceptionHandler` + `ProblemDetails` for centralized error translation (ADR-15).
- **OpenAPI** via `AddOpenApi()`.
- **`Microsoft.AspNetCore.Authentication.JwtBearer`** (M6).

### Database
PostgreSQL 15 in Docker Compose. Schema versioned through EF Core Migrations. Referential integrity enforced at the database level, not only in code.

### Testing
xUnit + Moq + FluentAssertions, across `StudyHub.Domain.Tests`, `StudyHub.Application.Tests`, and `StudyHub.Infrastructure.Tests`.

---

## 5. Architectural Decisions (ADRs)

| # | Decision | Rationale | Cost accepted |
|---|---|---|---|
| 01 | Clean Architecture, 4 layers | Separation of concerns; business logic testable in milliseconds | More projects and indirection than a 3-layer app needs |
| 02 | MediatR (CQRS) | One file per use case; cross-cutting behaviour added once in the pipeline | An extra indirection between endpoint and logic |
| 03 | FluentValidation | Rules declarative, independently testable, run before every handler | A second place to look when tracing a rejection |
| 04 | EF Core Code-First | Entities are the source of truth; schema history versioned in git | Migration discipline required; the snapshot file is easy to get wrong |
| 05 | PostgreSQL 15 | Strong relational guarantees; identical in Docker and in most clouds | — |
| 06 | JWT + refresh token rotation | Stateless auth with revocable sessions | Refresh tokens must be stored, hashed, and rotated. A revoked user keeps a working access token until it expires (ADR-21) |
| 07 | BCrypt with `Enhanced*` variants | Configurable work factor; pre-hashing removes BCrypt's silent 72-byte truncation | **Irreversible**: standard and enhanced hashes are not interchangeable once stored |
| 08 | **Notes and tasks in one `Items` table (TPH)** | Distinct types that can nest inside each other is a contradiction the relational model cannot express cheaply. Three alternatives were costed — a polymorphic parent (no foreign key at all), an exclusive arc (six parent columns), a fully dissolved model — and each demanded a real sacrifice. Unifying the two types removes the contradiction instead of paying for it | `Status`, `Priority`, `DueDate` are nullable at the database level; conditional check constraints replace `NOT NULL`. `Kind` becomes immutable (rule 3.2.8) |
| 09 | **Child inherits `CourseId`** | Deliberate duplication. Makes "delete a course and its whole tree" a single non-recursive query at any depth | Safe **only** while node moving is forbidden. If moving is added, `CourseId` must be rewritten down every descendant |
| 10 | **`Depth` stored as a column** | Enables `CK_Item_Depth` and `CK_Item_RootDepth` at the database level, and removes the need for a depth-calculation service | Same dependency on rule 3.2.7 as ADR-09. Also caps AI extraction depth — see ADR-26 |
| 11 | Level-by-level subtree fetch, not `WITH RECURSIVE` | "One query per level" only matters when levels are unbounded; depth is capped at five. Staying in LINQ keeps the soft-delete filter applied automatically and avoids a hand-maintained SQL string | At most five round trips instead of two |
| 12 | Soft delete + EF global query filters | Nothing is ever physically lost | Every table grows and never shrinks; raw SQL bypasses the filter entirely; correct restore is not possible until ADR-25 lands |
| 13 | Repository + Unit of Work over EF Core | Handler tests need no database; the pattern is worth learning | **Technically redundant** — `DbContext` is already a unit of work and `DbSet<T>` already a repository. An extra abstraction layer, and `IQueryable` composition is lost at the boundary |
| 14 | Controllers, not Minimal APIs | Attribute routing, filters, and `[Authorize]` are conventional and well documented | Slightly more ceremony per endpoint |
| 15 | `IExceptionHandler`, not custom middleware | The modern ASP.NET Core replacement; returns RFC-shaped `ProblemDetails` | — |
| 16 | Duplicate token accounting: `User.TokensUsedThisMonth` **and** `AiUsageLogs` | The counter answers "is there quota left" in one read; the log answers "what was it spent on" | They must be updated together, inside one method on `User`, or they drift |
| 17 | Manual DTO mapping via static extension methods | No extra dependency; explicit | Boilerplate per DTO; swappable for AutoMapper/Mapster later without touching Domain |
| **18** | **All timestamps are UTC; the API rejects any value that is not** | A `DateTime` with `Kind = Unspecified` is not a moment in time, it is a moment in an unstated timezone. Npgsql refuses to write it to a `timestamptz` column, so the alternative to rejecting it is a 500 | A client sending an unambiguous offset such as `+03:00` is also rejected, even though it could be converted. One validator rule instead of a conversion nobody can see |
| **19** | **Multiple concurrent sessions; one rotation chain per device** | A user with a phone and a laptop should not be logged out of one by using the other. The `IRefreshTokenRepository` shape already assumes it | Reuse detection revokes *every* chain, so one stolen token logs the user out everywhere. The `RefreshTokens` table grows without bound until cleanup lands in M10 |
| **20** | **The refresh token is returned in the response body, not a cookie** | There is no browser client and no frontend. A cookie is a browser-specific optimization that brings CSRF with it, and this project has no CSRF defence | The client is fully responsible for storing it safely. A browser client that puts it in `localStorage` loses it to any XSS |
| **21** | **The JWT carries `sub`, `jti`, and the standard registered claims — nothing else** | Every claim is a copy of a database row frozen at issue time. `sub` is the only value that cannot go stale | Any handler needing the name or email reads the database. Deactivating a user does not invalidate an already-issued access token; it takes effect within the access-token lifetime, at most 15 minutes |
| **22** | **Offset pagination (`page`, `pageSize`), default 20, hard maximum 100** | Per-user datasets are small; offset is simpler and permits jumping to a page | Pages shift when rows are inserted or deleted between requests, and deep offsets get slow. Switch to cursor pagination when either becomes visible |
| **23** | **A subtree is returned as a flat list, not nested JSON** | Projects straight into a DTO with one `Select`, is trivially paginated, and needs no self-referencing DTO or recursive mapper | The client assembles the tree from `ParentItemId`. Roughly ten lines of client code |
| **24** | **Quota is pre-checked against a reserve; usage recording never throws** | An external call that has already been paid for must never have its result discarded by an accounting rule | The hard guarantee "the counter never exceeds the quota" is lost — a single operation may overshoot by the gap between estimate and actual. Some requests are refused that would have fitted |
| **25** | **`DeletedAt` + `DeletedBatchId` replace `IsDeleted` — scheduled M7** | Correct restore needs to distinguish an item deleted deliberately from one deleted by cascade. A shared batch id per delete operation answers that in one column | A migration, a query-filter change, a `MarkAsDeleted` signature change, and every test asserting `IsDeleted`. **Cheap only while no real data exists — this is why it is scheduled, not deferred indefinitely** |
| **26** | **AI extraction is refused when the source note sits at maximum depth** | Extracted tasks are children of their source note; a child of a depth-4 note is depth 5, which the entity rejects. Refusing before the external call spends nothing | A user cannot extract from a deeply nested note at all. The alternative — creating the tasks as siblings — would silently break the provenance that is UC-06's entire justification |

---

## 6. Application Layer Structure (Vertical Slices)

Each feature owns a folder containing everything it needs. The slice follows the **use case**, not the database table — `CreateNoteCommand` lives under `Notes/` even though it writes to `Items` through `IItemRepository`.

```
StudyHub.Application/
├── Common/
│   ├── Interfaces/   → ICourseRepository, IItemRepository, IUserRepository,
│   │                   IRefreshTokenRepository, IUnitOfWork, IPasswordHasher,
│   │                   ICurrentUserService, ITokenService
│   ├── Behaviors/    → ValidationBehavior.cs
│   └── Exceptions/   → one file per exception type:
│                       ConflictException.cs, NotFoundException.cs,
│                       ForbiddenException.cs, QuotaExceededException.cs,
│                       ExternalServiceException.cs
│
├── Auth/Commands/{Login, RefreshToken, Logout}/
├── Courses/Commands/{CreateCourse, UpdateCourse, DeleteCourse}/
├── Courses/Queries/{GetCourses}/
├── Notes/Commands/CreateNote/
├── Tasks/Commands/{CreateTask, UpdateTaskStatus, UpdateTaskSchedule}/
├── Items/Commands/{UpdateItemContent, DeleteItem}/
├── Items/Queries/{GetItem, GetItemTree, GetRootItems}/
└── Users/Commands/RegisterUser/
```

**One exception type per file.** Three types sharing one file compiles and runs, but the file name then describes only one of its contents, and the comments inside end up naming files that do not exist.

**Request flow, end to end:**
1. A controller action builds a Command and calls `mediator.Send(...)`.
2. `ValidationBehavior` runs the matching validator. Invalid input throws before the handler exists.
3. The handler resolves ownership, calls a domain factory or mutator, and saves through `IUnitOfWork`.
4. Any exception is translated to a status code by `GlobalExceptionHandler`.

**Defence in depth is deliberate.** Ownership of a parent item is checked twice — once in the handler (to return a precise 403) and once inside `Item.Initialize` (because the entity trusts no caller). Course ownership is checked in the handler only, since the entity receives a `Guid` rather than an object; that asymmetry is known and accepted.

**Commands and queries have different fetch rules.** A query projects directly into its DTO with `Select` and never materializes an entity. A command loads the whole entity, because it is about to call a method on it.

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
| IsDeleted | bool | soft delete — becomes `DeletedAt` + `DeletedBatchId` in M7 (ADR-25) |
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
| IsDeleted | bool | soft delete — becomes `DeletedAt` + `DeletedBatchId` in M7 (ADR-25) |
| Kind | int | TPH discriminator: 0 = Note, 1 = Task. **Immutable** (rule 3.2.8) |
| Status | int? | tasks only |
| Priority | int? | tasks only |
| DueDate | timestamptz? | tasks only; UTC only (§14.1) |
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

**The cascade never fires today.** Users are deactivated, never deleted. The delete behaviour is correct as an intent — a session belongs to nobody but its user — but nothing exercises it.

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
| Method | Route | Auth | Returns |
|---|---|---|---|
| POST | `/api/auth/login` | anonymous | 200 + token pair |
| POST | `/api/auth/refresh` | anonymous¹ | 200 + new token pair |
| POST | `/api/auth/logout` | user | 204 |

¹ The refresh endpoint is anonymous by design: the access token it is meant to replace has usually already expired, so requiring it would make the endpoint useless exactly when it is needed. The refresh token in the body *is* the credential.

**Login and refresh both return this shape** (ADR-20):

```json
{
  "accessToken": "eyJhbGciOi...",
  "refreshToken": "kO3n...Xq",
  "tokenType": "Bearer",
  "expiresIn": 900
}
```

`expiresIn` is the access token's remaining lifetime in **seconds**, so the client never has to parse the JWT to schedule a refresh. No user profile fields are included (ADR-21); a client needing them calls `GET /api/auth/me`.

### Planned — M7
`GET /api/auth/me`, `GET /api/courses`, `GET /api/courses/{id}/tree`, `GET /api/items`, `GET /api/items/{id}`, `GET /api/items/{id}/tree`, `PATCH /api/items/{id}`, `PATCH /api/tasks/{id}/status`, `PATCH /api/tasks/{id}/schedule`, `PUT /api/courses/{id}`

### Error contract
All errors return RFC 9457 `ProblemDetails`:

| Exception | Status |
|---|---|
| `ValidationException` | 400 + `errors` grouped by field |
| — (no or invalid token) | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `QuotaExceededException` | 429 |
| `ExternalServiceException` | 502 |
| anything else | 500, with no internal detail leaked |

**401 has no exception type**, because it never originates in a handler. The JWT middleware rejects the request before MediatR runs. A handler that wants to reject on identity throws `ForbiddenException` and gets 403 — the caller was authenticated, just not entitled.

**Two different sources produce 400.** Model binding inside `[ApiController]` rejects malformed JSON *before* MediatR runs; its response carries a `traceId`. A `ValidationException` from a validator does not. Useful when diagnosing.

---

## 9. Security Model

### ⚠ Current state — authentication is not implemented

Identity comes from an `X-User-Id` request header read by a temporary `CurrentUserService`. **This is a complete authentication bypass**: anyone can name any user and become them. It exists solely so the content handlers could be built and tested before M6.

**This code must not be deployed or exposed on any network until M6 is complete.**

The design limits the blast radius of replacing it: the Application layer depends on `ICurrentUserService`, not on HTTP. When JWT arrives, only the API-layer implementation changes — not one line in Application.

### 9.1 Authentication decisions (new in v3.0)

These six were listed as open questions in v2.0. They are not open questions — each of them blocks a specific file in M6, so they are decided here.

| # | Decision | Value | Cost |
|---|---|---|---|
| 1 | Access token lifetime | **15 minutes** | A revoked user stays usable for up to 15 minutes |
| 2 | Refresh token lifetime | **7 days**, renewed in full on every rotation | An active session never expires. An absolute session cap is deferred (§12) |
| 3 | Concurrent sessions | **Unlimited; one chain per device** (ADR-19) | Reuse detection logs the user out of every device |
| 4 | Refresh token transport | **Response body** (ADR-20) | Client-side storage is the client's problem |
| 5 | JWT claims | **`sub`, `jti`, `iss`, `aud`, `iat`, `exp`** (ADR-21) | Name and email require a database read |
| 6 | Logout scope | **The presented refresh token only** | No "sign out everywhere"; deferred (§12) |

**The signing key** lives in `dotnet user-secrets`, never in `appsettings.json`. HS256 requires at least 32 bytes; a shorter key throws at *runtime*, not at build.

**A trap worth writing down before it happens.** `CurrentUserService` will read the user id from the `ClaimsPrincipal`. Whether it appears as `sub` or as `ClaimTypes.NameIdentifier` depends on which handler validated the token and whether inbound claim mapping was cleared. Both spellings compile, and the wrong one returns `null` at runtime, not an error. **Verify by enumerating the actual claims once and reading them** — this is verification rule 3 (§10) applied before the fact rather than after it.

### 9.2 Login rules

1. **One message for every failure.** "No such user" and "wrong password" return **401 with identical text**. Distinguishing them hands an attacker an account-enumeration tool.
2. **Check `IsActive`** before issuing any token — and return the same 401 text, for the same reason.
3. **Always call `Verify`**, even when no user was found, against a fixed dummy hash. Without it, response time separates the two cases and leaks exactly what rule 1 protects.
4. **Never log the password**, not even on failure.

### 9.3 Refresh rules

Hash the incoming token → look it up → check `IsActive(utcNow)` → issue a new pair → `oldToken.Revoke(utcNow, newToken.Id)` → save. **One `SaveChangesAsync` is already one transaction in EF Core; no explicit transaction is needed and none should be added.**

**Reuse detection**: a token that is presented after already being revoked means the chain was stolen. The response is to revoke every active token the user has, on every device. `ReplacedByTokenId` exists for exactly this.

### 9.4 Known accepted risks

- **Registration reveals whether an email is already in use.** This is the one that undermines the others: §9.2 builds careful anti-enumeration into login, and `POST /api/auth/register` gives the same information away on an easier endpoint. Closing it properly requires an email-verification flow (always return 202, send a mail either way), which is out of scope. **Recorded here rather than fixed, so that login's protection is not mistaken for complete protection.** Rate limiting (M10) reduces how fast the list can be harvested, not whether it can be.
- **An access token outlives a deactivation or a logout** by up to its lifetime. This is the price of stateless auth (ADR-06). Checking the database on every request would remove the reason JWT was chosen.
- **Course ownership is guarded in one layer only** (see §6).
- **Password length is unbounded.** Safe because `EnhancedHashPassword` pre-hashes the input, removing BCrypt's 72-byte truncation. Reverting to the standard variant would silently reintroduce it.

---

## 10. Testing Strategy

**Why Domain and Application first**: both are free of any dependency on a database, a web server, or the network. The full suite runs in about two seconds with Docker stopped.

| Project | Scope |
|---|---|
| `StudyHub.Domain.Tests` | Entity factories, invariants, tree rules |
| `StudyHub.Application.Tests` | Each handler in isolation, repositories mocked |
| `StudyHub.Infrastructure.Tests` | Password hashing, token generation and hashing — no database |

**Naming**: `MethodName_Scenario_ExpectedResult`. **Structure**: Arrange–Act–Assert, separated by blank lines. **Mocking**: only interfaces declared in Application; never the entity under test.

**Definition of done**: every new handler ships with at least one success test and one failure test.

**Extended in v3.0**: every validator containing a **conditional or cross-field rule** ships with a test. Plain `NotEmpty` and `MaximumLength` do not need one. The rule that a nested item may not carry its own `CourseId` currently lives in the validator alone — the entity ignores the value silently rather than rejecting it — so deleting that rule would break the API without turning a single test red.

**Assert on what did not happen too.** A failure test verifies that the exception was thrown *and* that `SaveChangesAsync` was never called.

**Not automated yet**: EF Core queries and HTTP round-trips need integration testing against a containerized database. Scheduled for M10. Until then, manual verification through `StudyHub.API.http` and direct `psql` inspection is the substitute — and `psql` is the stronger of the two, because it is the only view that does not pass through EF's soft-delete filter.

### Three verification rules learned the hard way
1. **A successful build proves nothing.** Every change is verified by its own specific effect: an HTTP status code, a row in the database, a passing test.
2. **A constraint is proven by trying to break it *and* by inserting a row that should pass.** One without the other is half an answer. And read *which* constraint the error names — a malformed statement produces a red error and no inserted row, which is exactly what a working constraint produces.
3. **A change with no externally observable behaviour is verified by reading the file.** When no code path calls the changed method yet, the build passes, the tests pass, and the endpoints behave identically whether or not the change was ever applied. Nothing else will catch it.

> **A corollary added in v3.0.** Rule 3 is a last resort, not a first choice. Before accepting "read the file" as the proof, ask whether the change could be given observable behaviour *today* by a unit test. Two proofs in the M6 checklist — that a corrupt stored hash returns `false` instead of throwing (`D4`), and that two passwords sharing 72 bytes do not verify each other (`D5`) — were deferred to a milestone they never depended on, because a proof was assumed to mean an HTTP status code. Both are unit tests over `IPasswordHasher` and need no endpoint, no database, and no login flow.

---

## 11. Roadmap

| # | Scope | Status |
|---|---|---|
| M1 | PRD & requirements | ✅ |
| M2 | Solution & Clean Architecture layers | ✅ |
| M3 | Domain entities, DbContext, relational integrity | ✅ |
| M4 | Application foundation: MediatR, FluentValidation, `ValidationBehavior`, test projects | ✅ |
| **M5** | **Hardening & the content tree**: global exception handling, domain and security fixes, `Email` value object, base-class split, clean schema with six check constraints, the `Items` TPH restructure, create/delete handlers, controllers | ✅ |
| **M5.1** | **Cleanup**: file encoding, exception file split, `Infrastructure.Tests`, `Email.FromPersisted`, UTC rule | ⏳ In progress |
| **M6** | **Authentication (UC-01, UC-02)**: login, JWT issuance, refresh rotation, reuse detection, removal of the `X-User-Id` bypass | Next |
| M7 | Content completion (UC-03, UC-04, UC-05): DTOs, read queries, update handlers, pagination, quota to configuration, **`DeletedAt` + `DeletedBatchId` migration (ADR-25)** | Pending |
| M8 | AI integration & quota enforcement (UC-06, UC-07): `ConsumeTokens` split, failure model, extraction depth guard | Pending |
| M9 | Dashboard aggregation (UC-08) as defined in §15.4 | Pending |
| M10 | Integration tests, rate limiting, refresh-token cleanup, API containerization | Pending |

> **Renumbering note.** v1.1 listed M5 as authentication and M6 as content management. What was actually built in that slot was hardening plus the content tree. The roadmap has been rewritten to match what happened rather than what was planned — which also keeps every `(M5)` tag in the troubleshooting log accurate. M5.1 is numbered as a point release for the same reason: it is cleanup of M5's output, not a milestone of its own, and giving it a whole number would shift every tag after it.

---

## 12. Deliberately Deferred

Each of these is a decision, not an oversight.

| Item | Why deferred | What it will cost later |
|---|---|---|
| **Node moving** | No use case requires it; it is the single largest source of complexity in a free-nesting tree | Cycle detection, subtree depth recalculation, `CourseId` rewrite down every descendant. Invalidates ADR-09 and ADR-10 |
| **Unarchive / restore** | Not requested — but the schema change that makes it *possible* is scheduled in M7, because it is cheap only while the database is empty | Currently impossible: after a cascade delete nothing distinguishes a child deleted deliberately from one deleted by cascade. ADR-25 fixes the schema; the restore handler itself remains deferred |
| **Absolute session lifetime cap** | Rotation with reuse detection already limits the damage of a stolen token | A `SessionStartedAt` column or a walk back up the `ReplacedByTokenId` chain, plus a forced re-login the user did not ask for |
| **"Sign out of all devices"** | Logout is per-device (§9.1); the all-device revocation path already exists for reuse detection | Roughly five lines over `GetActiveByUserAsync`, plus an endpoint |
| **Refresh token cleanup** | The table only grows with sessions, and there are none yet | A background job or a scheduled `DELETE` for rows expired more than N days ago. M10 |
| **Changing an item's `Kind`** | EF Core does not permit changing a discriminator on an existing row (rule 3.2.8) | Create-new-and-delete-old semantics, a new id, and a decision about what happens to the children |
| **Cursor pagination** | Offset is adequate at per-user scale (ADR-22) | A stable sort key, an opaque cursor format, and the loss of "jump to page 7" |
| **Many-to-many between tasks and notes** | Parenthood covers the known cases | A join table and a second relationship model |
| **Integration tests** | Domain and Application carry the business logic | A containerized test database and a slower suite |
| **API containerization** | `dotnet run` is sufficient locally | A Dockerfile and compose changes |
| **Rate limiting** | No public exposure yet | Middleware plus a policy per endpoint group. Also the only practical mitigation for §9.4's enumeration risk |
| **Email verification on registration** | Out of scope for a learning project | The only complete fix for account enumeration: always return 202, and send a different mail depending on whether the address was already registered |
| **`Content` length limit** | It is the note body; an arbitrary cap would be guesswork | A migration if a limit is ever chosen |

---

## 13. Open Questions

Closed since v2.0: API style is **Controllers** (ADR-14); DTO mapping is **manual extension methods** (ADR-17); repository granularity is **one per aggregate**. The six authentication questions are answered in §9.1, and the pagination, tree-shape, and quota-ordering questions in §14 and §15.

Still genuinely open — meaning nothing is blocked while they stay unanswered:

1. **AI provider abstraction** — is a single `IAiService` enough, or should the prompt and response contract be modelled explicitly before M8?
2. **Estimated cost per AI operation** (§15.1) — a fixed reserve per operation type, or a multiple of the input length? The second is more accurate and needs a tokenizer.
3. **Should `GET /api/courses` return item counts per course?** Useful for a dashboard; a possible N+1 if written carelessly.

---

## 14. Cross-Cutting Rules

These belong to no single milestone. That is precisely why they were missing from v2.0 — a rule with no obvious home does not get written, and then gets rediscovered as a bug in every milestone separately.

### 14.1 Time

**Every timestamp crossing the API boundary is UTC, in ISO 8601, ending in `Z`.**

```
2026-10-01T14:00:00Z      ✅ accepted
2026-10-01T14:00:00+03:00 ❌ 400
2026-10-01T14:00:00       ❌ 400
```

**Why this is not pedantry.** `DueDate` maps to a `timestamptz` column, and Npgsql refuses to write a `DateTime` whose `Kind` is `Unspecified`. A JSON value without an offset parses to exactly that. So the choice is not between strict and lenient — it is between a 400 that explains itself and a 500 that does not. This is a live defect today: no request in `StudyHub.API.http` has ever sent a `dueDate`, which is the only reason it has not been hit.

**Rules:**
- Validators reject any `DateTime` whose `Kind` is not `Utc`.
- Entities read `DateTime.UtcNow` and never `DateTime.Now`.
- A due date in the past is **valid**. Recording a task that was due yesterday is a normal thing to do.
- Timezone presentation is the client's job. The API neither stores nor asks for the user's timezone.

**Cost, stated plainly**: a client sending an unambiguous `+03:00` offset gets rejected even though the value could have been converted. Accepting it would mean a conversion whose result depends on the *server's* local timezone — correct, but invisible and hard to verify. One rejected format is cheaper than one invisible conversion.

### 14.2 Pagination

Every endpoint returning a **collection** paginates. Every endpoint returning a **tree** does not.

| Parameter | Default | Maximum |
|---|---|---|
| `page` | 1 | — |
| `pageSize` | 20 | 100 |

The response wraps the items:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 137
}
```

**Why a tree is exempt.** Splitting a subtree across pages returns children whose parents are on another page — the client cannot assemble anything from that. A subtree is bounded by depth 5 and by one course, so it is returned whole.

**The risk that comes with the exemption**: a course containing tens of thousands of items produces one very large response. No cap exists today. If it becomes real, the answer is a documented limit and a 400, not silent truncation — a truncated tree is indistinguishable from a complete one.

### 14.3 Logging

**Never logged, under any circumstance:** passwords (plain or hashed), raw refresh tokens, refresh token hashes, JWT signing keys, connection strings.

**Log the user id, not the email.** The id identifies the account for debugging without putting an address in a log file that outlives its usefulness.

**Expected exceptions log at Warning with the type name only. Unexpected ones log at Error with the full exception.** A 404 is not an incident; a 500 is. Logging them at the same level means neither can be found.

---

## 15. AI Integration Model

`UC-06` and `UC-07` are two sentences, and behind them is the milestone most likely to break — because the ordering problem below is not visible from the use case text at all.

### 15.1 Quota ordering — the problem and the decision

`User.ConsumeTokens` throws when the quota would be exceeded. The natural sequence is:

```
call the provider → pay → count tokens → ConsumeTokens → throws → result discarded
```

The money is spent and the result is thrown away by an accounting rule. **The check has to happen before the call, not after it.**

**Decision (ADR-24)** — three steps, in this order:

1. **Before the call**: `user.HasQuotaFor(estimatedCost)`. If false, throw `QuotaExceededException` → 429. Nothing external is invoked.
2. **The call.**
3. **After the call**: `user.RecordTokenUsage(actual)` — **never throws**, even if `actual` exceeds what remained. Writes the `AiUsageLog` row in the same transaction (ADR-16).

`ConsumeTokens` is replaced by these two. The split is the point: a *question* about quota and a *record* of spending are different operations, and merging them is what created the trap.

**Cost accepted**: the counter may overshoot the quota by the gap between estimate and actual, for one operation only. The next `HasQuotaFor` sees the real figure and refuses. A hard cap that could discard paid work is worse than a soft cap that cannot.

**Also**: `ResetQuotaIfNeeded(utcNow)` runs before the pre-check, or a user entering a new month is refused against last month's counter.

### 15.2 Depth and extraction

Extracted tasks are children of their source note (`UC-06`). A child of a depth-4 note would be depth 5, which `Item.Initialize` rejects and `CK_Item_Depth` rejects again.

**Decision (ADR-26)**: refuse the extraction **before calling the provider** when the source note is at `Item.MaxDepth`. Return 400 with a message naming the reason.

The alternative — creating the tasks as siblings instead of children — was rejected: it breaks provenance silently, and provenance-as-parenthood is the entire justification for `UC-06`'s shape. A refusal the user can read beats a result they cannot trust.

### 15.3 Failure model

Nothing originating outside the process reaches the client as a 500.

| Failure | Exception | Status |
|---|---|---|
| Quota insufficient before the call | `QuotaExceededException` | 429 |
| Provider unreachable, timed out, or returned an error | `ExternalServiceException` | 502 |
| Provider returned a response that could not be parsed | `ExternalServiceException` | 502 |
| Source note at maximum depth | `ValidationException` | 400 |

**No automatic retry.** A retry on a call that has already been billed doubles the cost to fix a failure that may not be transient. If a retry policy is ever added, it applies only to failures that are provably pre-billing — a connection refused, not a timeout after the request was accepted.

**A timeout is configured explicitly.** The default `HttpClient` timeout is 100 seconds, which is not a decision, only an absence of one.

**Both exceptions are Application-layer types.** The provider's own exception types stay inside Infrastructure, for the same reason `PostgresException` does (`CODING_STANDARDS.md` §1).

### 15.4 Dashboard definition (UC-08)

"Statistics, active courses, urgent tasks" cannot be finished, only extended. This is what the endpoint returns:

**Counts**
- Courses not deleted
- Items by kind (notes, tasks)
- Tasks by status (Pending, InProgress, Completed)
- Tasks overdue: `Status != Completed AND DueDate < utcNow`

**Urgent tasks** — at most **10**, ordered by `DueDate` ascending:

```
Status != Completed
AND DueDate IS NOT NULL
AND DueDate <= utcNow + 3 days
```

Overdue tasks are included, because a task that is already late is more urgent than one due tomorrow, not less.

**Recent courses** — at most 5, ordered by `UpdatedAt` descending, falling back to `CreatedAt`.

**The N+1 requirement is verified, not asserted.** Enable EF Core SQL logging, call the endpoint against a user with many courses and items, and count the statements. The count must be a small fixed number and must not grow with the data. "It looked fast" is not the proof.
