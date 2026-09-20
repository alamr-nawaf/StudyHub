# StudyHub — Architecture & Technology Guide

> This document explains **how** StudyHub is built and **why**, so anyone opening this repository — including "future you" in six months — can understand it without re-reading every commit.
>
> For **what** the system does (use cases, business rules, schema, roadmap), see [`Requirements.md`](Requirements.md).
> For **code style rules**, see [`CODING_STANDARDS.md`](CODING_STANDARDS.md).
> For **problems hit and how they were fixed**, see [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md).
>
> **v3.0** — aligned with `Requirements.md` v3.0. It follows the same reading convention: a statement tagged with a milestone — *(M6)*, *(M7)* — describes something not built yet; an untagged statement describes code that exists today. (v2.0 was the rewrite after the M5 restructure, when the `Notes` and `Tasks` tables of v1 disappeared.)

---

## 1. What Is This Project

StudyHub is a backend API for managing courses, notes, and tasks, with AI summaries of notes and AI-suggested tasks extracted from them — suggestions the user approves before anything is written.

It is a **personal learning project**. The goal is to practise professional .NET backend patterns correctly — Clean Architecture, CQRS, rich domain models, real schema constraints, and a disciplined verification habit — not to ship the fastest possible MVP.

Every technology choice below was made deliberately, usually after weighing at least one alternative, because the point is to understand *why* a pattern exists rather than to copy it.

---

## 2. Solution Structure

```
StudyHub/
├── StudyHub.Domain             → Business entities & rules. Zero external dependencies.
├── StudyHub.Application        → Use cases (Commands/Queries + Handlers). Depends only on Domain.
├── StudyHub.Infrastructure     → EF Core, PostgreSQL, security implementations.
├── StudyHub.API                → ASP.NET Core host. Thin — no business logic.
├── StudyHub.Domain.Tests       → Unit tests for entity rules.
├── StudyHub.Application.Tests  → Unit tests for handlers, dependencies mocked.
├── StudyHub.Infrastructure.Tests → Unit tests for infrastructure code that needs no database.
└── docs/                       → Requirements, architecture, standards, troubleshooting, ERD.
```

**The dependency rule**: arrows only point inward.

```
API → Infrastructure → Application → Domain
```

Domain never knows that EF Core, ASP.NET Core, or PostgreSQL exist. Application knows none of them either — it declares interfaces and lets Infrastructure supply the implementations.

**This rule is enforced by the compiler, not by discipline.** A repository implementation once landed in `StudyHub.Application` by mistake; it failed to build immediately, because that project has no reference to EF Core and therefore no `DbContext` type. The architecture caught the error rather than merely discouraging it.

---

## 3. Technology Stack & Why Each One Was Chosen

### Domain — plain C# / .NET 10

Entities with private setters, private constructors, and static factory methods that enforce invariants at creation time.

`Authorization/` holds the permission model: `Permissions` (string constants, one per capability) and `RolePermissions` (the role-to-capabilities map). Both are plain C# with no dependency, so the zero-dependency rule holds while ASP.NET Core still consumes the constants as policy names. **The map lives here, not in the API layer, so that one map has two callers** — the endpoint policy asks it from the `role` claim, and `User.Can` asks it from the entity. A map in the API layer would be invisible to Application, and a handler cannot ask a question it cannot see. The policy wiring lives in `StudyHub.API/Authorization/`; see Requirements §9.5 and ADR-31.

**Structure:**

```
BaseEntity            → Id, CreatedAt
└── AuditableEntity   → + UpdatedAt
    ├── User
    ├── Course
    ├── RefreshToken
    └── Item (abstract)
        ├── Note
        └── TaskItem

BaseEntity
└── AiUsageLog        → inherits the plain base: written once, never modified
```

The base class is split because a usage log is not an editable entity. Giving it `UpdatedAt` would create a column guaranteed to stay null forever. **Inheritance follows lifecycle, not convenience.**

**Value object**: `Email` owns normalization and format checking. It exists because that logic was previously written twice — in `User.Create` and in `UserRepository` — and a divergence between the two would let a duplicate account walk straight past a unique index. The EF Core converter rebuilds it through `Email.FromPersisted`, which does not validate — a converter is a mapping, not a gate, and one corrupt row must not turn every read of that user into a 500 (Requirements §3.3).

