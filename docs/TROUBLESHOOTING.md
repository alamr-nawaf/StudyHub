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

### D5. BCrypt silently ignores every byte past the first 72 (M5)
**Problem**: Found during code review. The algorithm truncates its input at 72 bytes with no error, so two long passwords sharing their first 72 bytes authenticate each other. `MaximumLength(72)` in the validator would not have closed the gap either — FluentValidation counts characters, and one Arabic character is two bytes.
**Fix**: Switched to `EnhancedHashPassword` / `EnhancedVerify`, which pre-hash the input so the limit no longer applies. Done before any real accounts existed, since the two formats are not interchangeable once hashes are stored.
**Why**: A silent limit is more dangerous than a hard one — nothing fails, so nothing gets investigated. And a rule expressed in characters says nothing about a limit measured in bytes.

### D6. A value object must be compared whole inside an EF Core query (M5)
**Problem**: Found during code review, while adding the `Email` value object. With a value converter in place, EF Core sees one text column and knows nothing about the object's inner property — a query filtering on `u.Email.Value` compiles cleanly and fails at runtime.
**Fix**: Built the `Email` before the query and compared the whole object: `u.Email == normalized`.
**Why**: A value converter maps the type, not its members. Anything a query asks of the object beyond equality has no SQL to be translated into.

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

### E5. Collapsing two migrations into one clean first migration (M5)
**Problem**: Planned in advance rather than hit at runtime. Two traps sit in the obvious approach: `migrations remove` refuses to drop a migration that is already applied, and deleting the migration files by hand leaves `StudyHubDbContextModelSnapshot.cs` behind — so the "clean" migration is scaffolded as a *diff* against the old model, full of `AlterColumn`, instead of a fresh set of `CreateTable`.
**Fix**: `docker compose down -v` first, then `migrations remove` twice, then confirmed `Migrations/` was completely empty before `migrations add InitialCreate`. Read the generated file and counted six `CreateTable` calls and zero `AlterColumn` before applying anything.
**Why**: EF diffs against the snapshot, not against the database. Deleting migration files without deleting the snapshot changes what is recorded, not what EF believes already exists.

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

---

## H. Leftover Scaffolding

### H1. Temporary code left in place after serving its purpose
**Items**: the `/weatherforecast` template endpoint, the `/db-check` connectivity probe, the `/ping` MediatR test (`PingQuery.cs`), and duplicate `using System;` blocks despite `ImplicitUsings` being enabled.
**Fix**: Deleted each once it had proven what it was written to prove.
**Why**: Temporary verification code is legitimate and useful — but it must be removed the moment it's served its purpose, or it becomes indistinguishable from real functionality.

---

## Recurring Lessons

1. **"It builds" and "it runs" prove almost nothing.** Verify the specific effect: query `psql` for the FK, run the endpoint that touches the database, check `git ls-files` for the artifact.
2. **A command reporting success isn't proof it did anything** (C2 in particular).
3. **Drawings and documents enforce nothing** — an ERD is not a schema, a PRD is not a test.
4. **Fix the Domain before building on top of it.** A2 would have surfaced mid-handler in a later milestone at a far worse time.
5. **A silent limit is more dangerous than a hard one.** BCrypt truncating at 72 bytes, a `git rm` that removes nothing, a handler registered but never invoked — none of them raise anything to notice. The absence of a complaint is not a result.
6. **A command that failed is not proof that what you were testing failed** (F3). Test a rule by trying to break it *and* by sending something that should pass — one result without the other is half an answer.
