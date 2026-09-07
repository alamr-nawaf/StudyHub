# How to Write a Troubleshooting Entry

The convention for `docs/TROUBLESHOOTING.md`. Follow it exactly so the log stays scannable as it grows.

---

## Entry format

```markdown
### X#. Short title describing the problem (Mx)
**Problem**: What actually happened — error text, HTTP code, unexpected behavior.
**Fix**: What was done, and which files changed.
**Why**: The root cause, not the symptom.
```

Three fields. Always named `Problem`, `Fix`, `Why` — no variations.

If it turned out not to be a bug, replace all three with a single line starting `**Not a bug.**` followed by the explanation.

---

## Sections

Entries are grouped by **type**, not by date:

| | |
|---|---|
| **A** | Design Flaws — wrong by design, not by accident |
| **B** | Configuration & Wiring |
| **C** | Git |
| **D** | Packages & Libraries |
| **E** | EF Core Migrations |
| **F** | Runtime & Environment |
| **G** | Testing |
| **H** | Leftover Scaffolding |

Don't invent a new section unless nothing fits. Numbering runs per section (`A1`, `A2`, ...), is sequential, and numbers are never reused.

---

## Milestone tag

Every new entry ends its heading with the milestone it occurred in:

```markdown
### B4. JWT key read as null from configuration (M5)
```

That single tag is the only time-tracking in the file — no dates, no timestamps. Entries without a tag predate this convention and stay as they are.

---

## Rules

1. **One problem, one entry.** Three problems in one session means three entries.
2. **Document review findings too.** If it was caught by reading code rather than by an error at runtime, say so in `Problem`: *"Found during code review — no runtime error."*
3. **`Why` must generalize.** It should be a rule that applies to other situations, not a restatement of the fix.
   - Good: *"An ERD is a drawing — it enforces nothing."*
   - Bad: *"We added the Fluent API config."*
4. **Keep each field to 1–3 lines.** This is a reference log, not an article.
5. **Repeats don't get new entries.** Add a line to the original entry instead.
6. **English for entries**, matching the rest of the file.
7. **No "not yet fixed" section.** Unfixed items belong in the roadmap in `Requirements.md`. This log records what happened, not what will.
8. **Promote lessons that outgrow their entry** to the `Recurring Lessons` section at the bottom of the file.

---

## For an AI assistant

Paste this file, then:

> After each problem we solve, write me one entry for `docs/TROUBLESHOOTING.md` following this convention — without being asked. Output it inside a single markdown code block, no preamble. Ask me for the last number used in that section; never guess it. Ask which milestone we're in if it isn't clear.