**Clock handling**: methods whose behaviour depends on time (`RefreshToken.IsActive`, `RefreshToken.Revoke`) take `utcNow` as a parameter, so they are testable with no clock abstraction. Everything else reads `DateTime.UtcNow` directly. A *period* is a separate question: which month an instant belongs to is answered by `BusinessCalendar` in Application, which converts a UTC instant into the configured business time zone (`BusinessTime:TimeZoneId`, Asia/Riyadh) and back, so a month begins at Riyadh midnight while everything stored and exchanged stays UTC (ADR-40). This is a deliberate limit: a full `TimeProvider` injection across every entity would touch every call site and every test to buy testability in places nobody tests.

**Why zero dependencies**: persistence ignorance. The business rules can be unit-tested with no infrastructure at all, and would survive a change of database or framework.

### Architecture — Clean Architecture, 4 layers

**Chosen over a simpler 3-layer approach** to separate *what the business does* from *how it is technically implemented*. The payoff has been concrete twice in this project: adding foreign keys after the fact, and swapping how the connection string is resolved, both changed **zero lines** in Domain or Application.

### Database — PostgreSQL 15

Free, strong relational guarantees, and identical behaviour in local Docker and in most cloud hosts. The project leans on those guarantees heavily: six foreign keys and six check constraints are enforced by the database, not only by code.

### ORM — EF Core 10 (Code-First) + Npgsql

**Code-First over Database-First**: the C# entities are the source of truth and the schema is generated from them, so schema history lives in git beside the code that depends on it.

**Configuration style**: Fluent API only (`IEntityTypeConfiguration<T>`, one file per entity). No Data Annotations on domain entities — persistence concerns stay out of the Domain project entirely.

**Table-Per-Hierarchy (TPH)**: `Note` and `TaskItem` are distinct C# classes stored in one `Items` table with an integer `Kind` discriminator. See §6 for the full explanation.

**A trap worth knowing**: EF's global query filters (`!IsDeleted`; `DeletedAt == null` is deferred, Requirements §12) apply to LINQ only. Any raw SQL bypasses them completely.

### Application pattern — MediatR 14 (CQRS)

Every use case is one `Command` or `Query` plus one dedicated `Handler`. A controller action's entire body is "build a command, send it, map the result".

**Chosen over a traditional service layer** (`CourseService`, `TaskService`…) because:
- No shared god-class that grows forever.
- Cross-cutting behaviour is inserted once as a pipeline behaviour wrapping *every* request, instead of being repeated inside each service method.
- Each handler has exactly one reason to change.

> **Licensing**: MediatR became dual-licensed from v13 (2025) — free for personal, educational, and small-revenue use. This project is well inside the free tier. The informational warning printed at startup is expected and has no functional effect.

### Validation — FluentValidation 12

Rules live in a `Validator` class next to each command, not inside the handler. They run **automatically** through `ValidationBehavior` before any handler executes, so a handler may assume its input is already valid and never contains defensive `if (...) throw` checks.

The behaviour runs all validators in parallel via `Task.WhenAll` and forwards the `CancellationToken` — the synchronous `Validate()` would violate the async standard in `CODING_STANDARDS.md` §2 on the one piece of code that runs for every single request.

### Password hashing — BCrypt.Net-Next

**Why BCrypt**: a configurable work factor (currently `12`) means hashing can be made slower as hardware gets faster, which is the entire point of a password hash.

**Why the `Enhanced` variants specifically**: plain BCrypt silently truncates its input at 72 bytes. Two long passwords sharing their first 72 bytes would authenticate each other, with no error anywhere. `EnhancedHashPassword` / `EnhancedVerify` pre-hash the input, so the limit disappears.

```csharp
using BC = BCrypt.Net.BCrypt;
...
BC.EnhancedHashPassword(password, WorkFactor);
```

**This choice is irreversible.** Standard and enhanced hashes are not interchangeable; switching after real accounts exist would lock every user out.

**Two gotchas**: the library's namespace and its main class are both named `BCrypt`, which breaks a plain `using BCrypt.Net;` — hence the alias above. And `Verify` throws `SaltParseException` on a malformed stored hash, so it is wrapped to return `false` instead of turning a rejected login into a 500.

### Refresh token hashing — SHA-256, not BCrypt

Refresh tokens are stored hashed, but with a **deterministic** hash. Every refresh looks the token up by its hash, and BCrypt's per-row salt would make that lookup impossible — it would require scanning the entire table.

SHA-256 is safe here precisely because a refresh token is high-entropy random data, unlike a human-chosen password.

### Access tokens — `Microsoft.IdentityModel.JsonWebTokens`

