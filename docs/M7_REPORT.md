# M7 — Content completion: report

## Summary

M7 is implemented and verified; its roadmap status is **In progress**, waiting for the owner's review. A user can now list their courses, read a course's tree, list standalone items, read an item and its subtree, read their own profile (`GET /api/auth/me`), and update courses, item content, task status and task schedule. Reads go through three read-side interfaces that project to DTOs in SQL (ADR-32); another user's resource returns 403 on reads as on writes (ADR-33).

Every step from 0 to 8 is done, each in its own commit on `feature/m7-content`. The build has 0 warnings and 0 errors; tests went from **105 to 141**, all green. All ten new endpoints were called against the running API with PostgreSQL: **50 calls, 50 matched the expected status**, and each update was read back to confirm the change. No stop condition was reached: no new package, no migration, no raw SQL, and no authentication code changed apart from adding `GET /api/auth/me`.

## Test count at Step 0 and at the end

| When | Domain | Application | Infrastructure | Total |
|---|---|---|---|---|
| Step 0 (baseline) | 33 | 60 | 12 | **105** |
| Step 1 | 33 | 60 | 12 | **105** |
| Step 2 | 33 | 60 | 12 | **105** |
| Step 3 | 33 | 65 | 12 | **110** |
| Step 4 | 33 | 76 | 12 | **121** |
| Step 5 | 33 | 78 | 12 | **123** |
| Step 6 | 33 | 96 | 12 | **141** |
| Step 7 | 33 | 96 | 12 | **141** |
| Step 8 (end) | 33 | 96 | 12 | **141** |

## Status of each step

| Step | Status | Commit |
|---|---|---|
| 0 — Baseline | Done. Build clean (0 warnings, 0 errors), 105 tests green | `chore: snapshot uncommitted M6 work before starting M7`, `docs(m7): start the M7 report` |
| 1 — Record the decisions | Done. ADR-32/33/34 added; §13 questions 3, 4, 7 closed; ADR-25 and every `DeletedAt` mention now say deferred; quota to configuration moved to M8; `GetCourseTree` added to §6; `/// <summary>` rule added to CODING_STANDARDS §4 | `docs: record M7 decisions (ADR-32 to ADR-34)` |
| 2 — Shared pieces | Done. `PagedResult<T>`, `Paging`, `PagingRuleExtensions`, `UtcDateRuleExtensions`; `CreateTaskCommandValidator` uses the shared UTC rule and its four UTC tests stay green | `feat(common): add pagination and shared validation rules` |
| 3 — Courses: list | Done. `CourseDto`, `CourseMappings.ToDto`, `ICourseQueries` + `CourseQueries`, `GetCourses` slice, `GET /api/courses`; 2 handler + 3 validator tests | `feat(courses): add course list query` |
| 4 — Items: read | Done. `ItemDto`, `ItemMappings.ToDto`, `IItemQueries` + `ItemQueries`, `GetItem`, `GetRootItems`, `GetItemTree`, `GetCourseTree`; four endpoints; 11 handler tests | `feat(items): add item and tree read queries` |
| 5 — Current user | Done. `CurrentUserDto`, `IUserQueries` + `UserQueries` (Email projected whole, `.Value` read in memory, D6), `GetCurrentUser` slice, `GET /api/auth/me`; 2 handler tests | `feat(auth): add current user query` |
| 6 — Update commands | Done. `UpdateCourse`, `UpdateItemContent`, `UpdateTaskStatus`, `UpdateTaskSchedule` with validators; four endpoints returning 204, route id applied with `with { Id = id }`; 14 handler tests + 4 schedule validator tests | `feat(content): add update commands for courses, items and tasks` |
| 7 — Manual requests and a real run | Done. `M7` section (M7-1 … M7-18) appended to `StudyHub.API.http`; API started against PostgreSQL, 50 calls made, 50 matched the expected status; API stopped | `test(api): add M7 requests and record the real run` |
| 8 — Documents and report | Done. `(M7)` tags removed; the ten endpoints moved into the Implemented table of Requirements §8; §6 tree shows what now exists; M7 set to **In progress** in §11; README endpoints and "Next: M8"; three debts removed from ARCHITECTURE §9; this report completed | `docs(m7): update documents and complete the M7 report` |

## Files added and changed

Compared with the baseline commit `chore: snapshot uncommitted M6 work before starting M7`.

