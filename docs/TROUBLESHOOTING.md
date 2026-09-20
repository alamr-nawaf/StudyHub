# StudyHub — Troubleshooting Log

A record of every technical problem hit during development, how it was fixed, and why it happened. Kept so the same mistakes aren't repeated — and so the reasoning behind each fix isn't lost.

> Entries are grouped by **type**, not by date. New entries carry a milestone tag `(Mx)` in the heading so it's clear when they happened. Entries without a tag predate this convention.
> See `docs/TROUBLESHOOTING_GUIDE.md` for how to add an entry.

---

## A. Design Flaws (wrong by design, not by accident)

### A1. Foreign Keys existed on the ERD but not in the actual database
**Problem**: Only `Tasks.UserId` had a real FK. `Courses.UserId`, `Notes.UserId`, `Notes.CourseId`, `Tasks.CourseId`, `Tasks.SourceNoteId`, `RefreshTokens.UserId`, and `AiUsageLogs.UserId` were plain `Guid` columns with no constraint and no index.
**Fix**: Wrote the missing `IEntityTypeConfiguration` classes (Course, RefreshToken, AiUsageLog) and added `HasOne(...).WithMany().HasForeignKey(...).OnDelete(...)` for every relationship, then a new migration.
**Why**: EF Core only creates a relationship when there's a navigation property *or* explicit Fluent API config. With neither present, it silently treats the column as an ordinary field. An ERD is a drawing — it enforces nothing.

### A2. Domain entities were missing the methods their own use cases required
**Problem**: `TaskItem.Create` didn't accept `courseId` (so a task could never be linked to a course — UC-04 was unbuildable), and the entity had no `UpdateStatus`, `UpdateDetails`, or `MarkAsDeleted` — despite a soft-delete query filter being configured for it. `Course` had no `UpdateDetails`.
**Fix**: Added the missing methods; each mutation also sets `UpdatedAt`.
**Why**: Entities were written from the ERD's *columns*, not from the use cases' *behaviors*. With private setters, an unexposed behavior isn't just missing — it's impossible to perform.
**Repeat (M5)**: The same gap in `User`, which had only a factory method — no `ConsumeTokens`, `ResetQuotaIfNeeded`, `Deactivate`, or `ChangePassword`, leaving UC-07's quota unenforceable. Added all four; `ResetQuotaIfNeeded` takes the current time as a parameter so it stays testable without a clock abstraction.

### A3. `User.Create` had zero validation, unlike every other entity
**Problem**: All other factory methods validated their input; `User.Create` accepted anything. Email was also stored as typed, so `User@x.com` and `user@x.com` would register as two separate accounts despite the unique index.
**Fix**: Added guard clauses for name and email, and normalized email with `.Trim().ToLowerInvariant()`.
**Why**: Inconsistency crept in as entities were written at different times. Postgres unique indexes are case-sensitive by default — normalization has to be deliberate.

### A4. Documentation duplicated instead of referenced
**Problem**: `docs/Requirements` and `README.md` contained the same content verbatim, so editing one silently made the other wrong.
**Fix**: Split responsibilities — `Requirements.md` = what and why (PRD), `ARCHITECTURE.md` = how it works, `README.md` = how to run it.
**Why**: No single source of truth. Duplicated docs always drift.

### A5. Domain exceptions reached the client as HTTP 500 (M5)
**Problem**: `StudyHub.API.http` expects 409 for a duplicate email and 400 for invalid input. Both returned 500 with a stack trace, because nothing caught `ConflictException` or `ValidationException` at the boundary.
**Fix**: Added `NotFoundException` and `ForbiddenException` to Application, and a `GlobalExceptionHandler` (`IExceptionHandler` + `AddProblemDetails`) in the API mapping each type to a status code and a `ProblemDetails` body.
**Why**: An exception type carries meaning only inside the process. Without a translation layer at the edge, every deliberate business rule arrives at the client as an anonymous server failure.
**Repeat (M5.1)**: The same shape, narrowed. `GlobalExceptionHandler` now covers the four Application exception types, but a domain invariant that throws `InvalidOperationException` still fell through to 500 — nesting under a parent already at maximum depth was one. Published the rule as `Item.IsAtMaxDepth` so the handler can ask it and return 409, and pointed `Initialize` at the same member so the rule exists once; `>= MaxDepth` now appears exactly once in the whole project. The entity guard stays: if it ever fires, a handler forgot to ask, and 500 is the honest answer to a bug.

### A6. `ValidationBehavior` violated the project's own async standard (M5)
**Problem**: Found during code review — no runtime error. The behavior called the synchronous `Validate()` and never passed `CancellationToken`, against the rule in `CODING_STANDARDS.md` §2.
**Fix**: Switched to `ValidateAsync` over `Task.WhenAll` across all validators, and forwarded the token to `next(cancellationToken)` — the delegate shape MediatR v14 expects.
**Why**: Cross-cutting code runs on every single request, so one defect there is not one defect — it is one per call. Pipeline code deserves the strictest reading of a standard, not the most convenient one.

### A7. A single base entity forced an audit field onto a write-once log (M5)
**Problem**: Found during code review. `AiUsageLog` inherited `UpdatedAt` from `BaseEntity` even though a usage record is never modified — a column guaranteed to stay null for the life of the table.
**Fix**: Split the base. `BaseEntity` now holds `Id` + `CreatedAt` only; a new `AuditableEntity` adds `UpdatedAt`. `AiUsageLog` inherits the first, every other entity the second.
**Why**: Inheritance should follow lifecycle, not convenience. One base class shared by entities with different lifecycles hands each of them state it can never legitimately use.

### A8. `AiUsageLog` stored the same instant in two columns (M5)
**Problem**: Found during code review. `Create` set `OperationDate` to `DateTime.UtcNow` while the inherited `CreatedAt` already held that exact value.
**Fix**: Removed `OperationDate`; the inherited `CreatedAt` is now the operation's timestamp.
**Why**: Two columns that can never disagree are one column. Storing a value twice adds no information and only creates a way for the copies to drift apart later.

### A9. Email normalization was written twice, in two layers (M5)
**Problem**: Found during code review. `User.Create` normalized with `.Trim().ToLowerInvariant()`, and `UserRepository.EmailExistsAsync` repeated the same expression independently.
**Fix**: Introduced an `Email` value object in Domain that owns normalization and validation. Both call sites now go through it; EF Core stores it as text via a value converter.
**Why**: A rule duplicated across two layers is a rule that will eventually hold in only one. The cost here was concrete — the two sides producing different strings means a duplicate account walking straight past a unique index.