Login and refresh issue them; `JwtBearer` validates them on every request that is not marked anonymous. `POST /api/auth/login` verifies the password against BCrypt, checks `IsActive`, and returns a signed access token plus a refresh token whose SHA-256 hash is the only form stored. Every refusal — unknown email, wrong password, deactivated account — is the same 401 body, and `Verify` runs against a dummy hash even when no user was found, so the two cases cannot be told apart by response time.


Tokens are written with `JsonWebTokenHandler` — the handler `JwtBearer` has used to read them since ASP.NET Core 8. `System.IdentityModel.Tokens.Jwt` is the previous generation of the same library, and writing with one handler while reading with the other is a known source of claim-name surprises (Requirements ADR-29, §9.1).

### AI provider — plain HTTP through `Microsoft.Extensions.Http`

Gemini is called over its REST API with a typed `HttpClient` from `IHttpClientFactory`, and parsed with `System.Text.Json`. No provider SDK is taken: the request is one POST with a prompt, the response is one JSON document, and an SDK would add a dependency and its release cadence to save a few lines. The factory is taken, because a `HttpClient` per request exhausts sockets and caches DNS for the life of the process. See §4.6 for the boundary this sits behind (Requirements ADR-35).

### Concurrency — PostgreSQL `xmin`

Refresh token rotation reads a row and writes it back, so two parallel requests could both succeed and fork one session into two. PostgreSQL changes the hidden `xmin` column on every update; EF Core maps it as a shadow concurrency token, so the second save fails instead. No column is added and no Domain type gains a field (Requirements ADR-27, §14.4).

Proven, not assumed: with SQL command logging on, a rotation logs `UPDATE "RefreshTokens" SET ... WHERE "Id" = @p3 AND xmin = @p4`. The same log shows the revoke and the insert leaving as one command, which is why §9.3 forbids adding an explicit transaction on top of the single save.

### Testing — xUnit + Moq + FluentAssertions

Moq fakes the interfaces declared in Application, so a test like "creating an item under another user's parent throws `ForbiddenException`" runs in milliseconds against no database. FluentAssertions makes failures readable at 2am.

`StudyHub.Infrastructure.Tests` covers infrastructure code that needs no database — password hashing and token generation. Application tests never reference Infrastructure; the tests follow the same dependency rule as the code.

**Not automated yet**: EF Core queries and HTTP round-trips need integration testing against a containerized database — scheduled for M10.

### Containerization — Docker Compose (PostgreSQL only)

Guarantees an identical Postgres version everywhere without a local install. The API itself is **not yet containerized**; it runs via `dotnet run`.

---

## 4. How the System Works

This section is the mental model. Read it before reading any handler.

### 4.1 The content model

Three content types, two tables.

```mermaid
graph TD
    C["Course: OOP 101<br/>always a root"]
    N1["Note: Lecture 1 notes<br/>Depth 0"]
    T1["Task: Review the slides<br/>Depth 1"]
    N2["Note: Open questions<br/>Depth 2"]
    T3["Task: Buy a notebook<br/>Depth 0, no course"]

    C --> N1
    N1 --> T1
    T1 --> N2

    style C fill:#e6f1fb,stroke:#185fa5
    style T3 fill:#f1efe8,stroke:#5f5e5a
```

Read the middle branch carefully: a **task nested under a note**, and a **note nested under that task**. Free nesting in both directions is the whole point of the design, and it is what made a single `Items` table necessary.

`Buy a notebook` shows the other valid shape — a standalone root belonging to no course at all.

**Two independent axes**, not alternatives:
- `CourseId` — which course this belongs to (grouping)
- `ParentItemId` — which item this sits under (nesting)

A root item may have a course or not. A nested item **inherits** its parent's `CourseId`; a request that sends a course together with a parent is rejected — any course, the parent's own included (Requirements rule 3.2.5).

### 4.2 Registration — implemented end to end

```mermaid
sequenceDiagram
    participant Client
    participant Ctrl as AuthController
    participant Med as MediatR
    participant Val as ValidationBehavior
    participant H as RegisterUserCommandHandler
    participant Repo as IUserRepository
    participant Hash as IPasswordHasher
    participant UoW as IUnitOfWork

    Client->>Ctrl: POST /api/auth/register
    Ctrl->>Med: Send(RegisterUserCommand)
    Med->>Val: run pipeline first

    alt input invalid
        Val-->>Client: ValidationException → 400 + field errors
    else input valid
        Val->>H: Handle(command)
        H->>Repo: EmailExistsAsync(email)
        alt email taken
            Repo-->>H: true
            H-->>Client: ConflictException → 409
        else email is new
            H->>Hash: EnhancedHashPassword(password)
            H->>H: User.Create(...) → normalizes email, validates
            H->>Repo: Add(user)
            H->>UoW: SaveChangesAsync()
            Note over UoW: catches Postgres 23505<br/>→ ConflictException
            UoW-->>H: ok
            H-->>Client: 201 Created { userId }
        end
    end
```

