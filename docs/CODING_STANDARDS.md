# StudyHub — Engineering & Coding Standards

Rules for writing code in this repository. Where a rule exists because something went wrong once, the troubleshooting entry is referenced.

---

## 1. Architectural Principles

**Dependency rule**: source code dependencies point strictly inward, toward Domain.
`API → Infrastructure → Application → Domain`

**Layer isolation**: the Domain layer has zero dependencies on any external framework — not EF Core, not ASP.NET Core, not a NuGet package beyond the base class library.

**Persistence ignorance**: database concerns (table names, column types, max lengths, indexes, delete behaviour) never leak into domain entities. All EF Core configuration goes through the Fluent API in Infrastructure. Data Annotations on domain entities are prohibited.

**Interfaces are declared where they are needed; implementations live where their dependencies are permitted.** `IItemRepository` belongs in `Application/Common/Interfaces`; `ItemRepository` belongs in `Infrastructure/Data/Repositories`. Putting an implementation in Application does not compile, because that project cannot see `DbContext` (B8).

**Database-engine types stay in Infrastructure.** `PostgresException` and anything else from Npgsql must never appear in Application. Translating a Postgres error code into a domain exception happens in `UnitOfWork`, not in a handler.

---

## 2. C# & .NET 10 Standards

**Encapsulation**: domain entities protect their state. Use `private set` or `init`. Parameterless constructors required by EF Core are `private` or `protected`.

**Nullability**: nullable reference types are enabled globally. Mark nullable properties with `?` explicitly.

**Asynchronous programming**: all I/O-bound operations are fully asynchronous, suffixed `Async`, and always take a `CancellationToken` — including inside pipeline behaviours, which run on every single request.

**Time as a parameter**: a method whose behaviour depends on the current time takes `utcNow` as an argument (`ResetQuotaIfNeeded(DateTime utcNow)`, `RefreshToken.IsActive(DateTime utcNow)`). Methods that merely stamp `UpdatedAt` read `DateTime.UtcNow` directly. Do not inject a clock abstraction across every entity to buy testability nobody uses.

---

## 3. Naming Conventions

| Element | Convention |
|---|---|
| Classes, interfaces, records | PascalCase; interfaces prefixed `I` |
| Properties and methods | PascalCase |
| Local variables and parameters | camelCase |
| Private fields | `_camelCase` |
| Check constraints | `CK_{Entity}_{Rule}` — e.g. `CK_Item_RootDepth` |
| Migrations | describe the change: `AddValueConstraints`, not `Update3` |

**Never give a property the same name as a nullable version of its own type.** `public ParentType? ParentType` does not resolve; the compiler reads the identifier as the type. Identical types are fine (`public Email Email`), a nullable wrapper is not.

---

## 4. Domain Layer Rules

**Every entity is created through a static factory method** (`Note.Create`, `User.Create`) that validates its input and throws on violation. There is no public constructor.

**Entities enforce their own invariants and trust no caller.** `Item.Initialize` re-checks ownership, parent state, and depth even though the handler already checked them. A handler that forgets a check must not be able to corrupt data.

**Behaviour before columns.** An entity is written from what its use cases must *do*, not from what the ERD shows. A column with no method able to write it is decoration; an unexposed behaviour with private setters is not merely missing but impossible (A2).

**Inheritance follows lifecycle.** `BaseEntity` (`Id`, `CreatedAt`) is for everything; `AuditableEntity` adds `UpdatedAt` for entities that change. A write-once record inherits the former.

**Value objects for rules that would otherwise be duplicated.** `Email` exists because normalization was written in two layers, and a divergence there defeats a unique index (A9). Value objects are `record` types with private constructors and a static `Create`.

**Exception messages are English; comments are Arabic.** Messages reach the client and the logs; comments explain intent to the author. Comments state *why*, never *what* — the code already says what.

---

## 5. API & Error Handling

**RESTful compliance**: standard verbs and status codes — 201 Created, 204 No Content, 400, 403, 404, 409.

**Controllers are thin.** An action builds a command, sends it through MediatR, and maps the result. Three to five lines, always. No business logic, and no skipping a layer to reach Infrastructure directly.

**Centralized exception handling.** No `try/catch` in controllers for domain exceptions. Translation happens in `GlobalExceptionHandler`, implemented as `IExceptionHandler` with `AddProblemDetails()` — the modern ASP.NET Core replacement for hand-written middleware. All errors return RFC 9457 `ProblemDetails`.

| Exception | Status |
|---|---|
| `ValidationException` | 400, with errors grouped by field name |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| anything else | 500, with no internal detail in the response |

**Registering a handler is not wiring it.** `AddExceptionHandler<T>()` makes it available; `app.UseExceptionHandler()` makes it run. The same applies to authentication: pipeline order is `UseExceptionHandler → UseAuthentication → UseAuthorization → MapControllers`, and every wrong order still builds and starts (B1, B5).

**DTOs**: never return a domain entity from an endpoint. Map to a DTO through a static extension method (`item.ToDto()`).