### A10. The note's check constraint was weaker than the rule it enforced (M5)
**Problem**: `Note.Create` rejects whitespace-only input via `IsNullOrWhiteSpace`, but the database constraint only tested `IS NOT NULL`. A note whose title was three spaces was accepted by Postgres — confirmed by inserting one directly.
**Fix**: Rewrote it as `COALESCE("Title", '') ~ '\S' OR COALESCE("Content", '') ~ '\S'`, then re-ran the same insert and watched it get rejected.
**Why**: A check constraint that evaluates to `NULL` **passes**, so the columns have to be coalesced before testing. And a constraint written from the column's shape rather than from the entity's rule will always be the looser of the two.

### A11. The database accepted any integer for an enum column (M5)
**Problem**: `Status` and `Priority` are stored as integers with three valid values each, and nothing stopped a row with `Status = 99`.
**Fix**: Added `CK_Task_Status` and `CK_Task_Priority` (`BETWEEN 0 AND 2`), then confirmed that 99 and 7 are rejected while 0 and 1 still insert.
**Why**: An enum is a C# guarantee, not a database one — the column only ever sees an `int`. Anything writing from outside the application (a script, a migration, a repair query) meets no rule at all.

### A12. Refresh tokens were stored in readable form and could never be revoked (M5)
**Problem**: Found during code review. `RefreshToken.Token` held the raw token, and although a `RevokedAt` column existed, no method could ever set it.
**Fix**: Replaced the column with `TokenHash`, added `Revoke()` and `IsActive()`, plus `ReplacedByTokenId` to record the rotation chain.
**Why**: A table holding live session credentials in readable form turns a database leak into account takeover. And a column no behavior can write is decoration — the same pattern as A2, in a security-critical place.

### A13. The column looked up on every session refresh had no index (M5)
**Problem**: Found during code review. `RefreshTokens.Token` was the lookup key for every refresh, yet the only index on the table was the one on `UserId` that came free with the foreign key.
**Fix**: Unique index on `TokenHash`. Also replaced the standalone `AiUsageLogs.UserId` index with a composite `(UserId, CreatedAt)`, which serves both query shapes.
**Why**: An index follows how a column is *queried*, not whether it happens to be a foreign key. And a composite index already covers its own leading column — a separate index on that column is duplicate work on every write.

### A14. Several columns were left as unbounded `text` (M5)
**Problem**: `FullName`, `Note.Title`, `RefreshToken.Token`, and `AiUsageLog.OperationType` had no configured length, so EF generated plain `text`.
**Fix**: Gave each an explicit `HasMaxLength` in its configuration file.
**Why**: EF Core constrains only what a configuration mentions. Silence is not a default of "reasonable" — it is a default of "unbounded".

### A15. Entities were defined as distinct types yet required to nest inside each other (M5)
**Problem**: Found while designing the tree. `Note` and `TaskItem` were separate tables with separate rules, while the requirement said any of them could be the parent of any other. Three storage shapes were costed and every one demanded a real sacrifice: no foreign key at all, or six parent columns across two tables, or a dissolved domain model.
**Fix**: Merged them into one `Items` table using EF Core TPH inheritance — `Note` and `TaskItem` stay separate C# classes over a shared `Item` base, with a `Kind` discriminator and a self-referencing `ParentItemId`. Verified in psql that a task nests under a note and a note under a task.
**Why**: The price we kept trying to negotiate was the price of a contradiction, not of a tree. A tree over a *single* type needs one nullable self-referencing column and nothing else. When every shape costs something, the requirement — not the schema — is usually what's wrong.

### A16. Two of the three planned tree operations were never needed (M5)
**Problem**: The plan specified a tree service with three operations: fetch descendants, detect cycles, compute depth. Moving a node had already been deferred — which makes a cycle impossible (a newly created child has no descendants that could enclose its own ancestor) and makes depth permanent from creation.
**Fix**: Dropped both. Depth became a stored column guarded by `CHECK ("Depth" BETWEEN 0 AND 4)` and `CHECK (("ParentItemId" IS NULL) = ("Depth" = 0))`. Only descendant fetching survived.
**Why**: Two of the three were defences against a mutation the design does not permit. Decide which operations exist before deciding which invariants need guarding — a rule with no operation able to break it is dead code.

### A17. A recursive query was planned for a problem that has a fixed bound (M5)
**Problem**: The plan called for `WITH RECURSIVE` to fetch a subtree, reasoning that level-by-level fetching means one query per level. True in general — but depth is capped at five, so level-by-level costs at most five queries.
**Fix**: Kept the subtree walk in LINQ, looping level by level. Raw SQL would have cost a hand-maintained query string, a result shape EF constrains, and — critically — a query the soft-delete filter does not touch at all.
**Why**: "One query per level" is only alarming when the number of levels is unknown. A bound turns an unbounded cost into a constant, and an optimization justified by the unbounded case stops being justified with it.

### A18. A value converter validated on the way out of the database (M5.1)
**Problem**: Found during code review — no runtime error yet. `UserConfiguration` rebuilt the `Email` value object with `Email.Create`, which throws on a malformed address. Any row whose email column was corrupted by a manual `UPDATE`, an import, or a future migration would make every read of that user throw — including the login lookup — turning a rejected credential into a 500.
**Fix**: Added `Email.FromPersisted`, a non-validating factory, and pointed the converter at it. `Email.Create` stays on every entry path. One test asserts that `FromPersisted` passes an invalid value through untouched, so the difference between the two factories reads as deliberate rather than as a forgotten check.
**Why**: A converter is a mapping, not a gate. Validation belongs where untrusted input enters the system; running it again on the way out re-judges data the system already accepted, and turns one bad row into a total outage for its owner instead of a single rejection. Same shape as D4, in a different place.

### A19. A client date without a UTC offset reached the database as a 500 (M5.1)
**Problem**: Found during code review, then reproduced. `POST /api/tasks` with `"dueDate": "2026-10-01T14:00:00"` — or with an explicit offset like `+03:00` — returned 500. Npgsql refuses to write a `DateTime` whose `Kind` is not `Utc` to a `timestamptz` column, and the JSON reader produces `Unspecified` for a naive value and `Local` for an offset one. No request in `StudyHub.API.http` had ever sent a `dueDate`, which is the only reason it went unnoticed.
**Fix**: Added a UTC rule to `CreateTaskCommandValidator` rejecting any `Kind` other than `Utc`, with six validator tests covering it and the course-inheritance rule beside it. `DueDate` is the only `DateTime` reaching the API from a client; every other timestamp is written by an entity from `DateTime.UtcNow`.
**Why**: A rule that lives only in a driver's write path surfaces as a server error instead of a rejection. The boundary that accepts a value is the boundary that must judge it — and an input shape nothing in the manual test file ever sends is an input shape nobody has verified.