**Two conflict checks, on purpose.** The pre-check in the handler produces a friendly message. The unique index plus the `23505` translation in `UnitOfWork` is the actual protection — two simultaneous requests with the same email both pass the pre-check, and only the database can arbitrate.

**Why the `23505` translation lives in Infrastructure**: `PostgresException` is an Npgsql type. Catching it in a handler would make the Application layer aware of the database engine and break the dependency rule. The same place translates `DbUpdateConcurrencyException` into `ConflictException` as well (ADR-27).

### 4.3 Creating a nested item — the richest flow

```mermaid
sequenceDiagram
    participant Client
    participant Ctrl as NotesController
    participant Val as ValidationBehavior
    participant H as CreateNoteCommandHandler
    participant Cur as ICurrentUserService
    participant Repo as IItemRepository
    participant Ent as Note.Create / Item.Initialize
    participant UoW as IUnitOfWork

    Client->>Ctrl: POST /api/notes { title, parentItemId }
    Ctrl->>Val: Send(CreateNoteCommand)
    Note over Val: rejects parentItemId + courseId together
    Val->>H: Handle(command)
    H->>Cur: UserId
    H->>Repo: GetByIdAsync(parentItemId)

    alt parent not found (or soft-deleted)
        Repo-->>H: null
        H-->>Client: NotFoundException → 404
    else parent belongs to someone else
        H-->>Client: ForbiddenException → 403
    else parent at maximum depth
        H-->>Client: ConflictException → 409
    else parent is valid
        H->>Ent: Note.Create(userId, title, content, parent, courseId)
        Note over Ent: re-checks ownership<br/>checks parent not deleted<br/>checks Depth < MaxDepth<br/>inherits parent.CourseId<br/>sets Depth = parent.Depth + 1
        Ent-->>H: note
        H->>Repo: Add(note)
        H->>UoW: SaveChangesAsync()
        Note over UoW: DB re-checks depth,<br/>root/depth agreement,<br/>title non-blank
        UoW-->>Client: 201 Created { noteId }
    end
```

**Three layers guard the same rules, deliberately:**

| Layer | Checks | Why it exists |
|---|---|---|
| Validator | shape of the request | fails fast, before any I/O |
| Handler | parent exists, is owned, has room | produces a precise 404, 403, or 409 |
| Entity | ownership, depth, deleted parent | **trusts no caller** — a handler that forgets cannot corrupt the tree |
| Database | depth range, root/depth agreement, title, enum ranges | protects against anything writing outside the application |

Duplication here is not waste. Each layer answers a different question: the handler answers *"what should the client be told?"*, the entity answers *"is this object valid?"*, the database answers *"is this row valid regardless of who wrote it?"*

**A rule is written once and asked twice** (ADR-30). The handler asks `parent.IsAtMaxDepth`; `Item.Initialize` asks the same member. Restating the condition in the handler would put the rule in two places, free to drift. Before this shape existed the handler did not ask at all, so the request reached the entity's guard and the client got a 500 — the A5 failure shape, now closed for this rule.

### 4.4 Cascade delete — two shapes

```mermaid
graph LR
    subgraph A["Delete an item"]
        A1["load item"] --> A2["check ownership"]
        A2 --> A3["walk children<br/>level by level<br/>max 5 rounds"]
        A3 --> A4["MarkAsDeleted on each"]
        A4 --> A5["one SaveChanges<br/>= one transaction"]
    end
```

```mermaid
graph LR
    subgraph B["Delete a course"]
        B1["load course"] --> B2["check ownership"]
        B2 --> B3["WHERE CourseId = id<br/>one query, all depths"]
        B3 --> B4["MarkAsDeleted on each<br/>+ on the course"]
        B4 --> B5["one SaveChanges<br/>= one transaction"]
    end
```

Stamping one `DeletedBatchId` on every row a delete operation touches, so a later restore can tell a cascade from a deliberate delete, is deferred (ADR-25, Requirements §12).

**The course path needs no traversal at all.** Because every nested item inherits its root's `CourseId`, a flat `WHERE CourseId = @id` already returns the entire tree at every depth. That is the payoff of the deliberate duplication in ADR-09.

**Why no explicit transaction**: a single `SaveChangesAsync` call *is* one transaction in EF Core. Wrapping it in `BeginTransaction` would add nothing.

