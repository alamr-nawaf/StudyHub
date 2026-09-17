# StudyHub — Engineering Requirements (PRD) v3.0

> **Scope of this document**: *what* the system does and *why* each decision was made.
> For *how it is built*, see `docs/ARCHITECTURE.md`. For *code style*, see `docs/CODING_STANDARDS.md`. For *problems hit and fixed*, see `docs/TROUBLESHOOTING.md`.
>
> **v3.0 supersedes v2.0.** No behaviour already built has changed. What changed is that decisions previously left open — or never written down at all — are now decided. Section numbers 1–13 are unchanged, so every cross-reference in `ARCHITECTURE.md` and `TROUBLESHOOTING.md` stays valid; the two new sections are appended as §14 and §15. Inside §3.2, the new rule 8 shifts the two deletion rules to 9 and 10; no other document refers to them by number.
>
> **Reading convention.** A statement tagged with a milestone — *(M6)*, *(M7)* — describes something not built yet, and names the milestone that delivers it. An untagged statement describes code that exists today. Describing unbuilt code in the present tense is the documentation form of `G4`.
>
> **What is new in v3.0**
> - §14 — Cross-Cutting Rules: time, pagination, logging, concurrency. Rules that belong to no single milestone, which is exactly why they were missing.
> - §15 — AI Integration Model: the approval flow, the quota ordering problem, the depth conflict, the failure model, and the dashboard definition.
> - §9 — the six authentication decisions that M6 cannot start without, and what happens when two refreshes race.
> - §2 — `UC-03` and `UC-05` no longer promise archiving, because restore is not possible as designed; `UC-06` makes the user's approval explicit.
> - §3 — rule 3.2.8 (`Kind` is permanent), and rules that handlers ask the entity about instead of restating (ADR-30).
> - §5 — ADR-18 through ADR-30.

---

## 1. Project Vision

StudyHub is a backend API for managing courses, study notes, and tasks, with AI-suggested tasks extracted from raw notes, which the user approves before anything is written.

This is a **personal learning project**. The explicit goal is to practise professional .NET backend engineering — Clean Architecture, CQRS, domain modelling, schema design, and a disciplined verification habit — not to ship the fastest possible MVP. Where a decision has a cost, that cost is written down rather than hidden.

---

## 2. Core Use Cases

### Identity & Access Management
- **UC-01**: As a user, I can register and log in securely so that my data is protected.
- **UC-02**: As a user, my session stays active through refresh tokens without frequent manual logins, and a stolen token can be revoked.
- **UC-09**: As an administrator, I can deactivate a user's account, so that an abusive or compromised account can be stopped without deleting anything the person wrote.

> **UC-09 is numbered 09, not 03.** Numbers are never reused and never shifted: every other document, test name, and log entry that already points at UC-03 through UC-08 would otherwise point at the wrong thing. The same reasoning gave M5.1 a decimal instead of a whole number (§11).

> **Why deactivation is the first and only administrative capability.** `User.Deactivate` has existed since M3 with no caller — a behaviour that nothing can invoke is not merely unused, it is unproven. UC-09 gives it one. A capability such as listing users is *not* in scope here: it will be added when an endpoint needs it, not in advance (ADR-31, and the A16 precedent — two of three planned tree operations were built and then never needed).

### Content Management (Core Domain)
- **UC-03**: As a user, I can create, edit, and delete courses. Deletion is a soft delete — the row stays in the database — but it is not reversible through the API. A course is always a top-level container and is never nested inside anything.
- **UC-04**: As a user, I can create notes and tasks that either stand alone, sit under a course, or nest under another note or task — in any combination, up to five levels deep. Tasks additionally carry a status (Pending, InProgress, Completed), a priority, and an optional due date.
- **UC-05**: As a system, deleting any node deletes its entire subtree across every level, using soft delete so nothing is physically removed from the database.

> **UC-03 and UC-05 no longer say "archive" (v3.0).** v2.0 used the word *archive*, which promises the user that what was archived can be seen and restored. §12 states that correct restore is impossible with the current `IsDeleted` design. A document that promises what the design cannot deliver is worse than one that admits the gap. The word is now *delete*. The `DeletedAt` + `DeletedBatchId` change that would make restore possible is scheduled in M7 — see §12 and ADR-25.

> **UC-05 reverses v1.1 deliberately.** The v1.1 wording specified *"Soft-Delete Cascade prevention"* — deleting a course was supposed to leave its tasks untouched. The current design does the opposite: deletion cascades down the whole tree. This is an intentional change of direction, not a correction of a bug. Rationale in ADR-08 and ADR-09.

### AI Integration & Cost Control
- **UC-06**: As a user, I can submit a note to an AI service and receive suggested actionable tasks. Nothing is written until I approve a suggestion; each approved task is created as a child of the source note, so the provenance is visible in the tree itself. A note already at maximum depth cannot be used as an extraction source — see §15.2.
- **UC-07**: As a system, I enforce a **monthly** AI token quota per user to prevent abuse and control external API cost. The quota is checked *before* the external call, never only after it — see §15.1.

> **UC-06 makes the approval explicit (v3.0).** `ARCHITECTURE.md` §4.6 already settled "human in the loop", while v2.0's wording — "extracted tasks are created" — read as if tasks were written automatically. Approval needs no endpoint of its own: the client creates each approved task through the existing `POST /api/tasks`. See §15 and ADR-28.

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
3. **Maximum depth is five levels** — `Depth` runs 0 to 4. Enforced in the entity (`Item.MaxDepth`) *and* by `CK_Item_Depth`. A request that would exceed it is refused by the handler with 409 before the entity is called (ADR-30).
4. **Depth is stored, not computed.** Because node moving is not supported (rule 7), an item's depth is fixed at creation and never changes. `CK_Item_RootDepth` enforces that `ParentItemId IS NULL` if and only if `Depth = 0`.
5. **A child inherits its parent's `CourseId`.** A request that sends a course together with a parent is rejected by the validator — *any* course, the parent's own included, because sending both is ambiguous rather than a preference. The entity ignores the value regardless. This is deliberate duplication (ADR-09).
6. **Ownership unity.** A child's `UserId` always equals its parent's. Enforced inside `Item.Initialize` — so a handler that forgets to check ownership still cannot produce a cross-owner link.
7. **Node moving is not supported.** An item's parent is fixed at creation. This is a deliberate deferral, and three consequences follow from it:
   - Cycles are impossible. A newly created child has no descendants, so it cannot enclose its own ancestor. No cycle detection code exists, and none is needed while this rule holds.
   - Depth is permanent, which is what makes rule 4 safe.
   - `CourseId` never changes, which is what makes rule 5 safe.

   **If node moving is ever added, all three collapse at once.** It would require cycle detection, depth recalculation for the entire moved subtree (`parent depth + subtree height ≤ max`), and a `CourseId` rewrite down every descendant.