**Application — added**
- `Common/Pagination/PagedResult.cs`, `Common/Pagination/Paging.cs`
- `Common/Validation/PagingRuleExtensions.cs`, `Common/Validation/UtcDateRuleExtensions.cs`
- `Common/Interfaces/ICourseQueries.cs`, `IItemQueries.cs`, `IUserQueries.cs`
- `Courses/CourseDto.cs`, `Courses/CourseMappings.cs`
- `Courses/Queries/GetCourses/` — query, handler, validator
- `Courses/Queries/GetCourseTree/` — query, handler
- `Courses/Commands/UpdateCourse/` — command, handler, validator
- `Items/ItemDto.cs`, `Items/ItemMappings.cs`
- `Items/Queries/GetItem/` — query, handler
- `Items/Queries/GetItemTree/` — query, handler
- `Items/Queries/GetRootItems/` — query, handler, validator
- `Items/Commands/UpdateItemContent/` — command, handler, validator
- `Tasks/Commands/UpdateTaskStatus/` — command, handler, validator
- `Tasks/Commands/UpdateTaskSchedule/` — command, handler, validator
- `Auth/Queries/GetCurrentUser/` — `CurrentUserDto`, query, handler

**Application — changed**
- `Tasks/Commands/CreateTask/CreateTaskCommandValidator.cs` — uses `UtcOrNull()`
- `Users/Commands/RegisterUser/RegisterUserCommandHandler.cs` — comment: M7 → M8

**Infrastructure**
- Added `Data/Queries/CourseQueries.cs`, `ItemQueries.cs`, `UserQueries.cs`, `PagedQueryExtensions.cs`
- Changed `DependencyInjection/ServiceCollectionExtensions.cs` — registers the three query classes

**API**
- Changed `Controllers/AuthController.cs` (`GET me`), `CoursesController.cs` (`GET`, `GET {id}/tree`, `PUT {id}`), `ItemsController.cs` (`GET`, `GET {id}`, `GET {id}/tree`, `PATCH {id}`), `TasksController.cs` (`PATCH {id}/status`, `PATCH {id}/schedule`)
- Changed `StudyHub.API.http` — `M7` section

**Tests — added** (all in `StudyHub.Application.Tests`, test count in brackets)
- `Courses/Queries/GetCourses/GetCoursesQueryHandlerTests.cs` (2), `GetCoursesQueryValidatorTests.cs` (3)
- `Courses/Queries/GetCourseTree/GetCourseTreeQueryHandlerTests.cs` (3)
- `Items/Queries/GetItem/GetItemQueryHandlerTests.cs` (3)
- `Items/Queries/GetItemTree/GetItemTreeQueryHandlerTests.cs` (3)
- `Items/Queries/GetRootItems/GetRootItemsQueryHandlerTests.cs` (2)
- `Auth/Queries/GetCurrentUser/GetCurrentUserQueryHandlerTests.cs` (2)
- `Courses/Commands/UpdateCourse/UpdateCourseCommandHandlerTests.cs` (3)
- `Items/Commands/UpdateItemContent/UpdateItemContentCommandHandlerTests.cs` (3)
- `Tasks/Commands/UpdateTaskStatus/UpdateTaskStatusCommandHandlerTests.cs` (4)
- `Tasks/Commands/UpdateTaskSchedule/UpdateTaskScheduleCommandHandlerTests.cs` (4), `UpdateTaskScheduleCommandValidatorTests.cs` (4)

**Documents**
- Added `docs/M7_REPORT.md`
- Changed `docs/Requirements.md`, `docs/ARCHITECTURE.md`, `docs/CODING_STANDARDS.md`, `docs/TROUBLESHOOTING.md`, `README.md`

## Endpoint calls

Run on 2026-09-17 against `http://localhost:5158` with PostgreSQL in Docker. Throwaway users: `m7@test.com` and `m7-other@test.com` (both registered fresh, 201). Every call was made from a script in the terminal; ids are replaced by names below.