### A20. The implementation plan dropped a claim the requirements had already decided (M6)
**Problem**: Caught while reviewing the M6 plan, before any file was written. `Requirements.md` §9.1 (decision 5), ADR-21 and the §11 roadmap row all state the access token carries `sub`, `jti` and `role`. The plan's step 6.2 defined `GenerateAccessToken(Guid userId, DateTime utcNow)` — no parameter the role could travel through — and step 6.3 named its proof `GenerateAccessToken_ShouldCarrySubAndJtiOnly`. Nothing would have gone red: the token would be issued, login would return 200, and the gap would surface only in the last session of M6, when a policy asked for a claim no token carried.
**Fix**: Added `UserRole` to the signature, renamed the test to `GenerateAccessToken_ShouldCarrySubJtiAndRole`, fixed both plan steps in the same session, and fixed the claim value to the enum name rather than its number.
**Why**: The plan decides order and proof; the requirements decide content. A plan that silently *narrows* a decided requirement is harder to catch than one that contradicts it, because every step still reads as complete on its own. Same shape as D6: found by reading a plan against its reference, not by running code.

### A21. An interface defined one session before its only consumer could not serve it (M6)
**Problem**: `ITokenService` was written in 6.2 and proven in 6.3, a full session before the login handler existed. When the handler was finally written it needed two things the contract could not give it: the refresh-token lifetime, which lives in `JwtSettings` inside Infrastructure and is invisible to Application, and a dummy BCrypt hash to compare against when no user is found. Both gaps compiled and both surfaced only while writing the consumer.
**Fix**: `GenerateRefreshToken` now takes `utcNow` and returns a `RefreshTokenResult` carrying the raw token, its hash and its expiry; `IPasswordHasher` gained a `DummyHash` member backed by a `static readonly` hash generated at the same work factor. One test in `TokenServiceTests` changed shape; none was added or lost.
**Why**: An interface is a guess about a caller that does not exist yet, and the guess is only checked when the caller is written. Writing the contract early is still worth it — it keeps the layers honest — but the session that first consumes it must be free to amend it, and the amendment is not a failure of the earlier session. The second gap has a rule of its own: a value whose *format* belongs to one layer must be produced by that layer. A hand-written BCrypt literal in the handler would have parsed, returned false quickly, and silently restored the timing leak that rule 3 of §9.2 exists to close.

### A22. A vertical slice named after an entity hid that entity from its own layer (M6)
**Problem**: The refresh slice was created as `Auth/Commands/RefreshToken/`, following the folder name written in Requirements §6. That declares a namespace member `RefreshToken` under `Auth.Commands`, which shadows the domain entity of the same name for **every** file under `Auth.Commands` — not only the ones inside the new folder. The logout tests, which had compiled before and were not edited, began reporting CS0118 on a `using StudyHub.Domain.Entities;` that had become inert.
**Fix**: Renamed the slice to `Auth/Commands/Refresh/` and removed the `DomainRefreshToken` aliases that had been added as a first response. Requirements §6 was corrected in the same session.
**Why**: The first fix — an alias in each affected file — worked and was wrong: it pays a recurring tax and leaves the cause invisible to whoever writes the next file under `Auth`. Name a slice after the operation (`Refresh`) rather than the entity it touches, and the collision cannot occur. Note also where the error appeared: in a file nobody had edited. A namespace declaration changes name resolution for its whole parent, so the failing file is not always the changed one.

### A23. Any revoked refresh token could sign its owner out of every device (M6)
**Problem**: Found during code review — no runtime error. Reuse detection fired on `RevokedAt is not null`, so a token ended by *logout* counted as stolen. Anyone holding an old logged-out token could revoke all of a user's sessions at will, and a refresh still in flight when the user tapped logout signed them out of their other devices.
**Fix**: `RefreshTokenCommandHandler` now runs reuse detection only when `ReplacedByTokenId` is set — revoked by rotation. A token revoked without a successor gets a plain 401. The handler tests were split into rotated and logged-out cases; Requirements §9.3 was updated.
**Why**: A security response that anyone can trigger is an attack surface of its own. The signal was "an old copy of a *live* chain"; the field that tells a live chain from a dead one already existed and was not consulted.

### A24. A task update field left out of the body silently reset the value (M7)
**Problem**: Found while writing the M7 report — no runtime error, all tests green. `Status` and `Priority` were non-nullable enums on `UpdateTaskStatusCommand` and `UpdateTaskScheduleCommand`, so a body omitting the field bound as `0` and passed `IsInEnum()`. `PATCH /api/tasks/{id}/schedule` with `{ "dueDate": null }` returned 204 and quietly set the priority to `Low`; the same shape on `/status` set `Pending`.
**Fix**: Made both properties nullable, and put `.Cascade(CascadeMode.Stop).NotNull().IsInEnum()` on each in its validator, so an absent field is one 400 with `'Status' must not be empty.`; the handlers pass `request.Status!.Value`. Added a validator test per field and confirmed 400 against the running API, with the task's stored values unchanged.
**Why**: A non-nullable value type cannot express "the client did not send this". The default is indistinguishable from a deliberate `0`, and a validator that only checks the *range* accepts it — so the strictest possible enum check still lets a silent reset through. Where absence must be refused, the type has to be able to represent absence first. Note that `DueDate` is the opposite case: there, `null` is a deliberate value that clears the date.