**Why level-by-level instead of `WITH RECURSIVE`**: depth is capped at five, so the walk costs at most five queries. Staying in LINQ keeps the soft-delete filter applied automatically and avoids a hand-maintained SQL string that EF would not filter at all.

### 4.5 How an exception becomes a status code

```mermaid
graph TD
    E["exception thrown anywhere"] --> G["GlobalExceptionHandler"]
    G --> V["ValidationException → 400<br/>+ errors grouped by field"]
    G --> F["ForbiddenException → 403"]
    G --> N["NotFoundException → 404"]
    G --> C["ConflictException → 409<br/>duplicate, full parent, lost race"]
    G --> I["InvalidCredentialsException → 401<br/>login and refresh only"]
    G --> Q["QuotaExceededException → 429"]
    G --> X["ExternalServiceException → 502"]
    G --> S["anything else → 500<br/>no internal detail leaked"]
```

Every response is RFC 9457 `ProblemDetails`. Expected exceptions are logged as warnings; unexpected ones as errors with the full stack.

**The other 401, and the permission 403, never reach this handler.** The authorization middleware rejects a missing or invalid access token (401), or a valid one whose role lacks the endpoint's permission (403), before MediatR runs. Both responses have an empty body.

**400 is shape; 403, 404 and 409 are state.** Validators throw the first; handlers throw the rest. A handler never throws `ValidationException` (Requirements §8).

**The wiring trap**: `AddExceptionHandler<T>()` registers the handler, but nothing calls it without `app.UseExceptionHandler()` in the pipeline. Both compile and start cleanly either way.

**Two different sources produce 400.** Model binding inside `[ApiController]` rejects malformed JSON *before* MediatR runs, and its response carries a `traceId`. A validator's `ValidationException` does not. That difference is the fastest way to tell a bad payload from a broken rule.

### 4.6 The AI flow (UC-06, UC-07)

Two operations on a note the user already saved, one paid call each (ADR-38):

```mermaid
graph LR
    N["POST /api/notes/{id}/summarize<br/>POST /api/notes/{id}/extract-tasks"] --> D{"extraction, and the<br/>note at max depth?"}
    D -->|yes| R1["409"]
    D -->|no| Q{"quota left<br/>for the estimate?"}
    Q -->|no| R2["429"]
    Q -->|yes| AI["AI provider answers"]
    AI -->|"fails, nothing billed"| R3["502"]
    AI -->|"unreadable, billed"| L
    AI --> L["insert the AiUsageLog row"]
    L --> S["200 + summary or suggestions<br/>nothing else written"]
    S --> U["user approves a suggestion"]
    U --> T["POST /api/tasks<br/>parent = the note"]
```

**The provider boundary is one interface and one class.** Application declares `IAiService` with one method per operation — "summarize this note", "extract tasks from this note", never "complete this prompt". Everything Gemini knows about — the route, the key, the two prompts, the request and response JSON, and the `usageMetadata` field the token count is read from — lives inside `GeminiAiService` in Infrastructure. A second provider is therefore a second class and one line in `AddInfrastructureServices` (ADR-35). Both failure modes leave that class as `ExternalServiceException`, an Application type, for the same reason `PostgresException` never leaves the persistence code.

**The call goes through a typed `HttpClient` from `IHttpClientFactory`**, with the base address, the explicit timeout and the API-key header configured where the client is registered. A client constructed per request exhausts sockets and caches DNS for the life of the process, and the 100-second default timeout is the absence of a decision rather than one (§15.3).

**A reasoning model is assumed, not hoped against.** Such a model spends part of the output budget on thinking, and returns that thinking as extra response parts marked `thought`. Reading `parts[0].text` would therefore hand the model's reasoning back as if it were the answer, and when the budget is exhausted *while* thinking the provider returns a candidate with no answer parts at all — occasionally with no `content` object either. So every step of the walk down the response is optional, thought parts are skipped, and the remaining text is concatenated. How much a model may think is passed straight through from `Ai:ThinkingBudget` and `Ai:ThinkingLevel` and never invented: a model that does not know those fields refuses the entire request, so with both unset the request carries no thinking configuration at all.

**A failed first call has to be diagnosable.** A refusal logs its status *and* the first 500 characters of the body, which is where the provider names an unknown model or a rejected field; an unusable answer logs `finishReason`, and `MAX_TOKENS` logs the one sentence that identifies the cause — the budget ran out before the answer began. All of it at Warning, and none of it in the response body, which must never say which provider is behind the endpoint (§15.3).

