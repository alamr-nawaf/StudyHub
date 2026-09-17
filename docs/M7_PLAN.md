# M7 — Content completion (plan for Claude Code)

**Goal:** a user can read and edit their own content through the API: list courses, read a course's tree, list standalone items, read an item and its subtree, read their own profile, and update courses, items and tasks.

**Reference:** `docs/Requirements.md` §6, §8, §14.1, §14.2 and ADR-11, ADR-13, ADR-17, ADR-22, ADR-23, ADR-30.

## Decisions already taken by the project owner (do not reopen)

- Read queries go through read-side interfaces that return DTOs (§13 question 4).
- Reading another user's resource returns **403**, the same as writing it (§13 question 7).
- `GET /api/courses` returns **no** item counts (§13 question 3).
- `DeletedAt` + `DeletedBatchId` (ADR-25) is **deferred**. It is not part of M7.
- Moving the monthly token quota to configuration moves to **M8**.
- Enum values stay numbers in JSON, as in the existing requests. Do not change JSON serialization settings.
- No endpoint beyond the M7 list in Requirements §8.
- Do not touch authentication, login, refresh, logout, admin or seeding code, except adding `GET /api/auth/me`.

## How to work

- Do the steps in order. After each step, `dotnet build` and `dotnet test` must pass; then commit on `feature/m7-content` with a Conventional Commits message, for example `feat(items): add item read queries`.
- Write the test count printed by `dotnet test` into the report at Step 0 and after every step.
- If something is covered neither by this plan nor by Requirements, choose the simplest option that contradicts neither, and list it under "Decisions I made" in the report. Stop only for the stop conditions at the end of this file.

---

## Step 0 — Baseline

1. Create and switch to branch `feature/m7-content`.
2. Run `dotnet build` and `dotnet test`. Create `docs/M7_REPORT.md` and write the starting test count in it.

## Step 1 — Record the decisions (documents only, no code)

In `docs/Requirements.md`:

1. Add these rows at the end of the ADR table in §5:

```markdown
| **32** | **Read queries go through read-side interfaces that return DTOs (`ICourseQueries`, `IItemQueries`, `IUserQueries`); repositories stay write-side** | Queries must project with `Select` inside Infrastructure (ADR-13, §6). Keeping reads out of the repositories leaves each repository about loading entities for commands, and each query interface about shaping responses | One more interface and implementation per area. The EF projections are not covered by unit tests, because handlers mock the interface; until M10 the proof of a query is calling its endpoint |
| **33** | **Reading another user's resource returns 403, the same as writing it** | The write path already reveals existence through its 403, so a 404 on reads alone would hide nothing; one rule for both paths is simpler | A caller can tell that an id exists |
| **34** | **`GET /api/courses` returns no item counts** | No client needs them yet, and a count per course is easy to write as an N+1 | A client that wants counts reads the course tree. Counts are added when a consumer asks for them (A16) |
```

2. §13: remove questions 3, 4 and 7, and add them to the "Closed since v2.0" sentence as ADR-34, ADR-32 and ADR-33. The remaining questions keep their original numbers 1, 2, 5 and 6 (write them as `**5.**` and `**6.**` so Markdown does not renumber them), because other text refers to them by number.
3. Replace every sentence saying where read queries project "is open (§13)" or "is decided before M7" with a reference to ADR-32. Check `Requirements.md`, `ARCHITECTURE.md` and `CODING_STANDARDS.md`.
4. ADR-25: change "scheduled M7" to "deferred", and replace its last sentence ("Cheap only while no real data exists — this is why it is scheduled, not deferred indefinitely") with: "The project is not deployed, so no real data will ever make this change expensive; it is deferred rather than scheduled." Then change every other mention of `DeletedAt` / `DeletedBatchId` arriving "in M7" (in `Requirements.md`, `ARCHITECTURE.md`, `CODING_STANDARDS.md`) to say it is deferred (§12).
5. §11: remove "quota to configuration" and the `DeletedAt` + `DeletedBatchId` migration from the M7 row, and add "quota to configuration" to the M8 row. In `RegisterUserCommandHandler.cs`, change "M7" to "M8" in the comment.
6. §6 tree: add `Courses/Queries/GetCourseTree/ (M7)`. Keep the `(M7)` tags until Step 8.

In `docs/CODING_STANDARDS.md` §4, after the paragraph "Exception messages are English; comments are Arabic.", add: "Every class, record and interface starts with a short English `/// <summary>` that says what it is for. Classes written before M7 keep their existing comments."

## Step 2 — Shared pieces

