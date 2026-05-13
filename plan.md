# Roaster — Plan

For all implementation details, see [Documentations/Summary.md](Documentations/Summary.md).

---

## Global Pre-checklist

- [x] `Roaster.slnx` at solution root
- [x] `Roaster.Editor` added to `Roaster.slnx` under `Frontend` solution folder
- [x] All backend projects added to `Roaster.slnx` under `Backend` solution folder (pending `Roaster.Migration` in Phase 6):
  - [x] `Roaster.Data`
  - [x] `Roaster.Contracts`
  - [x] `Roaster.Api`
  - [x] `Roaster.Consumer`
  - [x] `Roaster.Reviewer`
  - [x] `Roaster.Migration`

---

## Phases

| # | Phase | Status | Detail |
|---|-------|--------|--------|
| 0 | Setup & Rename | ✅ Done | [Phase-0-Setup.md](Documentations/Plans/Phase-0-Setup.md) |
| 1 | API | ✅ Done | [Phase-1-API.md](Documentations/Plans/Phase-1-API.md) |
| 2 | Events | ✅ Done | [Phase-2-Events.md](Documentations/Plans/Phase-2-Events.md) |
| 3 | Correlation IDs | ✅ Done | [Phase-3-CorrelationIDs.md](Documentations/Plans/Phase-3-CorrelationIDs.md) |
| 4 | AI Reviewer | ✅ Done | [Phase-4-AIReviewer.md](Documentations/Plans/Phase-4-AIReviewer.md) |
| 5 | Unit Tests | ✅ Done | [Phase-5-UnitTests.md](Documentations/Plans/Phase-5-UnitTests.md) |
| 6 | DbUp Migrations | ⬜ Not started | [Phase-6-Migration.md](Documentations/Plans/Phase-6-Migration.md) |
| 7 | Docker | ✅ Done | [Phase-7-Docker.md](Documentations/Plans/Phase-7-Docker.md) |
| 8 | Frontend | ✅ Done | [Phase-8-Frontend.md](Documentations/Plans/Phase-8-Frontend.md) |
| 9 | Continue | ⬜ Not started | [Phase-9-Continue.md](Documentations/Plans/Phase-9-Continue.md) |