**Without a key there is a fake provider, chosen once at startup.** `Ai:ApiKey` decides which implementation is registered, and the choice is logged by class name at startup. `FakeAiService` answers deterministically from the note itself, so both endpoints, the quota arithmetic and the 502 path can be exercised with no key, no network and no cost — and `Ai:FakeFailure` makes it fail on demand. It is never a fallback after a failed real call: a silent downgrade would make a broken provider look like a working one (ADR-35).

**Provenance is parenthood.** An approved task is simply a child of its source note. The old schema had a dedicated `Tasks.SourceNoteId` column; the tree makes it unnecessary.

**Human in the loop, with no endpoint of its own** (ADR-28). Extraction returns suggestions and writes nothing but the usage record. Approval is the existing `POST /api/tasks`. A summary is returned the same way and never stored: the client keeps it with `PATCH /api/items/{id}` or `POST /api/notes`, or pays again.

**Usage is recorded where it is paid, not where it is used.** An earlier version of this flow recorded tokens after the user approved — a user who never approved would have extracted for free. The record now follows the provider call directly, and never throws (ADR-24). A response that arrived but could not be read is recorded before the client is given its 502 (§15.3).

**The log is the only record of usage** (ADR-39). Until M8.1 a counter on `Users` was incremented in the same transaction as the `AiUsageLog` row, and it was reset lazily by the user's next AI call — so `GET /api/auth/me` reported last month's figure until that call, and the lazy reset raced the increment at the month boundary. The counter was a cached copy of a table that was already stored and indexed, so it was removed: a user's monthly usage is now the sum of their `AiUsageLogs` rows since the start of the month, recording is one insert that parallel operations can neither lose nor collide on, and there is nothing to reset. The month itself starts at midnight in Riyadh, not at UTC midnight three hours later (ADR-40). The rule and the record stay separate: `User.HasQuotaFor(used, estimate)` in Domain answers the question, and the inserted row is the record (ADR-24, Requirements §15.1).

---

## 5. The `Items` Table and TPH

The single most consequential decision in the codebase.

### The problem

A foreign key points at exactly one table. The requirement said a task's parent could be a course, a note, or another task — three tables. The relational model has no cheap way to express that.

### The alternatives, and what each cost

| Option | Shape | Cost |
|---|---|---|
| Polymorphic parent | `ParentId` + `ParentType` | **No foreign key at all.** Orphans accepted silently. Recursion spans two tables at every level |
| Exclusive arc | three nullable FK columns + a check | Six parent columns across two tables; the worst recursive query of the three |
| Single node table | one table, self-FK | Correct shape, but dissolves the domain model into wide nullable rows |

### The resolution

The cost was not the price of a *tree*. A tree over one type is a single nullable self-referencing column and nothing more. The cost was the price of a **contradiction**: types declared distinct while being required to nest interchangeably.

Unifying `Note` and `TaskItem` into one `Item` removed the contradiction rather than paying for it — and TPH kept the C# classes separate and rich while the database sees one table.

### What it bought

- A real self-referencing foreign key: `FK_Items_Items_ParentItemId`. The database refuses an orphaned parent reference.
- Free nesting in both directions, with no extra code.
- One-table recursion.
- `SourceNoteId` disappeared into ordinary parenthood.
- The note's awkward "title or content" constraint became a plain `Title NOT NULL`.

### What it cost

- `Status`, `Priority`, `DueDate` are nullable at the database level, so `CK_Item_TaskFields` replaces `NOT NULL`.
- Notes and tasks share one growing table.
- TPH is a concept you must understand before touching the configuration.

### How it is configured

```csharp
builder.HasDiscriminator<int>("Kind")
       .HasValue<Note>(0)
       .HasValue<TaskItem>(1);
```

An **integer** discriminator, not the default string: renaming a C# class later must not invalidate stored rows.

**`Kind` never changes** (Requirements rule 3.2.8). EF Core derives the discriminator from the object's CLR type, and an object cannot change its type — a conversion would be a delete and a create, taking the whole subtree with it.

`DbSet<Item>` returns everything; `DbSet<Note>` and `DbSet<TaskItem>` filter by `Kind` automatically. **Repositories that resolve a parent must use `Items`** — a parent may be either type, and querying `Notes` would return `null` for every task parent, with no error and no clue.

### The six check constraints

| Name | What it stops |
|---|---|
| `CK_Item_Depth` | nesting past five levels |
| `CK_Item_RootDepth` | a root with depth, **and** a child without — one constraint, both directions |
| `CK_Item_TaskFields` | a note carrying task fields, or a task missing them |
| `CK_Item_StatusValue` | an out-of-range enum value; presence is not validity |
| `CK_Item_PriorityValue` | same, for priority |
| `CK_Item_Title` | a whitespace-only title, matching the domain's `IsNullOrWhiteSpace` |