8. **An item's `Kind` is permanent.** A note cannot become a task, or the reverse. This is not a policy that could be relaxed later for free: under TPH, EF Core derives the discriminator from the object's CLR type, and an object cannot change its type — so a type change is a delete and a create, not an update. The new item gets a new id, and the old item's children cannot follow it: moving them is forbidden (rule 7), so deleting the old item deletes them too (rule 9). `PATCH /api/items/{id}` therefore never touches `Kind`. **(New in v3.0 — the rule existed in the tooling but was written nowhere.)**
9. **Deleting a node soft-deletes its whole subtree**, across every level, in a single transaction.
10. **Deleting a course soft-deletes the course and every item carrying its `CourseId`** — which, by rule 5, is the entire tree at every depth. No recursive traversal is needed.

### 3.3 Entity behaviour

Entities are rich: private setters, private constructors, static factory methods that enforce invariants at creation.

| Entity | Behaviour |
|---|---|
| `User` | `Create`, `ConsumeTokens`, `ResetQuotaIfNeeded`, `Deactivate`, `ChangePassword`; `PromoteToAdmin`, `Can(permission)` |
| `Course` | `Create`, `UpdateDetails`, `MarkAsDeleted` |
| `Item` (abstract) | `Initialize` (protected), `UpdateContent`, `MarkAsDeleted`; `IsAtMaxDepth` (read-only query, ADR-30) |
| `Note : Item` | `Create` |
| `TaskItem : Item` | `Create`, `UpdateStatus`, `UpdateSchedule` |
| `RefreshToken` | `Create`, `IsActive(utcNow)`, `Revoke(utcNow, replacedBy)` |
| `AiUsageLog` | `Create` (write-once; no mutators by design) |

> **`User.ConsumeTokens` changes in M8.** It currently throws when the quota would be exceeded. That is correct for a pre-check and wrong for post-call accounting, because throwing after a paid external call discards a result that has already been paid for. It will be split into a non-mutating `HasQuotaFor(estimate)` and a `RecordTokenUsage(actual)` that never throws. See §15.1 and ADR-24. Its existing tests change with it.

**A rule the handler must ask about is published as a query.** When a handler has to refuse a request that the entity would also refuse, the entity exposes the rule as a read-only member — `IsAtMaxDepth`. The handler asks it and returns a precise 4xx; the mutator asks the same member and throws. The rule is written once and enforced twice. If the mutator's check ever fires, a handler skipped the question: that is a bug, and 500 is the honest answer (ADR-30).

> **`Create` never produces an administrator, and there is no demotion.** The factory always sets `Role = User`; `PromoteToAdmin` is the single path to the other value, named loudly so it cannot pass unnoticed in a review, and tolerant of repetition in the same way `Deactivate` is. Demotion is deferred (§12), which means privilege has exactly one entry point and no exit — the shape that is easiest to audit. `Can(permission)` is the query form of ADR-30: a handler holding a `User` asks the entity instead of computing the answer from the role itself.

**Base classes**: `BaseEntity` holds `Id` + `CreatedAt`. `AuditableEntity : BaseEntity` adds `UpdatedAt`. `AiUsageLog` inherits the former — a usage record is never modified, so an audit field on it would be a permanently null column.

**Value object**: `Email` owns normalization (`Trim().ToLowerInvariant()`) and format validation. It exists because that logic was previously duplicated between `User.Create` and `UserRepository`, and a divergence there means a duplicate account slipping past a unique index.

**Two ways to build an `Email`, and the difference is deliberate.** `Email.Create` validates and is used at every entry point. `Email.FromPersisted` does not validate and is used only by the EF Core value converter when reading a row. A converter is a mapping, not a gate: if it validated, a single corrupt row would turn every read of that user — including the login lookup — into a 500 instead of a rejected credential. This is the same failure shape as `D4`, in a different place, and the same trust EF Core already extends to entities, which it materializes without calling their factories. **Cost**: `FromPersisted` has to be public, because the converter lives in Infrastructure, so any caller can skip validation; its name is the only guard.

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
- **`Microsoft.IdentityModel.JsonWebTokens`** (`JsonWebTokenHandler`) for token generation (ADR-29) — not `System.IdentityModel.Tokens.Jwt`, the previous generation of the same library.
- Implements every interface declared in Application.