**Never accept a user id from the request body.** Identity comes from `ICurrentUserService`. A command that carries a `UserId` field lets the client choose whose data to touch.

---

## 6. CQRS & MediatR Conventions

**Naming**: `{Verb}{Entity}Command` / `{Verb}{Entity}Query`, handlers suffixed `Handler`, validators suffixed `Validator`.

**One handler class = one use case.** No shared logic between handlers beyond what lives in Domain or a repository.

**Validators live in the same feature folder** as their command — never centralized in one file.

**The slice follows the use case, not the table.** `CreateNoteCommand` belongs under `Notes/`, even though it writes to the `Items` table through `IItemRepository`.

**A handler assumes its input is already valid.** Validation ran in the pipeline. Handlers check *state* (does the parent exist, is it owned) — never *shape*.

---

## 7. Persistence & EF Core

**Fluent API only**, one `IEntityTypeConfiguration<T>` file per entity.

**Add, not AddAsync.** The async overload matters only with database-generated value generators; ids are generated in the entity.

**A rule enforced in the domain is enforced in the database too.** Depth limits, enum ranges, and non-blank titles all exist in both places. Code protects the application path; a constraint protects every path.

**Check constraints that can evaluate to `NULL` pass.** Coalesce before testing: `COALESCE("Title", '') ~ '\S'`, not `"Title" IS NOT NULL` (A10).

**Presence is not validity.** A constraint asserting a column is non-null says nothing about whether its value is in range. Enum columns need both (A11).

**Indexes follow how a column is queried**, not whether it is a foreign key. A composite index already covers its leading column, so a separate index on that column is duplicate write cost (A13).

**Every column gets an explicit length** unless unbounded text is a deliberate decision that is written down. EF's default for silence is `text` (A14).

**Global query filters apply to LINQ only.** Raw SQL bypasses `!IsDeleted` entirely. If you write raw SQL, the filter is your responsibility.

**Migration discipline**: never edit `X.Designer.cs` or `StudyHubDbContextModelSnapshot.cs` by hand. EF diffs against the snapshot, not against the database — deleting migration files while leaving the snapshot produces a diff where you expected a fresh create (E5). Read every generated migration before applying it.

---

## 8. Testing Standards

**Naming**: `MethodName_Scenario_ExpectedResult`
e.g. `Create_UnderParentOfAnotherUser_ShouldThrow`

**Structure**: Arrange–Act–Assert, separated by blank lines, in every test.

**Mocking**: only interfaces declared in Application. Never mock the entity under test.

**Definition of done**: every new handler ships with at least one success test and at least one failure test.

**Assert on what did *not* happen too.** A failure test verifies that the exception was thrown *and* that `SaveChangesAsync` was never called — a handler that throws after writing is still a broken handler.

**Test the capability, not only the rule.** `Create_TaskUnderNote_ShouldSucceed` guards no invariant; it documents a deliberate design decision, and will fail loudly if someone later restricts the parent type.

---

## 9. Verification Discipline

Three rules, each learned from a specific failure.

**1. A successful build proves nothing.** Verify the specific effect: an HTTP status code from the `.http` file, a row in `psql`, a passing test. "It compiles" and "it starts" say nothing about wiring that nothing exercises yet (B1).

**2. Prove a constraint by breaking it *and* by inserting a row that should pass.** One without the other is half an answer. And read *which* constraint the error names — a malformed statement produces a red error and no inserted row, exactly like a working constraint does (F3).

**3. A change with no externally observable behaviour is verified by opening the file and reading it.** When no code path calls the changed method yet, the build passes, the tests pass, and every endpoint behaves identically whether or not the change was ever applied. Nothing else will catch it (G4).

---

## 10. Documentation Duties

**Log every problem.** After each problem solved, add one entry to `docs/TROUBLESHOOTING.md` following `docs/TROUBLESHOOTING_GUIDE.md`. Never guess an entry number or a milestone tag.

**A document that contradicts the code is worse than no document.** When a change makes a statement in `Requirements.md` or `ARCHITECTURE.md` false, fix it in the same session — not later.

**Never document a change as done before applying it.** Write the entry after the change exists and has been verified (G4).

**Record the cost of every architectural decision**, not just the choice. A decision without its trade-off written down looks like a mistake a year later.

---

## 11. Version Control Workflow

**Branching**: direct commits to `main` are prohibited. Use `feature/[name]`, `bugfix/[name]`, or `chore/[name]`.

**Commit messages**: Conventional Commits — `feat(domain): add item tree`, `fix(api): map ForbiddenException to 403`.

**Never commit a secret, and never leave a fallback that contains one.** Connection strings live in `dotnet user-secrets` for the running app and in `STUDYHUB_DB_CONNECTION` for design-time commands. A hardcoded default is how a password ends up permanently in git history (B2, B4).

**Review `git status` before staging.** `git add .` stages accidents and machine-local files indiscriminately (C4).

**Verify the effect of a git command, not its exit message.** `git rm -r --cached **/bin` reports success and removes nothing in `cmd.exe`, where `**` is not expanded (C2).