### A25. The provider response was read as if no model ever thinks (M8)
**Problem**: Found during code review before the first real Gemini call — no runtime error, because only the fake provider had ever answered. The parser read `candidates[0].content.parts[0].text`. A reasoning model returns its thinking as extra parts marked `thought`, so `parts[0]` would have been the *reasoning*, handed back as the summary; and when such a model exhausts `maxOutputTokens` while still thinking it returns a candidate with no `content` at all, which would have thrown inside the `try` and become a 502 naming nothing.
**Fix**: Walk every part, skip the ones marked `thought`, concatenate the rest, and treat each step down the response as optional. `finishReason` is logged when an answer cannot be read, with a dedicated message for `MAX_TOKENS`; a non-success status logs the first 500 characters of the body. `Ai:ThinkingBudget` and `Ai:ThinkingLevel` pass a thinking cap through to the provider, and are absent unless configured. Nine tests in `GeminiAiServiceTests` drive real thinking-model payloads through a stub `HttpMessageHandler`.
**Why**: Code written against one provider response is written against *one example* of it. The shape an external API is allowed to return is wider than the shape it happened to return, and the parts of a response that are optional are exactly the parts that appear first in production. Walk what a contract permits, not what a sample showed.

### A26. A requirements section still described a method the previous milestone had removed (M8.1)
**Problem**: Found during code review — no runtime error. After M8, Requirements §3.3 still listed `User.ConsumeTokens` and promised a `RecordTokenUsage` method, while the code had `HasQuotaFor` plus an atomic SQL record (ADR-37), and §15.1 of the same document already said so. §7 also still called the counter's concurrency "open".
**Fix**: Rewrote §3.3, §7 and §15.1 in M8.1, together with the removal of the counter.
**Why**: A plan lists the sections its author remembered; a search lists the sections that exist. When a milestone removes or renames a member, search every document for the old name before closing the milestone.

### A27. A monthly counter reset lazily showed last month's usage (M8.1)
**Problem**: Found during code review — no runtime error yet. `TokensUsedThisMonth` was reset only by the user's next AI call, and `GET /api/auth/me` read it without the month rule, so from the first of a month until that call it reported the previous month's usage. The same lazy reset raced the atomic increment at the boundary (ADR-37).
**Fix**: Removed the counter and `LastTokenResetDate`; monthly usage is now the sum of the month's `AiUsageLogs` rows, and the month starts at Riyadh midnight (ADR-39, ADR-40).
**Why**: A stored aggregate with an expiry is a cache, and every reader must know when it expires. When the history it summarizes is already stored and indexed, compute from the history instead of keeping a second copy that can drift.

---

## B. Configuration & Wiring

### B1. `AddInfrastructureServices` was written but never called
**Problem**: The DI extension method existed in Infrastructure but `Program.cs` never invoked it, and `appsettings.json` had no `ConnectionStrings` section.
**Fix**: Added `builder.Services.AddApplicationServices()` and `builder.Services.AddInfrastructureServices(builder.Configuration)` to `Program.cs`, plus the connection string to `appsettings.Development.json`.
**Why**: The app started fine without it, because no endpoint touched the database yet. **"It runs without crashing" proves nothing about wiring that nothing exercises yet.** A temporary `/db-check` endpoint that ran `db.Database.CanConnectAsync()` was the actual proof.

### B2. Database password hardcoded in `StudyHubDbContextFactory.cs`
**Problem**: A literal connection string with a password, committed to git.
**Fix**: `Environment.GetEnvironmentVariable("STUDYHUB_DB_CONNECTION")` with a local-dev fallback.
**Why**: Even for a local-only password, this is exactly how secrets end up permanently in git history — where deleting the file later doesn't remove them.

### B3. `dotnet ef` commands don't read `appsettings.json`
**Problem**: Migration commands succeeding was mistakenly treated as evidence that `appsettings.json` was configured correctly.
**Fix**: Understood the two separate paths — `dotnet ef` uses `IDesignTimeDbContextFactory`; the running app uses `appsettings` → DI. Both must be configured, and only running the app tests the second one.
**Why**: Two config paths that look interchangeable but aren't. Testing one says nothing about the other.

### B4. The same password survived in `appsettings.Development.json` (M5)
**Problem**: B2 removed the hardcoded password from the design-time factory, but the development settings file still carried a full connection string — password included — tracked by git.
**Fix**: Moved it into `dotnet user-secrets` (stored outside the repository, addressed by the `UserSecretsId` added to `StudyHub.API.csproj`) and deleted the `ConnectionStrings` section from the file. Verified with `dotnet user-secrets list`, then by running the API and getting a 201 from the register endpoint.
**Why**: B2 fixed one file, not the pattern. A secret removed from one location while a copy remains elsewhere has not been removed — it has been relocated.

### B5. Registering an exception handler does not put it in the pipeline (M5)
**Problem**: Found during code review — caught before it could bite. `AddExceptionHandler<T>()` and `AddProblemDetails()` compile and start the app cleanly, but the handler is never invoked without `app.UseExceptionHandler()`.
**Fix**: Added `app.UseExceptionHandler()` after `builder.Build()`, then confirmed the 409 and 400 responses from the `.http` file.
**Why**: The same shape as B1. Registering a type in the container only makes it *available*; something still has to ask for it. The container never complains about a service nobody resolves.

### B6. A configuration file compiled cleanly while describing the wrong schema (M5)
**Problem**: After the A15 restructure, `NoteConfiguration` built without a single error — every property it referenced (`Id`, `CourseId`, `Title`, `IsDeleted`) still existed through inheritance. It nonetheless declared `Note` a standalone table with its own check constraint, which would have produced a completely wrong model. Only `TaskItemConfiguration` failed, on one deleted property.
**Fix**: Deleted both files rather than repairing the single line the compiler flagged, and wrote one `ItemConfiguration` describing the real shape.
**Why**: The compiler checks that names resolve, not that a mapping is true. When a type's identity changes, its configuration is stale by default — and the absence of an error says nothing about it.

### B7. A pasted Guid carried the response's field name with it (M5)
**Problem**: `POST /api/notes` returned 400 with two errors: the JSON value could not be converted at `$.parentItemId`, and "The command field is required". The pasted value was `"noteId: f9c9..."` — the label from the previous response had been copied along with the Guid.
**Fix**: Removed the label, leaving the bare Guid.
**Why**: Two lessons. A single malformed field collapses the whole request object, so the second error is a symptom of the first, not a separate problem. And this 400 came from `[ApiController]` model binding — identifiable by the `traceId` in the body — before MediatR or `ValidationBehavior` ran at all. Same status code, entirely different source.

### B8. A repository implementation was created in the Application layer (M5)
**Problem**: `ItemRepository.cs` was placed in `StudyHub.Application/Common/Interfaces/` beside its interface. It failed to compile: `Microsoft.EntityFrameworkCore` does not exist in that project, and neither does `StudyHubDbContext`.
**Fix**: Deleted it and recreated it under `StudyHub.Infrastructure/Data/Repositories/`, leaving only `IItemRepository` in Application.
**Why**: The dependency rule made the mistake impossible to commit — Application has no reference to EF Core, so the compiler rejected the file the moment it landed in the wrong project. Interfaces are declared where they are needed; implementations live where their dependencies are permitted.