### API — `StudyHub.API`
- ASP.NET Core **Controllers** (ADR-14). Every action is 3–5 lines: build a command, send it, map the result.
- `IExceptionHandler` + `ProblemDetails` for centralized error translation (ADR-15).
- **OpenAPI** via `AddOpenApi()`.
- **`Microsoft.AspNetCore.Authentication.JwtBearer`**.

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
| 13 | Repository + Unit of Work over EF Core | Handler tests need no database; the pattern is worth learning | **Technically redundant** — `DbContext` is already a unit of work and `DbSet<T>` already a repository. An extra abstraction layer, and `IQueryable` composition is lost at the boundary — so read queries must project inside Infrastructure; where exactly is open (§13) |
| 14 | Controllers, not Minimal APIs | Attribute routing, filters, and `[Authorize]` are conventional and well documented | Slightly more ceremony per endpoint |
| 15 | `IExceptionHandler`, not custom middleware | The modern ASP.NET Core replacement; returns RFC-shaped `ProblemDetails` | — |
| 16 | Duplicate token accounting: `User.TokensUsedThisMonth` **and** `AiUsageLogs` | The counter answers "is there quota left" in one read; the log answers "what was it spent on" | They must be updated together, inside one method on `User`, or they drift |
| 17 | Manual DTO mapping via static extension methods | No extra dependency; explicit | Boilerplate per DTO; swappable for AutoMapper/Mapster later without touching Domain |
| **18** | **All timestamps are UTC; the API rejects any value that is not** | A `DateTime` with `Kind = Unspecified` is not a moment in time, it is a moment in an unstated timezone. Npgsql writes only `Kind = Utc` to a `timestamptz` column: a value without an offset arrives as `Unspecified`, and one with a non-`Z` offset arrives as `Local`, because the JSON reader converts it to server time. Both throw at save, so the alternative to rejecting them is a 500 | A client sending an unambiguous offset such as `+03:00` is also rejected, even though it could be converted. One rule in every validator that carries a `DateTime`, instead of a conversion nobody can see |
| **19** | **Multiple concurrent sessions; one rotation chain per device** | A user with a phone and a laptop should not be logged out of one by using the other | Reuse detection revokes *every* chain, so one stolen token logs the user out everywhere. The `RefreshTokens` table grows without bound until cleanup lands in M10 |
| **20** | **The refresh token is returned in the response body, not a cookie** | There is no browser client and no frontend. A cookie is a browser-specific optimization that brings CSRF with it, and this project has no CSRF defence | The client is fully responsible for storing it safely. A browser client that puts it in `localStorage` loses it to any XSS |
| **21** | **The JWT carries `sub`, `jti`, `role`, and the standard registered claims — nothing else** | Every claim is a copy of a database row frozen at issue time. `sub` and `role` are the only two worth freezing: the first can never go stale, and the second changes rarely enough that a 15-minute lag is acceptable. Reading the role from the database on every request would remove the reason JWT was chosen (ADR-06) | Any handler needing the name or email reads the database. Deactivating a user does not invalidate an already-issued access token; it takes effect within the access-token lifetime, at most 15 minutes. **A role change carries the same lag, and demotion is the dangerous direction: a revoked administrator keeps administrative rights for that window.** Shortening the window means shortening the access token for everyone |
| **22** | **Offset pagination (`page`, `pageSize`), default 20, hard maximum 100** | Per-user datasets are small; offset is simpler and permits jumping to a page | Pages shift when rows are inserted or deleted between requests, and deep offsets get slow. Switch to cursor pagination when either becomes visible |
| **23** | **A subtree is returned as a flat list, not nested JSON** | Projects straight into a DTO with one `Select`, and needs no self-referencing DTO or recursive mapper. Not chosen for pagination — a tree is never paginated (§14.2) | The client assembles the tree from `ParentItemId`. Roughly ten lines of client code |
| **24** | **Quota is pre-checked against an estimate; usage recording never throws** | An external call that has already been paid for must never have its result discarded by an accounting rule | The hard guarantee "the counter never exceeds the quota" is lost — each request that passes the pre-check may overshoot by the gap between estimate and actual, and parallel requests all pass it before any of them records. Some requests are refused that would have fitted |
| **25** | **`DeletedAt` + `DeletedBatchId` replace `IsDeleted` — scheduled M7** | Correct restore needs to distinguish an item deleted deliberately from one deleted by cascade. A shared batch id per delete operation answers that in one column | A migration, a query-filter change, a `MarkAsDeleted` signature change, and every test asserting `IsDeleted`. **Cheap only while no real data exists — this is why it is scheduled, not deferred indefinitely** |
| **26** | **AI extraction is refused with 409 when the source note sits at maximum depth** | Approved tasks become children of their source note; a child of a depth-4 note is depth 5, which the entity rejects — so every suggestion would be impossible to approve. Refusing before the external call spends nothing | A user cannot extract from a deeply nested note at all. The alternative — creating the tasks as siblings — would silently break the provenance that is UC-06's entire justification |
| **27** | **Optimistic concurrency on rotation: PostgreSQL's `xmin` is the concurrency token of `RefreshTokens`** | Rotation reads a row and writes it back. Without a check, two parallel refreshes with one token both succeed and fork the chain into two valid ones, and reuse detection never fires. `xmin` is maintained by PostgreSQL itself, so no column is added; it is mapped in Infrastructure as a shadow property, so no Domain type gains a persistence field | A legitimate client that refreshes twice in parallel loses one request (409) and must serialize its refreshes. No grace window (§12) |
| **28** | **Extraction returns suggestions; nothing is written until the user approves** *(M8)* | AI output enters the user's tree only through a human decision. Approval needs no endpoint of its own: the client creates each approved task through `POST /api/tasks` with the note as parent. Usage is recorded at extraction time, so approving nothing still costs quota | Once created, an extracted task is indistinguishable from a hand-written one. Suggestions are not stored: a client that loses them pays again to extract again |
| **29** | **Tokens are generated with `Microsoft.IdentityModel.JsonWebTokens`, not `System.IdentityModel.Tokens.Jwt`** | The latter is the previous generation of the same library, and ASP.NET Core 8+ validates bearer tokens with `JsonWebTokenHandler` by default. Writing and reading tokens with the same handler removes one source of claim-name mismatches (§9.1) | Most tutorials still show `JwtSecurityTokenHandler`; their examples need translating |
| **30** | **A handler asks the entity before acting; the entity re-checks the same question** | The rule is written once, in the Domain, and enforced twice with two meanings. In the handler it is an expected refusal and becomes a precise 4xx; in the entity it is an invariant that protects every other caller. Ownership already follows this shape (§6) | A handler that forgets to ask returns 500 instead of 409 — the data stays safe, only the status is wrong. Every handler that nests items must ask |
| **31** | **One `Role` column on `Users`; permissions are code — constants plus a role-to-permissions map in Domain** | Three designs were costed. Full RBAC tables (`Roles`, `Permissions`, `UserRoles`, `RolePermissions`) means four tables, seed data, two joins on every check and eventually an admin screen to edit what never changes — all to tell two roles apart. ASP.NET Core Identity brings its own `DbContext`, its own user entity and its own migrations, so the rich `User` here is either replaced or duplicated and the domain rules move into a library this project does not own. A column plus a code map costs neither. **Permissions in code live in `git` history: reviewed, diffed and tested. A permissions table editable in production is the shortest path to a silent privilege escalation** | Changing what a role may do needs a deployment, not an `UPDATE`. One user cannot hold two roles. A third role needs a migration, because `CK_User_RoleValue` fixes the range. Adding a *capability* to an existing role does not: one constant, one line in the map, one attribute on the endpoint. The Domain carries permission strings that ASP.NET Core consumes as policy names — plain `string`, no dependency, so the zero-dependency rule (§4) holds |

---

## 6. Application Layer Structure (Vertical Slices)

Each feature owns a folder containing everything it needs. The slice follows the **use case**, not the database table — `CreateNoteCommand` lives under `Notes/` even though it writes to `Items` through `IItemRepository`.