| # | Call | Method | Route | Expected | Actual | Result |
|---|---|---|---|---|---|---|
| 1 | register m7@test.com | POST | `/api/auth/register` | 201/409 | 201 | OK |
| 2 | login m7@test.com | POST | `/api/auth/login` | 200 | 200 | OK |
| 3 | register m7-other@test.com | POST | `/api/auth/register` | 201/409 | 201 | OK |
| 4 | login m7-other@test.com | POST | `/api/auth/login` | 200 | 200 | OK |
| 5 | me | GET | `/api/auth/me` | 200 | 200 | OK |
| 6 | me without token | GET | `/api/auth/me` | 401 | 401 | OK |
| 7 | create course | POST | `/api/courses` | 201 | 201 | OK |
| 8 | note under course | POST | `/api/notes` | 201 | 201 | OK |
| 9 | task under note | POST | `/api/tasks` | 201 | 201 | OK |
| 10 | standalone note | POST | `/api/notes` | 201 | 201 | OK |
| 11 | list courses | GET | `/api/courses?page=1&pageSize=20` | 200 | 200 | OK |
| 12 | list courses defaults | GET | `/api/courses` | 200 | 200 | OK |
| 13 | page size 101 | GET | `/api/courses?page=1&pageSize=101` | 400 | 400 | OK |
| 14 | page 0 | GET | `/api/courses?page=0` | 400 | 400 | OK |
| 15 | page past the end | GET | `/api/courses?page=999&pageSize=20` | 200 | 200 | OK |
| 16 | page 2147483647 (overflow guard) | GET | `/api/courses?page=2147483647&pageSize=100` | 200 | 200 | OK |
| 17 | course tree | GET | `/api/courses/{courseId}/tree` | 200 | 200 | OK |
| 18 | root items | GET | `/api/items?page=1&pageSize=20` | 200 | 200 | OK |
| 19 | get task | GET | `/api/items/{taskId}` | 200 | 200 | OK |
| 20 | get note | GET | `/api/items/{noteId}` | 200 | 200 | OK |
| 21 | note tree | GET | `/api/items/{noteId}/tree` | 200 | 200 | OK |
| 22 | update course (body id ignored) | PUT | `/api/courses/{courseId}` | 204 | 204 | OK |
| 23 | update note content | PATCH | `/api/items/{noteId}` | 204 | 204 | OK |
| 24 | update task status | PATCH | `/api/tasks/{taskId}/status` | 204 | 204 | OK |
| 25 | update task schedule | PATCH | `/api/tasks/{taskId}/schedule` | 204 | 204 | OK |
| 26 | schedule with +03:00 | PATCH | `/api/tasks/{taskId}/schedule` | 400 | 400 | OK |
| 27 | schedule without offset | PATCH | `/api/tasks/{taskId}/schedule` | 400 | 400 | OK |
| 28 | status 99 | PATCH | `/api/tasks/{taskId}/status` | 400 | 400 | OK |
| 29 | status on a note id | PATCH | `/api/tasks/{noteId}/status` | 404 | 404 | OK |
| 30 | schedule on a note id | PATCH | `/api/tasks/{noteId}/schedule` | 404 | 404 | OK |
| 31 | blank item title | PATCH | `/api/items/{noteId}` | 400 | 400 | OK |
| 32 | list courses after update | GET | `/api/courses` | 200 | 200 | OK |
| 33 | course tree after update | GET | `/api/courses/{courseId}/tree` | 200 | 200 | OK |
| 34 | get task after update | GET | `/api/items/{taskId}` | 200 | 200 | OK |
| 35 | clear due date | PATCH | `/api/tasks/{taskId}/schedule` | 204 | 204 | OK |
| 36 | get task after clearing | GET | `/api/items/{taskId}` | 200 | 200 | OK |
| 37 | other user reads item | GET | `/api/items/{taskId}` | 403 | 403 | OK |
| 38 | other user reads item tree | GET | `/api/items/{noteId}/tree` | 403 | 403 | OK |
| 39 | other user reads course tree | GET | `/api/courses/{courseId}/tree` | 403 | 403 | OK |
| 40 | other user updates course | PUT | `/api/courses/{courseId}` | 403 | 403 | OK |
| 41 | other user updates item | PATCH | `/api/items/{noteId}` | 403 | 403 | OK |
| 42 | other user updates status | PATCH | `/api/tasks/{taskId}/status` | 403 | 403 | OK |
| 43 | other user updates schedule | PATCH | `/api/tasks/{taskId}/schedule` | 403 | 403 | OK |
| 44 | other user root items exclude mine | GET | `/api/items` | 200 | 200 | OK |
| 45 | missing item | GET | `/api/items/{missingId}` | 404 | 404 | OK |
| 46 | missing item tree | GET | `/api/items/{missingId}/tree` | 404 | 404 | OK |
| 47 | missing course tree | GET | `/api/courses/{missingId}/tree` | 404 | 404 | OK |
| 48 | update missing course | PUT | `/api/courses/{missingId}` | 404 | 404 | OK |
| 49 | delete standalone note | DELETE | `/api/items/{standaloneId}` | 204 | 204 | OK |
| 50 | read deleted note | GET | `/api/items/{standaloneId}` | 404 | 404 | OK |