### B9. Authentication was wired but no content endpoint required it (M6)
**Problem**: Found during code review. `JwtBearer` validated tokens, yet only the `debug-claims` actions carried `[Authorize]`. A request with no token, a malformed token, or the old `X-User-Id` header reached the handler anonymously and failed in `CurrentUserService` as **403**, not 401 — and `DELETE /api/courses/{id}` answered 404 or 403 depending on whether the id existed.
**Fix**: A `FallbackPolicy` requiring an authenticated user in `Program.cs`, with `[AllowAnonymous]` on register, login, refresh, and `MapOpenApi()`. Verified over HTTP: all three cases return 401.
**Why**: Authentication identifies the caller; only authorization *refuses* one. Registering the scheme protects nothing on its own. Make protection the default and exposure the declaration, so a forgotten attribute fails closed.

### B10. The API refused to start because user secrets were never read (M8)
**Problem**: Starting the API for the M8 run with `dotnet run --no-launch-profile --urls http://localhost:5158` stopped immediately with `Database connection string is not configured.` — the same message as a missing secret, although `dotnet user-secrets list` showed the connection string.
**Fix**: Start it with the environment set: `ASPNETCORE_ENVIRONMENT=Development dotnet run --project StudyHub.API --no-launch-profile --urls ...`. The application then started, logged `AI provider in use: FakeAiService.` and served every M8 request.
**Why**: User secrets are only added to configuration in the Development environment, and `--no-launch-profile` discards the profile that sets it, so the environment silently became Production. A configuration value is not "set" in the abstract: it is set *for one environment*, and skipping the launch profile skips everything the profile was providing.

---

## C. Git

### C1. `bin/` and `obj/` committed to the repository (~43 MB, 327 files)
**Problem**: `.gitignore` contained only `.vs/`.
**Fix**: Added `bin/`, `obj/`, `*.user` to `.gitignore`, then untracked them.
**Why**: Build artifacts are regenerated from source on every build. Committing them bloats the repo, creates fake diffs on every build, and embeds machine-specific paths.

### C2. The first cleanup attempt silently did nothing
**Problem**: `git rm -r --cached **/bin **/obj` reported success but removed nothing — the commit changed only `.gitignore`. The artifacts remained tracked for several more commits.
**Fix**: Used a shell-independent approach instead:
```bash
git rm -r --cached .
git add .
git commit -m "fix: retroactively apply .gitignore"
```
**Why**: `**` glob expansion is a shell feature — it works in bash and modern PowerShell, but **not in Windows `cmd.exe`**, where it fails without a hard error. The lesson: verify the *effect* (`git ls-files | grep bin/`), not the command's exit message.
**Repeat (M5)**: Same family, opposite direction. `$env:VAR = "..."` is PowerShell syntax; in `cmd.exe` it fails with *"The filename, directory name, or volume label syntax is incorrect"* — an error about paths, not variables, because `cmd` read the whole line as a command to run. The `cmd` form is `set "VAR=value"`, with the quotes wrapping the entire assignment, not just the value.

### C3. `.gitignore` doesn't apply retroactively
**Problem**: Adding patterns to `.gitignore` didn't remove files git was already tracking.
**Fix**: The `git rm --cached` step above.
**Why**: `.gitignore` only governs *untracked* files. Once tracked, a file keeps being tracked until explicitly removed from the index.

### C4. Stray files committed (`git`, `tt`, `.csproj.user`)
**Problem**: Two empty files created by mistyped terminal commands, plus a personal IDE settings file.
**Fix**: Deleted them; `*.user` added to `.gitignore`.
**Why**: `git add .` stages everything in the working directory indiscriminately, including accidents and machine-local files. Review `git status` before staging, and keep `.gitignore` ahead of the tooling that generates files.

---

## D. Packages & Libraries

### D1. `ConfigurationBuilder` — type not found
**Problem**: Code using `ConfigurationBuilder` in Infrastructure wouldn't compile.
**Fix**: Dropped that approach entirely in favor of `Environment.GetEnvironmentVariable`, which needs no package.
**Why**: `ConfigurationBuilder` lives in `Microsoft.Extensions.Configuration`, which wasn't referenced by that project. The simplest fix for a design-time-only file was to remove the dependency, not add one.

### D2. `BCrypt.HashPassword` — `CS0234: does not exist in the namespace 'BCrypt'`
**Problem**: `using BCrypt.Net;` then calling `BCrypt.HashPassword(...)` fails to compile.
**Fix**: Use an explicit alias:
```csharp
using BC = BCrypt.Net.BCrypt;
...
BC.HashPassword(password, WorkFactor);
```
**Why**: In this library the namespace and the class are both named `BCrypt` (full path `BCrypt.Net.BCrypt`), so the compiler resolves the identifier as a namespace, not the class. This is the officially documented workaround, not a hack.

### D3. MediatR license warning at startup
**Problem**: `You do not have a valid license key for the Lucky Penny software MediatR`.
**Fix**: None needed — it's informational only.
**Why**: MediatR became dual-licensed from v13 (2025): free for personal, educational, and small-revenue use; paid above that threshold. This project is inside the free tier. The check logs a warning only — no feature gating, no network call. (Pinning to v12.4.1, the last Apache-2.0 version, is a valid alternative.)

### D4. `BCrypt.Verify` throws on a malformed hash instead of returning false (M5)
**Problem**: Found during code review. `Verify` handed the stored hash straight to the library; a corrupt or foreign-format hash raises `SaltParseException`, which would have failed the whole login request rather than rejecting the credentials.
**Fix**: Wrapped the call in `try/catch (SaltParseException)` returning `false`.
**Why**: A verification function has exactly two correct answers. Any third outcome — an exception included — converts a rejected login into a server error, and hands the caller a distinction it should never see.
**Correction**: this entry was written before the change was applied. The file was not actually edited until later in M5, and nothing detected the gap. See G4.
**Repeat (M5.1)**: The original `catch (SaltParseException)` was too narrow. A unit test over five malformed shapes found two that escape it: an empty string raises `ArgumentException` from the library's own guard, and a truncated hash like `$2a$12$short` passes the version check then raises `ArgumentOutOfRangeException` from a `Substring` inside `HashPassword`. Added an early `IsNullOrWhiteSpace` guard and widened the catch to `SaltParseException or ArgumentException`, which covers the out-of-range subclass and any corrupt shape not yet seen. The lesson beneath the lesson: the first fix was verified by reading the code, which can only confirm the failure mode you already imagined — the shapes a corrupt row actually takes are found by trying them.

