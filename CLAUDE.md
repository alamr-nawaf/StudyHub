# StudyHub — instructions for Claude Code

A personal learning project: a .NET 10 backend API built with Clean Architecture, EF Core on PostgreSQL, MediatR and FluentValidation. It will not be deployed, but it will be demonstrated to a review committee, so the main features must work. The goal is a working program built cleanly, not maximum process.

**Talk to the user in English. Write code, comments, documents in English. Never commit to git**

## Sources of truth

- `docs/Requirements.md` is the only reference for what to build. If code or another document contradicts it, follow Requirements and list the contradiction in your report. Never pick silently.
- Follow `docs/CODING_STANDARDS.md`.
- The current milestone plan is the `docs/M*_PLAN.md` file you were given. Work on the branch it names.
- Do not create an endpoint that Requirements §8 does not list, and do not invent a behaviour the plan does not describe. If something is missing, choose the simplest option that contradicts neither, and record it in your report.

## Commands (from the repository root)

- Build: `dotnet build`
- Tests: `dotnet test` (no database needed)
- Run the API: `dotnet run --project StudyHub.API` (needs PostgreSQL: `docker compose up -d`). It listens on http://localhost:5158.
- The API reads its secrets from user-secrets by itself. Never read, print, set or remove user secrets.
- Stop any API process you started as soon as its check ends. A forgotten instance locks the build output and the port.

## Architecture rules

- Dependencies point inward: API → Infrastructure → Application → Domain. Application declares interfaces; Infrastructure implements them.
- Controllers are thin: build a command or query, send it through MediatR, return the result. No business logic in a controller, and nothing outside Infrastructure touches `StudyHubDbContext`.
- No Data Annotations on domain entities. EF Core configuration is Fluent API only.
- No new NuGet package unless the plan names it. If one seems necessary, stop and explain why the existing packages are not enough.
- No raw SQL beyond what the plan explicitly allows: it bypasses the soft-delete query filter.
- Never edit `*.Designer.cs` or `StudyHubDbContextModelSnapshot.cs`. No migrations unless the plan asks for one.

## Code conventions

- Every new class, record and interface starts with a short English `/// <summary>` that says what it is for. Test classes are exempt: their name already states what they test.
- Comments inside the code are English and explain *why*, only where the reason is not obvious. Code written before M8 keeps its existing Arabic comments; do not translate it.
- Exception messages are English.
- One use case = one command or query, its handler and its validator, in their own feature folder (CODING_STANDARDS §6).

## Tests (definition of done)

- Every new handler has at least one success test and one failure test. A handler with no failure path, such as a list query, gets a test proving it reads only the current user's data instead.
- A failure test for a command also verifies that `SaveChangesAsync` was never called.
- Every conditional validator rule (`When`, a `Must` with a null branch, a cross-field rule) has a test.
- Test names follow `MethodName_Scenario_ExpectedResult`; Arrange–Act–Assert separated by blank lines; mock only interfaces declared in Application.
- A step is done when `dotnet build` and `dotnet test` pass. The milestone is done only when every new endpoint has also been called against the running API, as the plan describes. A green build alone proves nothing.

## Documents

- A document that contradicts the code is worse than no document. When a step changes behaviour a document describes, fix the document in the same step.
- Finish the milestone by writing the report file the plan names.

## Problem log

After solving a real problem (an error you had to investigate, a wrong assumption, a request that returned the wrong status), add one entry to `docs/TROUBLESHOOTING.md` following `docs/TROUBLESHOOTING_GUIDE.md`. Read the last number of the section from the file; never guess it. A repeat of an existing entry is a `Repeat` line under that entry, not a new one.

## Safety

- Never write an API key, a connection string, a token or a password into a file, a document, a log line or the report — not even as an example.
- Never commit.
- Never run `docker compose down`: it can destroy the database volume.
- Do not change authentication, login, refresh, logout, admin or administrator-seeding code unless the plan says so.
