---
trigger: always_on
---

Artifacts are the single source of truth for the current request.
Keep them small, explicit, and consistent.

## Folder & naming
Create/maintain:
- artifacts/00-intake.md
- artifacts/01-plan-and-phases.md
- artifacts/02-acceptance-criteria.md
- artifacts/03-scope.md
- artifacts/04-risks.md (optional)
- artifacts/05-task-breakdown.md
- artifacts/06-contracts.md (API/UI/data contracts as needed)
- artifacts/07-implementation-notes.md (append-only, owned sections)
- artifacts/08-open-questions.md (only if BLOCKED)
- artifacts/09-integration-summary.md
- artifacts/10-test-plan.md
- artifacts/11-qa-gate.md
- artifacts/12-handoff.md
- artifacts/13-release-notes.md (optional)

## Ownership (who writes what)
- PM:
  - 01, 02, 03, 04, contributes to 05
- Backend:
  - 06 (backend contracts), 07 (Backend section), contributes to 09
- UI Developer:
  - 06 (UI contracts), 07 (UI section), contributes to 09
- Tester/QA:
  - 10, 11
- Orchestrator:
  - 00, 05, 09, 12, 13

## Append-only notes rule
- artifacts/07-implementation-notes.md is append-only.
- Each role writes ONLY within its own section.
- When you change earlier decisions, add an "Amendment" note instead of rewriting history.

## Conflict rule
If two artifacts contradict:
- Stop and write BLOCKED + minimal questions.
- Do NOT “merge by guessing”.