```
StudyHub.Application/
├── Common/
│   ├── Interfaces/   → ICourseRepository, IItemRepository, IUserRepository,
│   │                   IUnitOfWork, IPasswordHasher, ICurrentUserService
│   │                   IRefreshTokenRepository, ITokenService
│   │                   AccessToken, RefreshTokenResult — the records
│   │                   ITokenService returns
│   │                   IAiService                                      (M8)
│   ├── Behaviors/    → ValidationBehavior.cs
│   ├── Validation/   → PasswordRuleExtensions.cs — the password policy,
│   │                   shared by registration and administrator seeding
│   └── Exceptions/   → one file per exception type:
│                       ConflictException.cs, NotFoundException.cs,
│                       ForbiddenException.cs
│                       InvalidCredentialsException.cs
│                       QuotaExceededException.cs,
│                       ExternalServiceException.cs                     (M8)
│
├── Auth/Commands/{Login, Refresh, Logout}/
├── Auth/Queries/GetCurrentUser/                                        (M7)
├── Courses/Commands/{CreateCourse, DeleteCourse}/
├── Courses/Commands/UpdateCourse/                                      (M7)
├── Courses/Queries/GetCourses/                                         (M7)
├── Notes/Commands/CreateNote/
├── Notes/Commands/ExtractTaskSuggestions/                              (M8)
├── Tasks/Commands/CreateTask/
├── Tasks/Commands/{UpdateTaskStatus, UpdateTaskSchedule}/              (M7)
├── Items/Commands/DeleteItem/
├── Items/Commands/UpdateItemContent/                                   (M7)
├── Items/Queries/{GetItem, GetItemTree, GetRootItems}/                 (M7)
├── Dashboard/Queries/GetDashboard/                                     (M9)
└── Users/Commands/{RegisterUser, DeactivateUser, SeedAdministrator}/
```

**One exception type per file.** Three types sharing one file compiles and runs, but the file name then describes only one of its contents, and the comments inside end up naming files that do not exist.

**Request flow, end to end:**
1. A controller action builds a Command and calls `mediator.Send(...)`.
2. `ValidationBehavior` runs the matching validator. Invalid input throws before the handler exists.
3. The handler resolves ownership, calls a domain factory or mutator, and saves through `IUnitOfWork`.
4. Any exception is translated to a status code by `GlobalExceptionHandler`.

**Defence in depth is deliberate.** Ownership of a parent item is checked twice — once in the handler (to return a precise 403) and once inside `Item.Initialize` (because the entity trusts no caller). Depth follows the same shape: the handler asks `parent.IsAtMaxDepth` and returns 409, and `Item.Initialize` asks the same member (ADR-30). Course ownership is checked in the handler only, since the entity receives a `Guid` rather than an object; that asymmetry is known and accepted.

**Commands and queries have different fetch rules** *(M7)*. A query projects directly into its DTO with `Select` and never materializes an entity. A command loads the whole entity, because it is about to call a method on it. Since repositories do not expose `IQueryable` (ADR-13), the projection has to run inside Infrastructure; where exactly is open (§13).

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
| Role | int | required, database default `0`; `CK_User_RoleValue` restricts it to `0`–`1` |
| CreatedAt / UpdatedAt | timestamptz | UpdatedAt nullable |

From M8, concurrent AI requests write `TokensUsedThisMonth`; how an increment survives that is open (§13, §14.4).

`Role` is an integer, not text, for the reason `Kind` is: renaming an enum member must not invalidate stored rows. It carries no index — the column has two distinct values and no query filters on it, so an index here would be write cost with no reader (A13). The check constraint is not optional: presence is not validity, and without it the database accepts any integer the application never wrote (A11).

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

**Concurrency token** (ADR-27): PostgreSQL's `xmin` system column, mapped as a shadow property. The migration creates nothing for it; a generated migration that adds an `xmin` column is wrong and must not be applied — read it first (`CODING_STANDARDS.md` §7).

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
| POST | `/api/auth/login` | anonymous | 200 + token pair |
| POST | `/api/auth/refresh` | anonymous¹ | 200 + new token pair |
| POST | `/api/auth/logout` | user | 204, in every case (§9.3) |
| POST | `/api/courses` | user | 201 + courseId |
| DELETE | `/api/courses/{id}` | user | 204 |
| POST | `/api/notes` | user | 201 + noteId |
| POST | `/api/tasks` | user | 201 + taskId |
| DELETE | `/api/items/{id}` | user | 204 |
| PATCH | `/api/admin/users/{id}/deactivate` | `users:deactivate` permission² | 204 |

Deletion has one route for both notes and tasks, because the operation does not distinguish them.

**"user" means any valid access token, and it is the default.** A fallback authorization policy requires an authenticated caller on every endpoint; only the three `anonymous` routes above, and the OpenAPI document, opt out with `[AllowAnonymous]` (§9).

² UC-09. The first endpoint guarded by a permission rather than by ownership, and the proof that the whole authorization path works end to end: a normal user's token gets 403, an administrator's gets 204, and `psql` shows `IsActive = false`. Deactivating an already-inactive account is still 204 — `User.Deactivate` is tolerant of repetition, and an administrator should not have to check first. There is deliberately no *reactivate* and no *list users* endpoint yet (§12).

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

`expiresIn` is the access token's remaining lifetime in **seconds**, so the client never has to parse the JWT to schedule a refresh. No user profile fields are included (ADR-21); a client needing them calls `GET /api/auth/me` *(M7)*.

### Planned — M7
`GET /api/auth/me`, `GET /api/courses`, `GET /api/courses/{id}/tree`, `GET /api/items`, `GET /api/items/{id}`, `GET /api/items/{id}/tree`, `PATCH /api/items/{id}`, `PATCH /api/tasks/{id}/status`, `PATCH /api/tasks/{id}/schedule`, `PUT /api/courses/{id}`

### Planned — M8
| Method | Route | Auth | Returns |
|---|---|---|---|
| POST | `/api/notes/{id}/extract-tasks` | user | 200 + suggestions; nothing is created (ADR-28) |

Approval uses the existing `POST /api/tasks`, with `parentItemId` set to the note.

### Planned — M9
`GET /api/dashboard` — contents defined in §15.4.

### Error contract
All errors return RFC 9457 `ProblemDetails`:

| Exception | Status |
|---|---|
| `ValidationException` | 400 + `errors` grouped by field |
| — (missing or invalid access token) | 401, from the authorization middleware, empty body |
| — (valid token, permission missing) | 403, from the authorization middleware, empty body |
| `InvalidCredentialsException` | 401 — login and refresh only: the credential itself was rejected |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 — duplicate email, parent at maximum depth, lost concurrency race |
| `QuotaExceededException` | 429 |
| `ExternalServiceException` | 502 |
| anything else | 500, with no internal detail leaked |

**401 has two sources, and only one of them is a handler.** The JWT middleware rejects a missing or invalid access token before MediatR runs, so that 401 has no exception type. The two anonymous authentication handlers — login and refresh — reject a presented credential (a password, a refresh token) by throwing `InvalidCredentialsException`. No other handler throws it: a handler behind `[Authorize]` that wants to reject on identity throws `ForbiddenException` and gets 403 — the caller was authenticated, just not entitled. **Cost**: one more exception type, which a careless handler could misuse for an authorization failure that should be 403.

**400 is about shape; 403, 404 and 409 are about state.** A validator rejects a request that is malformed on its own terms — 400. A handler rejects a well-formed request that the current data refuses: the parent is missing (404), not yours (403), or full (409). A handler therefore never throws `ValidationException`.

