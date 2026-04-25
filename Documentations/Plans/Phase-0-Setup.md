# Phase 0 — Solution Setup & Rename

Establish the canonical solution structure. No code is written in this phase — only repository and solution housekeeping.

---

## 0.A — Repository Rename

**What changed:**
- Repository folder renamed to `Roaster/`
- Solution file renamed from `Editor.slnx` → `Roaster.slnx`
- All documentation updated: `Maverices` → `Roaster`

**What stays the same:**
- `Editor/` project folder and `Editor.csproj` — the Blazor WASM frontend keeps this name
- Namespaces inside `Editor/` are unchanged

---

## 0.B — Solution File

**File:** `Roaster.slnx` (repo root)

```xml
<Solution>
  <Project Path="Editor/Editor.csproj" />
</Solution>
```

As backend projects are added (Phase 1 onward), each goes under a `Backend` solution folder:

```xml
<Solution>
  <Folder Name="Backend">
    <Project Path="src/Roaster.Data/Roaster.Data.csproj" />
    <Project Path="src/Roaster.Contracts/Roaster.Contracts.csproj" />
    <Project Path="src/Roaster.Api/Roaster.Api.csproj" />
    <Project Path="src/Roaster.Consumer/Roaster.Consumer.csproj" />
    <Project Path="src/Roaster.Reviewer/Roaster.Reviewer.csproj" />
    <Project Path="src/Roaster.Migration/Roaster.Migration.csproj" />
  </Folder>
  <Project Path="Editor/Editor.csproj" />
</Solution>
```

---

## 0.C — Target Solution Layout

```
Roaster.slnx
Editor/                    # Blazor WASM frontend (existing)
src/
  Roaster.Data/            # Added Phase 1
  Roaster.Contracts/       # Added Phase 1
  Roaster.Api/             # Added Phase 1
  Roaster.Consumer/        # Added Phase 2
  Roaster.Reviewer/        # Added Phase 4
  Roaster.Migration/       # Added Phase 6
Documentations/
  Summary.md
  Plans/
    Phase-0-Setup.md       ← this file
    Phase-1-API.md
    ...
docker/
docker-compose.yml
plan.md
```

---

## Checklist

- [x] Repo folder renamed to `Roaster/`
- [x] `Editor.slnx` renamed to `Roaster.slnx`
- [x] All docs updated from `Maverices` → `Roaster`
- [x] `CLAUDE.md` updated to reference `Roaster.slnx`
- [ ] `Roaster.slnx` updated with `Backend` solution folder as projects are added (done per-phase)
