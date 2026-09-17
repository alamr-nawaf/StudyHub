# M7 — Content completion: report

## Test count

| When | Domain | Application | Infrastructure | Total |
|---|---|---|---|---|
| Step 0 (baseline) | 33 | 60 | 12 | **105** |
| Step 1 | 33 | 60 | 12 | **105** |
| Step 2 | 33 | 60 | 12 | **105** |
| Step 3 | 33 | 65 | 12 | **110** |

## Status of each step

| Step | Status | Commit |
|---|---|---|
| 0 — Baseline | Done. Build clean (0 warnings, 0 errors), 105 tests green | `chore: snapshot uncommitted M6 work before starting M7`, `docs(m7): start the M7 report` |
| 1 — Record the decisions | Done. ADR-32/33/34 added; §13 questions 3, 4, 7 closed; ADR-25 and every `DeletedAt` mention now say deferred; quota to configuration moved to M8; `GetCourseTree` added to §6; `/// <summary>` rule added to CODING_STANDARDS §4 | `docs: record M7 decisions (ADR-32 to ADR-34)` |
| 2 — Shared pieces | Done. `PagedResult<T>`, `Paging`, `PagingRuleExtensions`, `UtcDateRuleExtensions`; `CreateTaskCommandValidator` uses the shared UTC rule and its four UTC tests stay green | `feat(common): add pagination and shared validation rules` |
| 3 — Courses: list | Done. `CourseDto`, `CourseMappings.ToDto`, `ICourseQueries` + `CourseQueries`, `GetCourses` slice, `GET /api/courses`; 2 handler + 3 validator tests | `feat(courses): add course list query` |

## Decisions I made

1. **The uncommitted M6 work was committed as a separate baseline commit.** The session started on `Milestone6` with 23 modified and 18 untracked files (the administrator endpoint, seeding, `PasswordRuleExtensions`, `CLAUDE.md`, `docs/M7_PLAN.md`, …). Leaving them unstaged would have mixed them into the first M7 commit, and Step 1 edits `Requirements.md`, which already had pending changes. They are committed alone as `chore: snapshot uncommitted M6 work before starting M7`, so every later commit contains only M7 work. `.claude/` stayed untracked: it is local tool configuration.
2. **A stale API process was stopped.** `StudyHub.API` (started the previous day) held the build output and port 5158, so the first `dotnet build` failed with `MSB3021: … being used by another process`. Stopping it was needed both for the build and for Step 7.