**Two different sources produce 400.** Model binding inside `[ApiController]` rejects malformed JSON *before* MediatR runs; its response carries a `traceId`. A `ValidationException` from a validator does not. Useful when diagnosing.

---

## 9. Security Model

### Current state

Identity comes from the `sub` claim of an access token that `JwtBearer` has already validated; `CurrentUserService` reads that claim and nothing else. The `X-User-Id` header that stood in for authentication until M6 is gone.

**Every endpoint requires a valid access token unless it opts out.** A fallback authorization policy requires an authenticated user; `register`, `login`, `refresh`, and the OpenAPI document carry `[AllowAnonymous]`. Protection is the default and exposure is the declaration — a new controller that forgets an attribute is closed, not open. Without the fallback, an anonymous request reached the handler and failed there as a 403, and the 404-versus-403 difference told a stranger which ids exist. **Cost**: the 401 has an empty body rather than `ProblemDetails`, and an anonymous request to a route that does not exist gets 401 instead of 404.

Replacing the header touched no line in Application: handlers still ask `ICurrentUserService`, and only its API-layer implementation changed.

### 9.1 Authentication decisions (new in v3.0)

Three of these were open questions in v2.0; the other three were never written down. None of them can stay open — each blocks a specific file in M6 — so all six are decided here.

| # | Decision | Value | Cost |
|---|---|---|---|
| 1 | Access token lifetime | **15 minutes** | A revoked user stays usable for up to 15 minutes |
| 2 | Refresh token lifetime | **7 days**, renewed in full on every rotation | An active session never expires. An absolute session cap is deferred (§12) |
| 3 | Concurrent sessions | **Unlimited; one chain per device** (ADR-19) | Reuse detection logs the user out of every device |
| 4 | Refresh token transport | **Response body** (ADR-20) | Client-side storage is the client's problem |
| 5 | JWT claims | **`sub`, `jti`, `role`**, plus registered claims only: `iss`, `aud`, `exp`, `iat`, `nbf` (ADR-21) | Name and email require a database read. A role change lags by up to 15 minutes, demotion included |
| 6 | Logout scope | **The presented refresh token only** | No "sign out everywhere"; deferred (§12) |

**The signing key** lives in `dotnet user-secrets`, never in `appsettings.json`. HS256 requires at least 32 bytes; a shorter key throws at *runtime*, not at build — so the key length is validated at startup.

**Clock skew is set explicitly to 30 seconds**. `JwtBearer` tolerates five minutes by default, which silently stretches decision 1 from 15 minutes to 20.

**A trap worth writing down before it happens.** `CurrentUserService` will read the user id from the `ClaimsPrincipal`. Whether it appears as `sub` or as `ClaimTypes.NameIdentifier` depends on which handler validated the token and whether inbound claim mapping was cleared. Both spellings compile, and the wrong one returns `null` at runtime, not an error. **Verify by enumerating the actual claims once and reading them** — this is verification rule 3 (§10) applied before the fact rather than after it.

### 9.2 Login rules

1. **One message for every failure.** "No such user" and "wrong password" return **401 with identical text**. Distinguishing them hands an attacker an account-enumeration tool.
2. **Check `IsActive`** before issuing any token — and return the same 401 text, for the same reason.
3. **Always call `Verify`**, even when no user was found, against a fixed dummy hash. Without it, response time separates the two cases and leaks exactly what rule 1 protects.
4. **Never log the password**, not even on failure.

### 9.3 Refresh rules

Hash the incoming token → look it up → check `IsActive(utcNow)` → issue a new pair → `oldToken.Revoke(utcNow, newToken.Id)` → save. **One `SaveChangesAsync` is already one transaction in EF Core; no explicit transaction is needed and none should be added.**

**Every refusal is 401 through `InvalidCredentialsException`, with one text** — an unknown token, an expired one, a revoked one, or a deactivated user. **Inactive has two causes, with two responses**: a token revoked by rotation triggers reuse detection (below); an expired one returns 401 and nothing else — expiry is not theft.

**Refresh refuses a deactivated user.** Without this check, deactivation would never end a session: §9.4 accepts that an access token outlives a deactivation by up to 15 minutes, which is only true if the next refresh fails.

**Two parallel refreshes with one token** (ADR-27). A transaction makes a save atomic; it does not stop two requests from reading the token as active before either writes. The concurrency token does: exactly one rotation succeeds, the other fails at save, and `UnitOfWork` translates `DbUpdateConcurrencyException` into `ConflictException` (409), the same way it already translates a unique violation. The losing client must use the pair the winning request received; presenting the old token again is a reuse, and is treated as one.

**Reuse detection**: a token that is presented after being revoked **by rotation** — `ReplacedByTokenId` is set — means an old copy of a live chain is in someone else's hands: the chain was stolen. The response is to revoke every active token the user has, on every device. `ReplacedByTokenId` exists for exactly this. **The revocations are saved before the 401 is returned** — a handler that throws first discards them, and the stolen chain stays alive.

**A token revoked without a successor is a plain 401, not a theft.** That is a token ended by logout, or by an earlier mass revocation. Its chain is already dead, so there is nothing left to steal from it. Treating it as theft would have two costs: anyone holding any old token could log the user out of every device, as often as they like, and a refresh still in flight when the user taps logout would sign that user out of their other devices. **Cost**: a stolen token whose owner has since logged out no longer raises the alarm — acceptable, because it can no longer open a session either.

**Logout** revokes the presented refresh token only if it belongs to the caller (`sub`) and is still active, and returns 204 in every case — a foreign, unknown, or already-revoked token included, as RFC 7009 does for token revocation. Logout never runs reuse detection: a revoked token arriving there is a double tap, not a theft.

### 9.4 Known accepted risks

- **Registration reveals whether an email is already in use.** This is the one that undermines the others: §9.2 builds careful anti-enumeration into login, and `POST /api/auth/register` gives the same information away on an easier endpoint. Closing it properly requires an email-verification flow (always return 202, send a mail either way), which is out of scope. **Recorded here rather than fixed, so that login's protection is not mistaken for complete protection.** Rate limiting (M10) reduces how fast the list can be harvested, not whether it can be.
- **An access token outlives a deactivation, a logout, or a role change** by up to its lifetime. This is the price of stateless auth (ADR-06). Checking the database on every request would remove the reason JWT was chosen. **Demotion is the worst case of the three**: a user who has just lost administrative rights keeps them until their access token expires.
- **Course ownership is guarded in one layer only** (see §6).
- **Password length is unbounded.** Safe because `EnhancedHashPassword` pre-hashes the input, removing BCrypt's 72-byte truncation. Reverting to the standard variant would silently reintroduce it.
- **The first administrator comes from configuration, and its password sits in `user-secrets` until someone removes it.** At startup, `AdminSeed:Email` names the account. If none exists, `AdminSeed:FullName` and `AdminSeed:Password` create it under the registration password policy, and `PromoteToAdmin` runs in the same save. An existing account is only promoted — its password is never overwritten, so stale configuration cannot reset a password its owner has since changed. The startup log asks for the password to be removed once it has served. Without `AdminSeed:Email` nothing runs, and startup does not touch the database. **Cost**: until it is removed, an administrator password lives in plain text on the machine — the same exposure as the JWT key and the connection string beside it. This replaced the direct `UPDATE` used before M6, which bypassed `PromoteToAdmin` and left `UpdatedAt` unstamped.
- **An administrator can deactivate any account, their own and the last administrator's included.** Nothing stops it, and there is no reactivation (§12). Recovery is to name a different, active account in `AdminSeed:Email` and restart. A guard would need a count of active administrators on every call — a rule for a situation that has one operator today.

