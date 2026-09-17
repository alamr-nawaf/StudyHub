# M7 — Content completion: report

## Test count

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
- **The route id wins over the body id.** Call 25 sent a random `id` in the body; the course at the route id is the one that changed.
- **Trees are flat and ordered.** Both trees returned the note (`depth 0`, `kind 0`, task fields null) and then the task (`depth 1`, `kind 1`).
- **The standalone list excludes items under a course.** `GET /api/items` returned only `M7 standalone`; for the second user it returned `totalCount = 0`.
- **Pagination.** Defaults (no query string) returned `page 1`, `pageSize 20`. A page past the end and `page = 2147483647` both returned 200 with empty `items` and the real `totalCount`.
- **`GET /api/auth/me`** returned `id`, `fullName`, `email = "m7@test.com"`, `role = 0`, `monthlyTokenQuota = 100000`, `tokensUsedThisMonth = 0`.
- **The projections run in SQL.** The EF Core command log shows the `ItemDto` projection as `CASE WHEN i."Kind" = 1 THEN i."Status" END` and friends, with no entity columns selected. A course tree is one statement (`WHERE "CourseId" = @courseId ORDER BY "Depth", "CreatedAt", "Id"`); an item tree is one statement for the root and one per level (`"ParentItemId" = ANY (@currentLevel)`), so the count follows the depth, not the number of rows.

## Decisions I made

1. **The uncommitted M6 work was committed as a separate baseline commit.** The session started on `Milestone6` with 23 modified and 18 untracked files (the administrator endpoint, seeding, `PasswordRuleExtensions`, `CLAUDE.md`, `docs/M7_PLAN.md`, …). Leaving them unstaged would have mixed them into the first M7 commit, and Step 1 edits `Requirements.md`, which already had pending changes. They are committed alone as `chore: snapshot uncommitted M6 work before starting M7`, so every later commit contains only M7 work. `.claude/` stayed untracked: it is local tool configuration.
2. **A stale API process was stopped.** `StudyHub.API` (started the previous day) held the build output and port 5158, so the first `dotnet build` failed with `MSB3021: … being used by another process`. Stopping it was needed both for the build and for Step 7.