What the bodies showed, beyond the status codes:

- **Updates are visible on the next read.** After the updates, `GET /api/courses` showed `title = "M7 course (renamed)"`, `description = null` and `updatedAt` set; the course tree showed the note as `"M7 note (edited)"` with its new content and the task with `status = 2`, `priority = 2`, `dueDate = "2026-10-01T14:00:00Z"`. Sending `dueDate: null` then cleared it (`priority = 0`, `dueDate = null`).
- **The route id wins over the body id.** Call 22 sent a random `id` in the body; the course at the route id is the one that changed.
- **Trees are flat and ordered.** Both trees returned the note (`depth 0`, `kind 0`, task fields null) and then the task (`depth 1`, `kind 1`).
- **The standalone list excludes items under a course.** `GET /api/items` returned only `M7 standalone`; for the second user it returned `totalCount = 0`.
- **Pagination.** Defaults (no query string) returned `page 1`, `pageSize 20`. A page past the end and `page = 2147483647` both returned 200 with empty `items` and the real `totalCount`.
- **`GET /api/auth/me`** returned `id`, `fullName`, `email = "m7@test.com"`, `role = 0`, `monthlyTokenQuota = 100000`, `tokensUsedThisMonth = 0`.
- **The projections run in SQL.** The EF Core command log shows the `ItemDto` projection as `CASE WHEN i."Kind" = 1 THEN i."Status" END` and friends, with no entity columns selected. A course tree is one statement (`WHERE "CourseId" = @courseId ORDER BY "Depth", "CreatedAt", "Id"`); an item tree is one statement for the root and one per level (`"ParentItemId" = ANY (@currentLevel)`), so the count follows the depth, not the number of rows.

## Decisions I made

1. **The uncommitted M6 work was committed as a separate baseline commit.** The session started on `Milestone6` with 23 modified and 18 untracked files (the administrator endpoint, seeding, `PasswordRuleExtensions`, `CLAUDE.md`, `docs/M7_PLAN.md`, …). Leaving them unstaged would have mixed them into the first M7 commit, and Step 1 edits `Requirements.md`, which already had pending changes. They are committed alone as `chore: snapshot uncommitted M6 work before starting M7`, so every later commit contains only M7 work. `.claude/` stayed untracked: it is local tool configuration.
2. **A stale API process was stopped.** `StudyHub.API` (started the previous day) held the build output and port 5158, so the first `dotnet build` failed with `MSB3021`. Stopping it was needed for the build and for Step 7 (log entry F7).
3. **Paging arithmetic is done in `long`, and a page past the end skips the second query.** `(page - 1) * pageSize` overflows `int` for a very large `page` and would reach EF as a negative offset (a 500). `PagedQueryExtensions` counts first, returns an empty page when the offset is at or past `totalCount`, and only otherwise runs `Skip`/`Take`. Proven by call 16 (`page = 2147483647` → 200, empty `items`).
4. **One shared `ToPagedResultAsync` in Infrastructure.** Both paginated queries use it, so the count, the offset and the empty-page rule exist once. It is `internal`: nothing outside Infrastructure needs it.
5. **The subtree walk is bounded by the root's depth.** `ItemQueries.GetSubtreeAsync` loops from `root.Depth` while `depth < Item.MaxDepth`, so a root at depth 0 costs at most 5 statements (root + 4 levels) and a deeper root costs fewer. `ItemRepository.GetSubtreeAsync` allows one extra, always-empty round; it was left unchanged, because commands are outside this milestone.
6. **Ordering happens before `ToDto()`.** EF cannot translate an `OrderBy` on a member of a DTO built through its constructor, so every query orders the entity query and then projects. Within a tree, each level is ordered by `CreatedAt, Id` and levels are appended in order, which gives `Depth, CreatedAt` without sorting in memory.
7. **`Kind` values are named constants**, `ItemMappings.NoteKind = 0` and `TaskKind = 1`, used by the projection and the tests.
8. **DTO enum fields keep their enum types** (`StudyTaskStatus?`, `TaskPriority?`, `UserRole`). They serialize as numbers with the unchanged JSON settings, as the plan requires; `Kind` is an `int` as specified.
9. **`GetItem` re-checks for null after the ownership check.** The owner lookup and the DTO fetch are two statements; an item deleted between them gives 404, not a `NullReferenceException`.
10. **`UserQueries` projects `Email` whole and reads `.Value` in memory** (D6), into an anonymous row and then `CurrentUserDto` with `Email` as a plain string, so the JSON shows `"email": "m7@test.com"` rather than a nested object.
11. **`GET /api/auth/me` does not check `IsActive`.** A deactivated user whose access token is still valid can read their own profile for up to 15 minutes, which is the lag §9.4 already accepts. Nothing in the plan or Requirements asks for more.
12. **Paging values are plain action parameters** (`int page = 1, int pageSize = Paging.DefaultPageSize`). A non-numeric value is rejected by model binding with 400 before MediatR.
13. **`UtcOrNull()` keeps the original message**, which names `DueDate` literally, as Step 2 asked ("same message"). A future date field other than `DueDate` would need the message to become a parameter.
14. **Documents beyond the literal plan, to avoid a contradiction with the code** (CODING_STANDARDS §10): Requirements §6 now lists the query interfaces, `Pagination/`, the two new rule extensions and the DTO and mapping files; §8 gained two short paragraphs (update bodies replace every field and ignore a body `id`; enums are numbers, task fields are null for notes); CODING_STANDARDS §5 now shows `items.ToDto()` on `IQueryable` instead of `item.ToDto()` on an entity; ARCHITECTURE §9 says the quota moves to configuration in M8; §13 now says "None blocks M7" instead of the stale "None blocks M6".
15. **The M7 row in §11 was set to "In progress".** Step 8 says to *leave* it "In progress", but it still said "Pending".
16. **The real run made 50 calls, not one per endpoint.** Besides one success call per endpoint, it checks 403 for the second user on every single-resource endpoint (the plan asked for one), 404s, validation 400s, a note id on both task routes, clearing a due date, a body `id` being ignored, and a deleted item returning 404.