### 9.5 Authorization model

Two mechanisms, and they are not interchangeable:

- **Ownership** guards what a user does with their own rows. The handler compares `UserId` against the caller and throws `ForbiddenException`. This exists already and does not change.
- **Permission** guards what a role may do to *other people's* rows. An ASP.NET Core policy checks it on the endpoint, before the handler runs.

The path of an authorized request: login issues the `role` claim → `[Authorize(Policy = ...)]` names a permission on the endpoint → the policy parses the claim back into `UserRole` and asks the same Domain map that the entity asks. **One map, two callers**, so the API layer keeps no list of its own that could drift. That is the reason the map lives in Domain and not in the API layer: a map in the API is invisible to Application, and a handler cannot ask a question it cannot see.

**Policies are registered from the constants, not from a list.** At startup, one policy is added for every `const` in `Permissions`, named by the permission itself and found by reflection. Adding a capability therefore stays at the three edits ADR-31 promises — a constant, a line in the map, an attribute — with no fourth place to forget; and a forgotten policy would not fail safe, it would throw on the first request. The `role` claim is accepted by its exact enum name only: `Enum.TryParse` also accepts `"1"` and `"admin"`, which the token never carries.

**A normal user holds no permission at all** — their rights over their own rows come from ownership, not from the role, so the `User` entry in the map is an empty set rather than an oversight. A role value the code does not recognize resolves to that same empty set instead of throwing: a row written by a future version must mean *no privilege*, never a crashed request. This is the `D4` shape — an unrecognized value out of storage is refused, not fatal.

**Deliberately absent**: demotion, a second role per user, a per-user exception to a role, any way to edit permissions in a running system, and an audit trail of administrative actions. Each is costed in §12.

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

**Extended in v3.0**: every validator containing a **conditional or cross-field rule** ships with a test. Plain `NotEmpty` and `MaximumLength` do not need one. The rule that a nested item may not carry any `CourseId` currently lives in the validator alone — the entity ignores the value silently rather than rejecting it — so deleting that rule would break the API without turning a single test red.

**Assert on what did not happen too.** A failure test verifies that the exception was thrown *and* that `SaveChangesAsync` was never called.

**A concurrency rule needs a concurrent proof.** A sequential test passes whether or not the concurrency token exists. Until integration tests arrive (M10), the proof is the `UPDATE` statement EF Core logs for a rotation: its `WHERE` clause must carry the `xmin` condition.

**Not automated yet**: EF Core queries and HTTP round-trips need integration testing against a containerized database. Scheduled for M10. Until then, manual verification through `StudyHub.API.http` and direct `psql` inspection is the substitute — and `psql` is the stronger of the two, because it is the only view that does not pass through EF's soft-delete filter.

### Three verification rules learned the hard way
1. **A successful build proves nothing.** Every change is verified by its own specific effect: an HTTP status code, a row in the database, a passing test.
2. **A constraint is proven by trying to break it *and* by inserting a row that should pass.** One without the other is half an answer. And read *which* constraint the error names — a malformed statement produces a red error and no inserted row, which is exactly what a working constraint produces.
3. **A change with no externally observable behaviour is verified by reading the file.** When no code path calls the changed method yet, the build passes, the tests pass, and the endpoints behave identically whether or not the change was ever applied. Nothing else will catch it.

> **A corollary added in v3.0.** Rule 3 is a last resort, not a first choice. Before accepting "read the file" as the proof, ask whether the change could be given observable behaviour *today* by a unit test. Two proofs in the M6 checklist — that a corrupt stored hash returns `false` instead of throwing (`D4`), and that two passwords sharing 72 bytes do not verify each other (`D5`) — were deferred to a milestone they never depended on, because a proof was assumed to mean an HTTP status code. Both are unit tests over `IPasswordHasher` and need no endpoint, no database, and no login flow; they were moved to M5.1 and are green. The HTTP checks stay in M6 as end-to-end confirmation that login turns a `false` into 401 — they are simply no longer the only proof.

---

## 11. Roadmap

| # | Scope | Status |
|---|---|---|
| M1 | PRD & requirements | ✅ |
| M2 | Solution & Clean Architecture layers | ✅ |
| M3 | Domain entities, DbContext, relational integrity | ✅ |
| M4 | Application foundation: MediatR, FluentValidation, `ValidationBehavior`, test projects | ✅ |
| **M5** | **Hardening & the content tree**: global exception handling, domain and security fixes, `Email` value object, base-class split, clean schema with six check constraints, the `Items` TPH restructure, create/delete handlers, controllers | ✅ |
| **M5.1** | **Cleanup**: exception file split, `Infrastructure.Tests` with the `D4` and `D5` proofs, `Email.FromPersisted`, UTC rule, depth refused with 409 (ADR-30) | ✅ |
| **M5.2** | **Role foundation (UC-09, ADR-31)**: `UserRole`, the permission map in Domain, the `Role` column with its check constraint, `PromoteToAdmin` and `Can`. No endpoint, no policy, no claim — those need authentication to mean anything | ✅ |
| **M6** | **Authentication & authorization (UC-01, UC-02, UC-09)**: login, JWT issuance with the `role` claim (ADR-29), refresh rotation with a concurrency token (ADR-27), reuse detection, removal of the `X-User-Id` bypass, permission policies, the first administrator endpoint, and seeding the first administrator account | ✅ |
| M7 | Content completion (UC-03, UC-04, UC-05): DTOs, read queries, update handlers, pagination, quota to configuration, **`DeletedAt` + `DeletedBatchId` migration (ADR-25)** | Pending |
| M8 | AI integration & quota enforcement (UC-06, UC-07): suggestion endpoint with user approval (ADR-28), `ConsumeTokens` split, counter concurrency (§13), failure model, extraction depth guard | Pending |
| M9 | Dashboard aggregation (UC-08) as defined in §15.4 | Pending |
| M10 | Integration tests, rate limiting, refresh-token cleanup, API containerization | Pending |