`CK_Item_Title` uses a regex rather than `IS NOT NULL` because a check constraint that evaluates to `NULL` **passes** — the earlier null-only version accepted a title of three spaces.

---

## 6. Identity & Authorization

### Protected by default

A fallback authorization policy requires an authenticated caller on **every** endpoint. Only `register`, `login`, `refresh`, and the OpenAPI document opt out, with `[AllowAnonymous]`. A new controller that forgets an attribute is therefore closed, not open.

Identity is the `sub` claim of the validated token. `CurrentUserService` reads it and nothing else, and the Application layer still depends only on `ICurrentUserService` — replacing the pre-M6 `X-User-Id` header changed no line outside the API project:

```
before M6:  header    → CurrentUserService → ICurrentUserService → handlers
now:        JWT claim → CurrentUserService → ICurrentUserService → handlers
                                              ↑ unchanged
```

### The flow

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant DB
    Client->>API: POST /api/auth/login
    API->>DB: store the SHA-256 of the refresh token
    API-->>Client: 200 { accessToken (15 min), refreshToken (7 days) }
    Client->>API: any request + Bearer accessToken
    Note over API: no valid token → 401, before MediatR
    Client->>API: PATCH /api/admin/users/{id}/deactivate
    Note over API: policy reads the role claim, asks RolePermissions → 403 if absent
    Client->>API: POST /api/auth/refresh { refreshToken }
    API->>DB: revoke the old token (xmin-checked) + insert the new, one save
    API-->>Client: 200 { new pair }
    Client->>API: POST /api/auth/logout { refreshToken }
    API->>DB: revoke it, if it is the caller's and still active
    API-->>Client: 204, always
```

**Permissions are policies named after `Permissions` constants.** `PermissionPolicies` registers one per constant by reflection; `PermissionAuthorizationHandler` parses the `role` claim by exact name and asks `RolePermissions` — the same map `User.Can` asks. No permission list exists in the API layer.

**The first administrator is seeded from `AdminSeed:*` in user-secrets** at startup, through `SeedAdministratorCommand`: an existing account is promoted and its password left alone; a missing one is created under the registration password policy and promoted in the same save.

The decisions and their costs live in Requirements §9 and ADR-19 to ADR-21, ADR-27, ADR-29, ADR-31.

### Ownership enforcement today

Parent-item ownership is checked **twice** — in the handler for a precise 403, and inside `Item.Initialize` because the entity trusts no caller. Depth follows the same shape: the handler asks `parent.IsAtMaxDepth` for a precise 409, and the entity asks it again (ADR-30).

Course ownership is checked in the handler **only**, because the entity receives a `Guid` rather than a `Course` object. This asymmetry is known and accepted; a future handler that forgets the check has no safety net beneath it.

---

## 7. Why the Business Logic Is Fast to Test

Domain and Application depend on no database and no web server, so the whole business-rule suite runs in about two seconds with Docker stopped:

```bash
dotnet test
```

The current count is whatever `dotnet test` reports. It is not written here, because a number in a document goes stale with the next test. A third project, `StudyHub.Infrastructure.Tests`, runs in the same command — still with no database.

This is a measurable payoff of the architecture, not a theoretical one. Compare it to the manual `.http` file, which needs Postgres running, the API running, and human inspection of each response — necessary for end-to-end confidence, far too slow for every change.

### Three verification rules, learned the hard way

1. **A successful build proves nothing.** Verify the specific effect: an HTTP status code, a row in `psql`, a passing test.
2. **Prove a constraint by breaking it *and* by inserting a row that should pass.** One without the other is half an answer — and read *which* constraint the error names. A malformed statement produces a red error and no inserted row, exactly like a working constraint does.
3. **A change with no externally observable behaviour is verified by reading the file.** When no code path calls the changed method yet, the build passes, the tests pass, and every endpoint behaves identically whether or not the change was ever applied. Nothing else will catch it.

Requirements §10 adds two consequences: a proof belongs to the layer where the behaviour lives, and a concurrency rule needs a concurrent proof.

---

## 8. Getting Started

```bash
# 1. Start PostgreSQL
docker compose up -d

# 2. Store the connection string for the running app (one time)
cd StudyHub.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=StudyHubDb;Username=postgres;Password=YourSecurePassword"
cd ..

