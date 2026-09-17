# StudyHub — instructions for Claude Code

A personal learning project: a .NET 10 backend API built with Clean Architecture, EF Core on PostgreSQL, MediatR and FluentValidation. It will not be deployed. The goal is a working program built cleanly, not maximum process.

Talk to the user in simple Arabic. Write code, documents and commit messages in English.

## Sources of truth

- `docs/Requirements.md` is the only reference for what to build. If code or another document contradicts it, follow Requirements and list the contradiction in your report. Never pick silently.
- Follow `docs/CODING_STANDARDS.md`.
- The current milestone plan is `docs/M7_PLAN.md`.

## Commands (from the repository root)

- Build: `dotnet build`
- Tests: `dotnet test` (no database needed)
- Run the API: `dotnet run --project StudyHub.API` (needs PostgreSQL: `docker compose up -d`). It listens on http://localhost:5158.
- The API reads its connection string and JWT key from user-secrets by itself. Never read, print, set or remove user secrets.

## Architecture rules

- Dependencies point inward: API → Infrastructure → Application → Domain. Application declares interfaces; Infrastructure implements them.
- Controllers are thin: build a command or query, send it through MediatR, return the result. No business logic in a controller, and nothing outside Infrastructure touches `StudyHubDbContext`.
- No Data Annotations on domain entities. EF Core configuration is Fluent API only.
- No new NuGet package. If one seems necessary, stop and explain why the existing packages are not enough.
- No raw SQL (`FromSql`, `ExecuteSql`, `WITH RECURSIVE`): it bypasses the soft-delete query filter.
- Never edit `*.Designer.cs` or `StudyHubDbContextModelSnapshot.cs`. No migrations unless the plan asks for one.

## Code conventions

- Every new class, record and interface starts with a short English `/// <summary>` that says what it is for.
- Comments inside the code explain *why*, in Arabic, and only where the reason is not obvious. Exception messages are English.
- One use case = one command or query, its handler and its validator, in their own feature folder (CODING_STANDARDS §6).

## Tests (definition of done)

- Every new handler has at least one success test and one failure test.
- A failure test for a command also verifies that `SaveChangesAsync` was never called.
- Every conditional validator rule (`When`, a `Must` with a null branch, a cross-field rule) has a test.
- Test names follow `MethodName_Scenario_ExpectedResult`; Arrange–Act–Assert separated by blank lines; mock only interfaces declared in Application.
- A step is done when `dotnet build` and `dotnet test` pass. The milestone is done only when every new endpoint has also been called against the running API (see the plan). A green build alone proves nothing.

## Problem log

After solving a real problem (an error you had to investigate, a wrong assumption, a request that returned the wrong status), add one entry to `docs/TROUBLESHOOTING.md` following `docs/TROUBLESHOOTING_GUIDE.md`. Read the last number of the section from the file; never guess it.

## Safety

- Work on branch `feature/m7-content`. Never commit to `main`. Never push.
- Never run `docker compose down`: it can destroy the database volume.
- Do not change authentication, login, refresh, logout, admin or administrator-seeding code unless the plan says so.