> **Renumbering note.** v1.1 listed M5 as authentication and M6 as content management. What was actually built in that slot was hardening plus the content tree. The roadmap has been rewritten to match what happened rather than what was planned — which also keeps every `(M5)` tag in the troubleshooting log accurate. M5.1 is numbered as a point release for the same reason: it is cleanup of M5's output, not a milestone of its own, and giving it a whole number would shift every tag after it. **M5.2 is a decimal for the same reason and one more**: it is a schema and domain change only, kept out of M6 so that a migration is not mixed into the heaviest security milestone. A migration is easier to read, and easier to roll back, on its own.

---

## 12. Deliberately Deferred

Each of these is a decision, not an oversight.

| Item | Why deferred | What it will cost later |
|---|---|---|
| **Node moving** | No use case requires it; it is the single largest source of complexity in a free-nesting tree | Cycle detection, subtree depth recalculation, `CourseId` rewrite down every descendant. Invalidates ADR-09 and ADR-10 |
| **Restore after delete** | Not requested — but the schema change that makes it *possible* is scheduled in M7, because it is cheap only while the database is empty | Currently impossible: after a cascade delete nothing distinguishes a child deleted deliberately from one deleted by cascade. ADR-25 fixes the schema; the restore handler itself remains deferred |
| **Absolute session lifetime cap** | Rotation with reuse detection already limits the damage of a stolen token | A `SessionStartedAt` column or a walk back up the `ReplacedByTokenId` chain, plus a forced re-login the user did not ask for |
| **"Sign out of all devices"** | Logout is per-device (§9.1); the all-device revocation path is built anyway, for reuse detection (M6) | A query for the user's active tokens — deliberately left out of `IRefreshTokenRepository` until something needs it — plus an endpoint |
| **Refresh token cleanup** | The table only grows with sessions, and there are none yet | A background job or a scheduled `DELETE` for rows expired more than N days ago. M10 |
| **Changing an item's `Kind`** | An object cannot change its CLR type, which is what the discriminator follows (rule 3.2.8) | Create-new-and-delete-old semantics, a new id, and the children: today they are deleted with the old item, because moving them is forbidden (rule 7) |
| **Cursor pagination** | Offset is adequate at per-user scale (ADR-22) | A stable sort key, an opaque cursor format, and the loss of "jump to page 7" |
| **Grace window for rotation** | Rotation is strict: one save wins (ADR-27). A window exists to absorb lost responses on unstable networks, and there is no real client yet | A few seconds during which a just-revoked token still issues a new pair. Reuse detection must then tolerate a branching chain, and a stolen token works inside the window |
| **Concurrency control on content edits** | One user editing their own data; two tabs at most | Last write wins silently today. The fix is an `ETag` / `If-Match` contract on every update endpoint |
| **Many-to-many between tasks and notes** | Parenthood covers the known cases | A join table and a second relationship model |
| **Integration tests** | Domain and Application carry the business logic | A containerized test database and a slower suite |
| **API containerization** | `dotnet run` is sufficient locally | A Dockerfile and compose changes |
| **Rate limiting** | No public exposure yet | Middleware plus a policy per endpoint group. Also the only practical mitigation for §9.4's enumeration risk |
| **Email verification on registration** | Out of scope for a learning project | The only complete fix for account enumeration: always return 202, and send a different mail depending on whether the address was already registered |
| **Demotion from administrator** | No use case asks for it, and one entry point to privilege with no exit is the easiest shape to audit | A `Demote` method, its tests, and an endpoint. Cheap — it is withheld because it is unneeded, not because it is hard |
| **More than one role per user** | Two roles that do not overlap are fully described by one column | The column becomes a join table, every permission check becomes a union over roles, and ADR-31's main saving disappears |
| **A permission granted to one user, outside their role** | It would break "permissions live in code": the exception would have to live in the database | A `UserPermissions` table, and a permission check that reads it on every request |
| **Editing permissions at runtime** | The shortest path to a silent privilege escalation (ADR-31) | Not planned to return |
| **An audit trail of administrative actions** | There is exactly one administrative action, and it leaves its own trace: `IsActive = false` with a stamped `UpdatedAt` | A table, an interceptor or an explicit write per action, and a retention rule. Add it with the first *destructive* administrative action, not before |
| **Listing users for an administrator** | UC-09 needs a user id, and `psql` supplies it. An endpoint built before its consumer is the A16 pattern | A read query, a DTO, pagination, a `users:view` permission, and the decision of which fields an administrator may see |
| **Reactivating a deactivated account** | UC-09 asks only to stop an account; `User` has no `Activate` | A domain method, its tests, a second permission, and an endpoint. Until then a mistaken deactivation is undone with a direct `UPDATE` |
| **`Content` length limit** | It is the note body; an arbitrary cap would be guesswork | A migration if a limit is ever chosen |

---

## 13. Open Questions

Closed since v2.0: API style is **Controllers** (ADR-14); DTO mapping is **manual extension methods** (ADR-17); repository granularity is **one per aggregate**. The authentication questions are answered in §9.1; the registration message is an accepted risk (§9.4); `DeletedAt` is scheduled (ADR-25); pagination, tree shape, and concurrency are settled in §14; the approval flow and quota ordering in §15.

Still open. None blocks M6; each names the milestone that needs the answer:

1. **AI provider and its abstraction** (M8) — which provider (Gemini is the current candidate, named in `ARCHITECTURE.md` §4.6), and is a single `IAiService` enough, or should the prompt and response contract be modelled explicitly?
2. **Estimated cost per AI operation** (M8, §15.1) — a fixed reserve per operation type, or a multiple of the input length? The second is more accurate and needs a tokenizer.
3. **Should `GET /api/courses` return item counts per course?** (M7) Useful for a dashboard; a possible N+1 if written carelessly.
4. **Where does a read query project?** (M7) Repositories lose `IQueryable` (ADR-13), yet queries must project with `Select` (§6). Either repository methods return DTOs — simplest, but one interface then serves both writing and reading — or queries get their own read-side interface — cleaner CQRS, one more interface.
5. **How does the token counter survive concurrent extractions?** (M8, §14.4) It must neither lose an increment nor fail a paid call. Either an atomic `UPDATE … SET "TokensUsedThisMonth" = "TokensUsedThisMonth" + n` — never conflicts, but bypasses `RecordTokenUsage` and needs its own transaction with the log insert — or optimistic concurrency with a retry — keeps the domain method, but needs a reload-and-retry path in Infrastructure.
6. **What does "recent courses" mean?** (M9, §15.4) As defined, the most recently created or edited. Ordering by the latest item activity is truer to the word, at the cost of one aggregate per course.
7. **Reading another user's item: 404 or 403?** (M7) 403 matches the write path; 404 hides that the item exists. The write path already reveals existence through its 403, so hiding it on reads alone buys little — but the choice must be written before the first query handler.

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