- `StudyHub.Application/Common/Pagination/PagedResult.cs`: `record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)`. It serializes as `items`, `page`, `pageSize`, `totalCount` (§14.2).
- `StudyHub.Application/Common/Pagination/Paging.cs`: constants `DefaultPageSize = 20` and `MaxPageSize = 100`.
- `StudyHub.Application/Common/Validation/PagingRuleExtensions.cs`: page must be at least 1; page size must be between 1 and `MaxPageSize`. Same pattern as `PasswordRuleExtensions`.
- `StudyHub.Application/Common/Validation/UtcDateRuleExtensions.cs`: move the UTC rule out of `CreateTaskCommandValidator` (null passes, otherwise `Kind` must be `Utc`, same message) and use it there. The existing validator tests must stay green.

Paging rules for every paginated endpoint:

- Out-of-range values return 400. They are never clamped.
- Lists are ordered by `CreatedAt` descending, then `Id`, so pages are stable.
- A page past the end returns 200 with an empty `items`.

## Step 3 — Courses: list

- `StudyHub.Application/Courses/CourseDto.cs`: `CourseDto(Id, Title, Description, CreatedAt, UpdatedAt)`.
- `StudyHub.Application/Courses/CourseMappings.cs`: `ToDto(this IQueryable<Course>)`, a projection with `Select`. This is the ADR-17 extension method; because it extends `IQueryable`, a query never materializes an entity.
- `StudyHub.Application/Common/Interfaces/ICourseQueries.cs`:
  - `GetOwnerIdAsync(courseId)` returns `Guid?`: null when the course does not exist or is soft-deleted.
  - `GetPageAsync(userId, page, pageSize)` returns `PagedResult<CourseDto>` with that user's courses only.
- `StudyHub.Infrastructure/Data/Queries/CourseQueries.cs`, registered in the Infrastructure DI extension.
- `StudyHub.Application/Courses/Queries/GetCourses/`: query `(Page, PageSize)`, validator using the paging rules, handler using `ICurrentUserService`.
- Endpoint: `GET /api/courses?page=1&pageSize=20` returns 200 and `PagedResult<CourseDto>`. Query-string defaults come from `Paging`.
- Tests: handler success; handler passes the current user's id and never another; validator: page size 100 passes, 101 fails, page 0 fails.

## Step 4 — Items: read

- `StudyHub.Application/Items/ItemDto.cs`: `ItemDto(Id, ParentItemId, CourseId, Depth, Kind, Title, Content, Status, Priority, DueDate, CreatedAt, UpdatedAt)`. `Kind` is an `int` (0 = Note, 1 = Task, §3.1). `Status`, `Priority` and `DueDate` are null for notes.
- `StudyHub.Application/Items/ItemMappings.cs`: `ToDto(this IQueryable<Item>)`. `Kind` and the task fields come from the CLR type inside the projection (`i is TaskItem`, `((TaskItem)i).Status`), because `Kind` is a discriminator, not a property.
- `StudyHub.Application/Common/Interfaces/IItemQueries.cs`:
  - `GetOwnerIdAsync(itemId)` returns `Guid?`.
  - `GetByIdAsync(itemId)` returns `ItemDto?`.
  - `GetRootPageAsync(userId, page, pageSize)` returns that user's items with no parent **and** no course, paginated.
  - `GetSubtreeAsync(itemId)` returns the item and all its descendants.
  - `GetByCourseAsync(courseId)` returns every item of the course, at every depth.
- `StudyHub.Infrastructure/Data/Queries/ItemQueries.cs`, registered in DI.
- Trees are flat lists ordered by `Depth`, then `CreatedAt` (ADR-23), and are never paginated. `GetSubtreeAsync` walks level by level like `ItemRepository.GetSubtreeAsync`, at most five levels (ADR-11). A tree may take several statements; the number must not grow with the number of rows.
- Queries, one folder each:

| Folder | Endpoint | Returns |
|---|---|---|
| `Items/Queries/GetItem/` | `GET /api/items/{id}` | 200 + `ItemDto` |
| `Items/Queries/GetRootItems/` | `GET /api/items?page=1&pageSize=20` | 200 + `PagedResult<ItemDto>` |
| `Items/Queries/GetItemTree/` | `GET /api/items/{id}/tree` | 200 + array of `ItemDto` |
| `Courses/Queries/GetCourseTree/` | `GET /api/courses/{id}/tree` | 200 + array of `ItemDto` |

- The three single-resource handlers (GetItem, GetItemTree, GetCourseTree) check in this order: owner id is null → `NotFoundException` (404); owner is not the current user → `ForbiddenException` (403, ADR-33); only then fetch the data.
- Tests: each single-resource handler has success, 404 and 403. GetRootItems has success and "uses the current user's id". Its validator uses the paging rules already tested in Step 3, so it needs no new validator test.

