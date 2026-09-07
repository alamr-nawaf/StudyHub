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

### A3. `User.Create` had zero validation, unlike every other entity
**Problem**: All other factory methods validated their input; `User.Create` accepted anything. Email was also stored as typed, so `User@x.com` and `user@x.com` would register as two separate accounts despite the unique index.
**Fix**: Added guard clauses for name and email, and normalized email with `.Trim().ToLowerInvariant()`.
**Why**: Inconsistency crept in as entities were written at different times. Postgres unique indexes are case-sensitive by default — normalization has to be deliberate.

### A4. Documentation duplicated instead of referenced
**Problem**: `docs/Requirements` and `README.md` contained the same content verbatim, so editing one silently made the other wrong.
**Fix**: Split responsibilities — `Requirements.md` = what and why (PRD), `ARCHITECTURE.md` = how it works, `README.md` = how to run it.
**Why**: No single source of truth. Duplicated docs always drift.

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