### D5. BCrypt silently ignores every byte past the first 72 (M5)
**Problem**: Found during code review. The algorithm truncates its input at 72 bytes with no error, so two long passwords sharing their first 72 bytes authenticate each other. `MaximumLength(72)` in the validator would not have closed the gap either — FluentValidation counts characters, and one Arabic character is two bytes.
**Fix**: Switched to `EnhancedHashPassword` / `EnhancedVerify`, which pre-hash the input so the limit no longer applies. Done before any real accounts existed, since the two formats are not interchangeable once hashes are stored.
**Why**: A silent limit is more dangerous than a hard one — nothing fails, so nothing gets investigated. And a rule expressed in characters says nothing about a limit measured in bytes.
**Correction**: as with D4, this entry preceded the change it describes. The switch to `EnhancedHashPassword` was applied later in M5, still before any real account existed — the claim in **Fix** holds, but only by luck of timing. See G4.

### D6. A value object must be compared whole inside an EF Core query (M5)
**Problem**: Found during code review, while adding the `Email` value object. With a value converter in place, EF Core sees one text column and knows nothing about the object's inner property — a query filtering on `u.Email.Value` compiles cleanly and fails at runtime.
**Fix**: Built the `Email` before the query and compared the whole object: `u.Email == normalized`.
**Why**: A value converter maps the type, not its members. Anything a query asks of the object beyond equality has no SQL to be translated into.
**Repeat (M5.2)**: The planned `AdminSeeder` filtered with `u.Email.Value == normalized` and re-implemented the trimming and lower-casing that `Email` already owns (A9). Caught while reviewing the plan, before the file was written. Corrected to build the `Email` first and compare the whole object.

### D7. A package version arrived through a dev-only dependency and stopped at the project boundary (M5.1)
**Problem**: `MSB3277` — conflicting versions of `Microsoft.EntityFrameworkCore.Relational`, 10.0.4 against 10.0.11 — appeared the moment `StudyHub.Infrastructure.Tests` was added. `StudyHub.Infrastructure` itself had built cleanly for weeks. `dotnet list package --include-transitive` showed 10.0.11 inside Infrastructure and 10.0.4 inside the test project, from the same graph.
**Fix**: Added an explicit `PackageReference` to `Microsoft.EntityFrameworkCore.Relational` in `StudyHub.Infrastructure`, without `PrivateAssets`, so it flows to consuming projects.
**Why**: The higher version was reaching Infrastructure only through `Microsoft.EntityFrameworkCore.Design`, which carries `PrivateAssets: all` and therefore does not cross a project reference. A project that uses a package's API — `HasCheckConstraint` and `HasFilter` come from Relational — must declare it; relying on a transitive path means the version is decided by a graph whose shape changes depending on who is looking at it.

### D8. An extension method was missing because of an absent assembly, not a missing `using` (M6)
**Problem**: `.Bind(configuration.GetSection(...))` on `OptionsBuilder<JwtSettings>` failed with CS1061 in `StudyHub.Infrastructure`, although the namespace was already imported and `GetSection` and `GetConnectionString` compiled in the same file.
**Fix**: Added `Microsoft.Extensions.Options.ConfigurationExtensions` to `StudyHub.Infrastructure`, matching the `10.0.x` family the other packages use.
**Why**: A `using` only surfaces what the project already references. EF Core drags `Configuration.Abstractions` in transitively — which is why the neighbouring configuration calls compiled — so an absent assembly reads exactly like a forgotten import. Two identical-looking failures, two different fixes.