## Step 5 — Current user

- `StudyHub.Application/Auth/Queries/GetCurrentUser/CurrentUserDto.cs`: `CurrentUserDto(Id, FullName, Email, Role, MonthlyTokenQuota, TokensUsedThisMonth)`.
- `StudyHub.Application/Common/Interfaces/IUserQueries.cs`: `GetCurrentAsync(userId)` returns `CurrentUserDto?`. `Email` is a value object stored through a converter: project the whole value and never filter on `Email.Value` (TROUBLESHOOTING D6).
- `StudyHub.Infrastructure/Data/Queries/UserQueries.cs`, registered in DI.
- Endpoint: `GET /api/auth/me` returns 200. It is protected by the fallback policy, so it gets no `[AllowAnonymous]`.
- Tests: success; missing user → 404.

## Step 6 — Update commands

Commands load the entity through the existing repositories, call its method, and save once (§6). Each endpoint returns 204.

| Endpoint | Command folder | Body | Entity method |
|---|---|---|---|
| `PUT /api/courses/{id}` | `Courses/Commands/UpdateCourse/` | `title`, `description` | `Course.UpdateDetails` |
| `PATCH /api/items/{id}` | `Items/Commands/UpdateItemContent/` | `title`, `content` | `Item.UpdateContent` |
| `PATCH /api/tasks/{id}/status` | `Tasks/Commands/UpdateTaskStatus/` | `status` | `TaskItem.UpdateStatus` |
| `PATCH /api/tasks/{id}/schedule` | `Tasks/Commands/UpdateTaskSchedule/` | `priority`, `dueDate` | `TaskItem.UpdateSchedule` |

- Each body replaces all of its fields. A null `description`, `content` or `dueDate` clears the value.
- Order of checks: missing or soft-deleted → 404; for the two task endpoints, an id that belongs to a note → 404; another owner → 403.
- No command changes `Kind` (rule 3.2.8).
- Controller pattern: bind the command from the body, then set the route id with `command with { Id = id }`. Never use an id taken from the body.
- Validators: course title `NotEmpty` + `MaximumLength(200)`, description `MaximumLength(2000)`; item title `NotEmpty` + `MaximumLength(250)`; `Status` and `Priority` `IsInEnum`; `DueDate` uses the UTC rule from Step 2 (§14.1).
- Tests: each handler has success, 404 and 403, and each failure test verifies `SaveChangesAsync` was never called. Both task handlers also test that a note id gives 404. The schedule validator tests UTC passes, Local fails, Unspecified fails and null passes.

## Step 7 — Manual requests and a real run

1. Append a section `M7` to `StudyHub.API/StudyHub.API.http` with one request per new endpoint. Use the `S1` token (`{{s.response.body.$.accessToken}}`) and `# @name` chaining for ids: create a course, a note under it, a task under the note, then read and update them.
2. With PostgreSQL running, start the API in the background and call every new endpoint once from the terminal. Use a throwaway user `m7@test.com` (a 409 on registration is fine; then log in). Create data, read it, update it, and read it again to see the change. Register a second throwaway user `m7-other@test.com` and use its token on one item endpoint to see 403. Stop the API afterwards.
3. Write every call into the report: method, route, expected status, actual status. A mismatch is a bug: fix it, add a TROUBLESHOOTING entry, and call again.
4. If the API cannot start because PostgreSQL is unreachable, do not try to fix the environment. Write it at the top of the report and stop.

## Step 8 — Documents and report

1. Remove the `(M7)` tags from everything that now exists (`Requirements.md` §6 and §8, `CODING_STANDARDS.md`, `ARCHITECTURE.md`). Move the M7 endpoints in Requirements §8 into the Implemented table with their auth and status codes.
2. Leave M7 as "In progress" in §11. The project owner marks it done after reviewing the report.
3. `README.md`: add the new endpoints to the endpoints table, and change "Next" to M8.
4. `ARCHITECTURE.md` §9: remove the debts this milestone closed ("No read queries", "No update handlers", "No DTOs").
5. Complete `docs/M7_REPORT.md` with these sections: Summary; Test count at Step 0 and at the end; Status of each step; Files added and changed; Endpoint calls (the table from Step 7); Decisions I made; Contradictions found; Problems and log entries written; Not done.

## Stop conditions

Stop, and write the reason at the top of the report, when:

- this plan contradicts `docs/Requirements.md` and the simplest option does not resolve it;
- a step seems to need a new NuGet package, a migration, raw SQL, or a change to authentication code;
- the build or the tests stay red after three honest attempts at the same problem.