**Why this is not pedantry.** `DueDate` maps to a `timestamptz` column, and Npgsql writes only a `DateTime` whose `Kind` is `Utc`. A JSON value without an offset parses to `Unspecified`; a value with a non-`Z` offset parses to `Local`, because the JSON reader converts it to server time. Npgsql refuses both. So the choice is not between strict and lenient — it is between a 400 that explains itself and a 500 that does not. This is a live defect today, in both formats: no request in `StudyHub.API.http` has ever sent a `dueDate`, which is the only reason it has not been hit.

**Rules:**
- Validators reject any `DateTime` whose `Kind` is not `Utc` — every validator that carries one, M7's schedule update included.
- Entities read `DateTime.UtcNow` and never `DateTime.Now`.
- A due date in the past is **valid**. Recording a task that was due yesterday is a normal thing to do.
- Timezone presentation is the client's job. The API neither stores nor asks for the user's timezone.

**Cost, stated plainly**: a client sending an unambiguous `+03:00` offset gets rejected even though the value could have been converted. Accepting it would route the value through the *server's* local timezone and back — correct, but invisible and hard to verify. One rejected format is cheaper than one invisible conversion.

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

**Why a tree is exempt.** Splitting a subtree across pages returns children whose parents are on another page — the client cannot assemble anything from that. So a subtree is returned whole. It is bounded in depth — five levels — but not in breadth, which is the risk below.

**The risk that comes with the exemption**: a course containing tens of thousands of items produces one very large response. No cap exists today. If it becomes real, the answer is a documented limit and a 400, not silent truncation — a truncated tree is indistinguishable from a complete one.

### 14.3 Logging

**Never logged, under any circumstance:** passwords (plain or hashed), raw refresh tokens, refresh token hashes, JWT signing keys, connection strings.

**Log the user id, not the email.** The id identifies the account for debugging without putting an address in a log file that outlives its usefulness.

**Expected exceptions log at Warning with the type name only. Unexpected ones log at Error with the full exception.** A 404 is not an incident; a 500 is. Logging them at the same level means neither can be found.

### 14.4 Concurrency

**A read-modify-write on a row that parallel requests can reach needs a concurrency check.** EF Core sends only the columns it changed, but computes their values from what it loaded — so without a check, the second of two parallel saves silently overwrites the first. One `SaveChangesAsync` is one transaction, which makes each save atomic — it does not stop two requests from reading the same row before either writes.

| Row | The race | Decision |
|---|---|---|
| A refresh token during rotation (M6) | two refreshes with one token fork the chain | `xmin` concurrency token; the loser gets 409 (ADR-27) |
| `Users.TokensUsedThisMonth` (M8) | two extractions lose one increment | must neither lose it nor fail a paid call; mechanism open (§13) |
| Courses and items | two tabs edit the same row | last write wins; deferred (§12) |

**Choosing the loser's fate.** A conflict the client can resolve becomes a 409. A conflict on the record of something already paid for is resolved on the server and never surfaced.

---

## 15. AI Integration Model

`UC-06` and `UC-07` are two sentences, and behind them is the milestone most likely to break — because the ordering problem below is not visible from the use case text at all.

**The flow** *(M8, ADR-28)*: one paid request, then approvals that cost nothing.

```
POST /api/notes/{id}/extract-tasks
  1. load the note                          → 404 / 403
  2. note.IsAtMaxDepth                      → 409      (§15.2)
  3. user.ResetQuotaIfNeeded(utcNow)
  4. user.HasQuotaFor(estimate) is false    → 429      (§15.1)
  5. call the provider; it fails            → 502      (§15.3)
  6. user.RecordTokenUsage(actual) + AiUsageLog, one save
  7. return the suggestions; nothing else is written

then, for each suggestion the user approves:
POST /api/tasks  { "title": "…", "parentItemId": "<note id>" }
```

A suggestion carries a title and optional content, and no due date: "by Friday" cannot become a UTC moment without the user's timezone, which the API neither stores nor asks for (§14.1). The user sets the date while approving.

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

**Cost accepted**: the counter may overshoot the quota by the gap between estimate and actual — once per request that passes the pre-check, and parallel requests all pass it before any of them records. The next `HasQuotaFor` after them sees the real figure and refuses. A hard cap that could discard paid work is worse than a soft cap that cannot. Recording itself must not lose an increment under concurrency (§14.4).

**Also**: `ResetQuotaIfNeeded(utcNow)` runs before the pre-check, or a user entering a new month is refused against last month's counter.

### 15.2 Depth and extraction

Extracted tasks are children of their source note (`UC-06`). A child of a depth-4 note would be depth 5, which `Item.Initialize` rejects and `CK_Item_Depth` rejects again.

**Decision (ADR-26)**: refuse the extraction **before calling the provider** when `note.IsAtMaxDepth` is true. Return 409 with a message naming the reason — the same status and the same domain query as creating any child under a full parent (ADR-30). Under the approval flow, this also spares the user from paying for suggestions that could never be approved.

The alternative — creating the tasks as siblings instead of children — was rejected: it breaks provenance silently, and provenance-as-parenthood is the entire justification for `UC-06`'s shape. A refusal the user can read beats a result they cannot trust.

### 15.3 Failure model

Nothing originating outside the process reaches the client as a 500.

| Failure | Exception | Status |
|---|---|---|
| Quota insufficient before the call | `QuotaExceededException` | 429 |
| Provider unreachable, timed out, or returned an error | `ExternalServiceException` | 502 |
| Provider returned a response that could not be parsed | `ExternalServiceException` | 502 |
| Source note at maximum depth | `ConflictException` | 409 |

**A billed call is recorded even when its result is unusable.** A response that arrived but could not be parsed has still been paid for: its usage is recorded and saved first, and only then does the client get the 502. A call that produced no response has no token count to record; that loss is accepted.

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

**Recent courses** — at most 5, ordered by `UpdatedAt` descending, falling back to `CreatedAt`. Note what this measures: a course's `UpdatedAt` changes only when its own title or description does — adding an item under it touches nothing on the course. The list therefore means "recently created or edited", not "recently used" (§13).

**The N+1 requirement is verified, not asserted.** Enable EF Core SQL logging, call the endpoint against a user with many courses and items, and count the statements. The count must be a small fixed number and must not grow with the data. "It looked fast" is not the proof.