### D9. A Fluent API call written into the plan had been removed from the provider (M6)
**Problem**: `builder.UseXminAsConcurrencyToken()` does not exist in `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3. It was obsoleted in version 7.0 in favour of the standard `IsRowVersion()` API, then deleted. Caught before the file was written, by checking the release notes of the installed version.
**Fix**: Configured a `uint` shadow property named `xmin`, typed `xid` and marked `IsRowVersion()`, in `RefreshTokenConfiguration`. The generated migration came out empty and `\d "RefreshTokens"` shows no added column, which is the intended result.
**Why**: A plan ages against the packages it names, and provider-specific APIs are the first to move. Read the release notes of the *installed* version before copying a Fluent API call — a documentation page that matches the method name may describe a version you are not running.

### D10. The validation pipeline silently skipped every command without a result (M6)
**Problem**: Found during code review. `ValidationBehavior` was constrained `where TRequest : IRequest<TResponse>`. In MediatR 12+, `IRequest` inherits only `IBaseRequest`, not `IRequest<Unit>`, so the container skipped the behaviour for `LogoutCommand`, `DeleteCourseCommand`, and `DeleteItemCommand`. `LogoutCommandValidator` never ran; `{"refreshToken": null}` would have reached `HashRefreshToken` as a 500.
**Fix**: Constraint changed to `where TRequest : notnull`. `ValidationBehaviorTests` sends an invalid `LogoutCommand` through a real service provider — it failed before the fix and passes after.
**Why**: Microsoft's container treats an open generic whose constraint does not match as "not registered", without an error. Handler tests call the handler directly and cannot see the pipeline, so a wiring rule needs a test that goes through the container.

---

## E. EF Core Migrations

### E1. `The name 'AddMissingForeignKeys' is used by an existing migration`
**Fix**: `dotnet ef migrations remove` to drop the earlier incomplete one, then re-add.
**Why**: A migration by that name had already been scaffolded in a previous attempt. Migration names must be unique across the project.

### E2. `PendingModelChangesWarning` on `database update`
**Problem**: After removing a migration, `database update` refused to run.
**Fix**: Re-create the migration (`migrations add`) before updating.
**Why**: Removing the migration left the C# model ahead of the last recorded snapshot. EF refuses to apply a schema it knows is already out of date rather than silently skipping the gap.

### E3. "An operation was scaffolded that may result in the loss of data"
**Fix**: Verified from the log that `InitialCreate` was being applied fresh — meaning the database was empty (the volume had been wiped earlier with `docker compose down -v`), so nothing could be lost.
**Why**: EF raises this defensively whenever a column changes shape, whether or not real data exists. On a database with real data, the migration file must be read manually before applying.

### E4. Each migration generates two files (plus a snapshot)
**Not a bug.** `X.cs` holds the actual `Up`/`Down` operations; `X.Designer.cs` is a model snapshot at that point in time, used by EF for future diffs. `StudyHubDbContextModelSnapshot.cs` is the current cumulative model. Never edit the last two by hand.

### E5. Collapsing migrations into one clean first migration (M5)
**Problem**: Planned in advance rather than hit at runtime. Two traps sit in the obvious approach: `migrations remove` refuses to drop a migration that is already applied, and deleting the migration files by hand leaves `StudyHubDbContextModelSnapshot.cs` behind — so the "clean" migration is scaffolded as a *diff* against the old model, full of `AlterColumn`, instead of a fresh set of `CreateTable`.
**Fix**: `docker compose down -v` first, then `migrations remove` once per migration, then confirmed `Migrations/` was completely empty before `migrations add InitialCreate`. Read the generated file and counted the `CreateTable` calls, with zero `AlterColumn`, before applying anything. Done twice — once for the schema fixes, once after the A15 restructure.
**Why**: EF diffs against the snapshot, not against the database. Deleting migration files without deleting the snapshot changes what is recorded, not what EF believes already exists.

### E6. A generated migration was scaffolded with an empty `Up` (M8.1)
**Problem**: `dotnet ef migrations add RemoveTokenCounterFromUsers` wrote its three files and updated the snapshot, but the `Up` body was empty — no `DropColumn` for `TokensUsedThisMonth` or `LastTokenResetDate`, although both properties had already been removed from `User`. Applying it as generated would have recorded the migration as done while changing nothing, leaving the database two columns ahead of the model.
**Fix**: The project owner wrote the two `DropColumn` calls by hand, applied the migration, and confirmed in `psql` that neither column remains on `Users` and that `__EFMigrationsHistory` lists it.
**Why**: EF scaffolds operations by diffing `StudyHubDbContextModelSnapshot.cs` against the current model — never the database against the model (E5). When the snapshot is already level with the model, that diff is empty and so is the migration, while the command still succeeds and still writes a plausible-looking file. A migration must be read before it is applied; an exit code says a file was written, not that the file does anything.

---

## F. Runtime & Environment

### F1. `SocketException (10061): target machine actively refused it` on port 5432
**Problem**: Every database call failed after a machine restart.
**Fix**: `docker compose up -d`, verified with `docker ps`.
**Why**: The Postgres container doesn't auto-start with the OS. Standard session start order: `docker compose up -d` → `dotnet run`.

### F2. Docker password changes don't take effect on an existing volume
**Problem**: Editing `POSTGRES_PASSWORD` in `docker-compose.yml` doesn't change the password of an already-initialized database.
**Fix**: `docker compose down -v` then `docker compose up -d` (destroys data — dev only).
**Why**: Postgres applies those environment variables only during first-time initialization of the data volume.

### F3. A pasted multi-line statement merged with the previous one in psql (M5)
**Problem**: An `INSERT` written to test `CK_Task_Status` returned `syntax error at or near "INTO"`, with a fragment of the *previous* query still visible in the error text. The statement never parsed, so the constraint was never exercised.
**Fix**: Re-sent the same `INSERT` on a single line; it was then correctly rejected by `CK_Task_Status`.
**Why**: At a glance the result looked like proof — a red error naming the right table and no row inserted, which is exactly what a working constraint produces. Read *which* error came back, not merely that one did.

### F4. A stale `@userId` in the `.http` file surfaced as a 500, not a 404 (M5.1)
**Problem**: Every `POST /api/tasks` returned 500 while verifying an unrelated date defect. The log showed `PostgresException 23503` — `FK_Items_Users_UserId`. The `@userId` variable still held an id from a database that had since been recreated, so `Guid.TryParse` passed, no handler asks whether the current user exists, and the failure surfaced only at save time.
**Fix**: Registered a fresh user and updated the variable. No code changed. Translating `23503` in `UnitOfWork` is scheduled in M6 step 6.4, alongside the concurrency-exception translation that touches the same method.
**Why**: Two causes met. A manual test file carries data-dependent state that expires silently when the database is reset — nothing in it fails loudly, the ids simply stop matching. And `UnitOfWork` translates only `23505`, so every other PostgreSQL error state arrives as a 500 that names nothing; the `SqlState` in the log is the first thing to read before suspecting the feature you just touched.

### F5. Four healthy source files were reported as binary by the repository export tool (M5.1)
**Not a bug.** All four are valid UTF-8 with a BOM — first bytes `EF BB BF`, no NUL bytes anywhere — verified by reading the bytes rather than trusting the label. `[Binary file]` came from the export tool's own detection, and it spread: two files carried the label from the start, and two more acquired it immediately after being edited, while their bytes stayed correct throughout. A re-save "fix" was performed on the first two and changed nothing, because nothing was wrong. A high proportion of Arabic comments was ruled out as the trigger — `ForbiddenException.cs` is 49% non-ASCII bytes and exports fine. The real cost was a diagnosis built on a tool's verdict instead of on the file, and two documents briefly recording a defect that never existed.

### F6. A key-generation command failed because the shell was an older PowerShell (M6)
**Problem**: `[System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48)` returned `MethodNotFound` under Windows PowerShell 5.1. The next command, `dotnet user-secrets set "Jwt:Key" $key`, then reported `Missing parameter value for 'value'` — an unrelated-looking message caused entirely by the first failure leaving `$key` unassigned.
**Fix**: Replaced the one-liner with a version-independent form — allocate a `byte[]`, fill it through `RandomNumberGenerator.Create().GetBytes($bytes)`, then Base64-encode it — and confirmed the result with `dotnet user-secrets list` instead of trusting the set command's exit.
**Why**: Windows PowerShell 5.1 runs on .NET Framework, which exposes only the instance method; the static overload arrived with .NET 6. A .NET API is reachable from a shell only through the runtime that shell was built on, so the class name resolving proves nothing about the method. And a failed assignment does not stop the script — it hands an empty value to the next command, which then fails for a reason that hides the real one.

### F7. The build failed because an API instance from an earlier session was still running (M7)
**Problem**: The first `dotnet build` of M7 failed with `MSB3021: Unable to copy file ... StudyHub.Infrastructure.dll ... being used by another process`, while `dotnet test` in the same run passed with 105 tests. The process holding the file was a `StudyHub.API` started the day before and never stopped; it also held port 5158.
**Fix**: Found the owner with `Get-NetTCPConnection -LocalPort 5158` and `Get-Process`, stopped it, rebuilt clean. The M7 manual run then started its own instance and stopped it afterwards.
**Why**: On Windows a running process locks its own DLLs, so only the project whose output it runs fails to build; the test projects build into other folders and stay green. A green `dotnet test` next to a red build is therefore a sign of a locked file, not of broken code. A server started for a manual check belongs to that check and should be stopped when it ends.
**Repeat (M8.1)**: The same lock, a day later: a `StudyHub.API` started on 18 September (the owner's first real Gemini call) still held port 5158, so the M8.1 baseline build failed with `MSB3027`/`MSB3021` while all 185 tests passed. Identified with `Get-NetTCPConnection -LocalPort 5158` and `Win32_Process` (its creation date and its path under `StudyHub.API/bin`), stopped, rebuilt clean. Checking the port before the first build is now Step 0 of every plan for this reason.

---

## G. Testing

### G1. `Should()` not found in `StudyHub.Domain.Tests`
**Fix**: `dotnet add StudyHub.Domain.Tests package FluentAssertions`.
**Why**: The package had been added to `Application.Tests` only, while the first test was written in `Domain.Tests`. Test projects don't share package references.

### G2. Test count higher than expected (3 instead of 2)
**Fix**: Deleted the template-generated `UnitTest1.cs` from `Application.Tests`.
**Why**: `dotnet test` runs every test project in the solution and reports a combined total. `dotnet new xunit` scaffolds a placeholder test that counts toward it.

### G3. `using Xunit;` shows as unused (greyed out)
**Not a bug.** Modern xUnit templates add `<Using Include="Xunit" />` to the `.csproj`, making it a global using. The explicit one is redundant and can be deleted.

### G4. A change was logged as done before it had been applied (M5)
**Problem**: `BCryptPasswordHasher.cs` was never edited, yet entries D4 and D5 already described the fix as complete. Nothing caught it: the build passed, all 43 tests passed, and the `.http` file still returned 201 — because no code path calls `Verify` yet and a stored hash looks identical either way.
**Fix**: Applied the change for real, wiped the database (old-format hashes are not verifiable by `EnhancedVerify`), corrected D4 and D5, and moved the two behavioural proofs into the M6 checklist.
**Why**: Every verification in use here is behavioural — a status code, a row, a green test. A change with no externally observable behaviour passes all of them unchanged. Such a change must be verified by opening the file and reading it; there is nothing else.

### G5. A destructive proof poisoned the account the next session depended on (M6)
**Problem**: Session B proved two refusal paths by corrupting a row directly on `login@test.com`: `IsActive = false`, then `PasswordHash = 'corrupt'`. The first was reverted, the second was not — `SELECT` showed `IsActive = t` beside a hash of `corrupt`. Session C reused the account, so its first request returned 401, and the two chained requests after it failed with `Unable to evaluate expression`, an error that points at the `.http` file rather than at the data.
**Fix**: Gave session C its own account, `rotate@test.com`, and left the damaged one damaged. The revert steps stay in the session B script but are no longer load-bearing.
**Why**: A proof that mutates shared state is a test without teardown. Reverting one of two mutations is the common case, not forgetting both: the first revert makes the cleanup feel finished. Two cheaper habits: one throwaway account per proof session, and reading the *first* red result rather than the noisiest one — the chained-request errors here were consequences, and chasing them would have cost an hour on a file that was correct.

---

## H. Leftover Scaffolding

### H1. Temporary code left in place after serving its purpose
**Items**: the `/weatherforecast` template endpoint, the `/db-check` connectivity probe, the `/ping` MediatR test (`PingQuery.cs`), and duplicate `using System;` blocks despite `ImplicitUsings` being enabled.
**Fix**: Deleted each once it had proven what it was written to prove.
**Why**: Temporary verification code is legitimate and useful — but it must be removed the moment it's served its purpose, or it becomes indistinguishable from real functionality.

### H2. A superseded EF Core configuration was left in place beside its replacement (M5.1)
**Problem**: Found during code review — no runtime error, no failing test. `UserConfiguration.cs` configured `User.Email` twice: the original block converting through `Email.Create`, and the block added in step 5.1.4 converting through `Email.FromPersisted`. Both compiled; the behaviour was correct only because EF Core lets the last call win.
**Fix**: Deleted the superseded block and kept `FromPersisted`, leaving one `Property(u => u.Email)` call in the file.
**Why**: A configuration API that overwrites silently turns leftover code into a correctness question decided by line order. Nothing in the suite guards it either — `EmailTests` exercises the value object directly and never travels through the converter, so reading the file was the only available proof (verification rule 3).

### H3. A one-time claims check was copied into every controller (M6)
**Problem**: Found during code review. `GET debug-claims`, written once to read the real claim names (Requirements §9.1), existed in all five controllers after the claim name was settled — five routes that returned a caller's token contents back to them.
**Fix**: Removed from `AuthController`, `CoursesController`, `ItemsController`, `NotesController`, and `TasksController`, with their now-unused `using` lines.
**Why**: A diagnostic that answered its question is dead code. Scaffolding copied instead of placed once multiplies the cleanup — and each copy is one more surface nobody remembers is there.

---

## Recurring Lessons

1. **"It builds" and "it runs" prove almost nothing.** Verify the specific effect: query `psql` for the FK, run the endpoint that touches the database, check `git ls-files` for the artifact.
2. **A command reporting success isn't proof it did anything** (C2 in particular).
3. **Drawings and documents enforce nothing** — an ERD is not a schema, a PRD is not a test.
4. **Fix the Domain before building on top of it.** A2 would have surfaced mid-handler in a later milestone at a far worse time.
5. **A silent limit is more dangerous than a hard one.** BCrypt truncating at 72 bytes, a `git rm` that removes nothing, a handler registered but never invoked — none of them raise anything to notice. The absence of a complaint is not a result.
6. **A command that failed is not proof that what you were testing failed** (F3). Test a rule by trying to break it *and* by sending something that should pass — one result without the other is half an answer.
7. **When every available design costs something real, question the requirement** (A15). The price is usually being paid for a contradiction in what was asked, not for the mechanism being built.
8. **The dependency rule is a constraint, not a document** (B8). Application has no reference to EF Core, so a repository implementation placed there cannot compile — the architecture caught the mistake instead of merely discouraging it.


