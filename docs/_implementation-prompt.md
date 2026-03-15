# Implementation Prompt

> Paste this into a fresh Claude Sonnet 4.6 chat session with the Pandowdy workspace open.

---

You are implementing a planned refactoring of the `Pandowdy.Project` module. Two documents govern your work:

1. **Blueprint** — `docs/Memory-Resident-Refactoring-Blueprint.md` — your execution checklist. Follow its steps sequentially, in the exact order listed. Do not skip, reorder, or combine steps.

2. **Plan** — `docs/Memory-Resident-Project-Refactoring-Plan.md` — the authoritative design reference. When a Blueprint step says `(Plan: "Section")`, read that section of the Plan for the full type definition, pseudocode, or behavioral spec before writing code. If the Blueprint and Plan ever conflict, the Plan wins.

Read both documents in their entirety before writing any code. Then execute the Blueprint from Step 0.1 through the Post-Implementation Checklist.

**Key rules:**

- **One step at a time.** Complete each step fully before moving to the next. Use the todo list tool to track your progress through every step.
- **Verify when told.** Steps marked `[VERIFY]` require you to build or run tests and confirm the result before proceeding.
- **Git policy.** You must NEVER execute git commands. At `[GIT COMMIT POINT]` markers, notify me with the suggested commit message and wait for me to confirm before continuing. At `[GIT WARNING]` markers, do not ask to commit — the code is in a transitional state.
- **Ambiguity policy.** If any step is unclear, contradicts the Plan, or requires a judgment call not covered by either document, stop and ask me for clarification. Do not guess or improvise.
- **Coding style.** Follow `.github/copilot-instructions.md` for all C# code: curly braces on all control statements, primary constructors where straightforward, `_camelCase` private fields, PascalCase public members, nullable reference types. Do not use single-line control statements without braces.
- **No extra files.** Do not create summary documents, changelogs, or other artifacts beyond what the Blueprint specifies.

Begin by reading both documents, then start at Phase 0, Step 0.1.