# 3. Store the JWT signing key — the app refuses to start without one of at least 32 bytes
#    PowerShell 5.1 and 7 both:
#      $bytes = New-Object byte[] 48
#      [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
#      dotnet user-secrets set "Jwt:Key" ([Convert]::ToBase64String($bytes))

# 3b. Optional — seed the first administrator on the next start (Requirements §9.4)
#      dotnet user-secrets set "AdminSeed:Email" "admin@example.com"
#      dotnet user-secrets set "AdminSeed:FullName" "Administrator"
#      dotnet user-secrets set "AdminSeed:Password" "<meets the registration policy>"
#    Once the startup log says the administrator was created:
#      dotnet user-secrets remove "AdminSeed:Password"

# 3c. Optional — the AI provider. Without a key the application starts with the fake
#     provider and both AI endpoints work offline; the startup log names which one is live
#      dotnet user-secrets set "Ai:ApiKey" "<the provider key>"
#      dotnet user-secrets set "Ai:Model" "<the model name>"
#    Everything else under "Ai" is non-secret and lives in appsettings.json

# 4. Set the environment variable for design-time commands (per terminal session)
#    PowerShell:  $env:STUDYHUB_DB_CONNECTION = "Host=localhost;..."
#    cmd.exe:     set "STUDYHUB_DB_CONNECTION=Host=localhost;..."

# 5. Apply the schema
dotnet ef database update --project StudyHub.Infrastructure --startup-project StudyHub.API

# 6. Run
dotnet run --project StudyHub.API

# 7. Test the business logic
dotnet test
```

### Two configuration paths that are not interchangeable

| Path | Reads from |
|---|---|
| The running app | `appsettings` → user secrets → DI |
| `dotnet ef` commands | `IDesignTimeDbContextFactory` → `STUDYHUB_DB_CONNECTION` |

**Migration commands succeeding says nothing about whether the app is configured**, and vice versa. Both must be set up, and only running the app tests the first.

The design-time factory has **no fallback value** — it throws with a clear message if the variable is missing. A fallback is how a password ends up committed.

### Manual endpoint testing

Open `StudyHub.API/StudyHub.API.http` in Visual Studio or VS Code with the REST Client extension. Send `S0` once to register the session user, then `S1` at the start of every session to log in; the later requests read the access token from `S1`'s response.

### Inspecting the database directly

```bash
docker exec -it studyhub_postgres psql -U postgres -d StudyHubDb
```

**`psql` is the stronger of the two manual tools**, because it is the only view that does not pass through EF's soft-delete filter. A soft-deleted row is invisible to the API and plainly visible here.

Paste one statement per line — a multi-line paste can merge with the previous statement and produce a syntax error that looks exactly like a constraint rejection.

---

## 9. Current State & Known Technical Debt

Documented on purpose. A learning project is more useful when its gaps are visible.

### Security
- **An access token outlives a deactivation, a logout, or a role change** by up to 15 minutes — the price of stateless auth (Requirements §9.4).
- **The administrator seed password sits in user-secrets** until the operator removes it after the first start (Requirements §9.4).
- **Registration reveals whether an email is in use** — an accepted risk that also undermines login's anti-enumeration rules (Requirements §9.4).
- **Course ownership is guarded in one layer only** (§6).

### Design limits
- **Node moving is not supported.** Three other decisions — stored depth, inherited `CourseId`, and the absence of cycle detection — are safe *only* because of this. Adding moving invalidates all three at once.
- **Correct restore is impossible as designed.** After a cascade delete, nothing distinguishes a child deleted deliberately from one deleted by cascade. Fixing it means `DeletedAt` plus `DeletedBatchId` instead of `IsDeleted` — deferred, because the project is not deployed and no real data will make it expensive (ADR-25, Requirements §12).
- **Repository + Unit of Work over EF Core is technically redundant.** `DbContext` is already a unit of work and `DbSet<T>` already a repository. Kept because the pattern is worth learning, at the cost of an extra abstraction and the loss of `IQueryable` composition at the boundary — so read queries project inside Infrastructure, through read-side query interfaces (Requirements ADR-32).
- **Last write wins on content edits.** Only refresh tokens get a concurrency token; two tabs editing one item overwrite each other silently (Requirements §12).

### Infrastructure
- **No integration tests.** Infrastructure and API are covered by manual verification only. M10.
- **The API is not containerized.** Only Postgres runs in Docker.
- **No rate limiting.** M10.
- **Refresh tokens are never deleted** — expired and revoked rows accumulate until the cleanup job in M10.

Full roadmap: [`Requirements.md`](Requirements.md) §11.