## Contradictions found

1. **CLAUDE.md vs the plan, on list handlers.** CLAUDE.md requires every new handler to have "at least one success test and one failure test". The plan asks `GetCourses` and `GetRootItems` for a success test and a "uses the current user's id" test. These handlers have no failure path: they throw nothing, and invalid paging is rejected by the validator before they run. I followed the plan. The second test is the negative one: it verifies that no other user id is ever passed.
2. **Requirements §13 said "None blocks M6"** while M6 was already done. Changed to M7 (decision 14).
3. **CODING_STANDARDS §5 showed `item.ToDto()` on an entity**, which contradicts ADR-32, §6 ("a query never materializes an entity") and the plan's `IQueryable` extension. Corrected (decision 14).
4. **Requirements §11 had M7 as "Pending"**, while plan Step 8 speaks of leaving it "In progress" (decision 15).
5. **README still promised the `DeletedAt` + `DeletedBatchId` migration for M7** after Step 1 deferred it. The plan fixes README only in Step 8, so it was out of date between Steps 1 and 8; it is correct now.

No contradiction between the plan and `docs/Requirements.md` needed a stop.

## Problems and log entries written

- **F7 (M7), new.** The first build failed with `MSB3021` because an API instance from an earlier session locked `StudyHub.Infrastructure.dll` and port 5158, while `dotnet test` passed. Found the process through the port owner, stopped it, and rebuilt.
- **Not logged — a tooling slip, not a project problem.** Two long shell commands that wrote several files failed to parse (`unexpected EOF while looking for matching quote`), so nothing was written; the files were then written one by one. Nothing in the repository was affected.
- **No endpoint mismatch.** All 50 calls returned the expected status on the first run, so no bug fix and no further log entry were needed.

## Not done

- **M7 is not marked done.** It is "In progress" in §11 until the owner reviews this report.
- **The `.http` M7 section was written but not sent through the REST Client.** The same flow was run from a script in the terminal (see Endpoint calls). The file's requests depend on `S1` and, for M7-18, on `S3` (`other@test.com`), whose account ADM6 may have deactivated (G5).
- **No `psql` check.** The run verified results through the API and the EF command log only. Every write was read back through a query endpoint, but the soft delete of the standalone note was confirmed only as a 404, not as a row with `IsDeleted = true`.
- **A missing enum field in an update body is not rejected.** `status` and `priority` are non-nullable enums, so a body without them binds as `0` (Pending / Low) and passes `IsInEnum`. That fits "each body replaces all of its fields" but can silently reset a value. Making the fields nullable with `NotNull()` would close it; the plan did not ask for it.
- **The read-side EF queries have no automated test.** As ADR-32 records, the handlers mock the interfaces; until M10 the proof is the endpoint calls above.
- **Test data left in the database:** users `m7@test.com` and `m7-other@test.com`, one course, one note, one task, and one soft-deleted standalone note.
- **Nothing was pushed**, and `.claude/` remains untracked.
