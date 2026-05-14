# Frontend Architecture (Roaster.Editor)

**Blazor WebAssembly** (.NET 10) running entirely in the browser.

**Startup:** `wwwroot/index.html` → Blazor runtime → `Program.cs` → `App.razor` (router) → `Layout/MainLayout.razor`.

**API base URL:** configured in `wwwroot/appsettings.json` under `Api:BaseUrl` (default: `http://localhost:8080`).

---

## Directory Structure

```
src/Roaster.Editor/
├── App.razor                    # Router, NotFound fallback
├── _Imports.razor               # Global @using (System, MudBlazor, BlazorMonaco, Services)
├── Program.cs                   # WASM host: HttpClient registrations, MudBlazor DI, ApiOptions
├── Pages/
│   ├── Index.razor              # "/" — session dashboard, action handlers (new/edit/delete)
│   ├── Session.razor            # "/session/{Id}" — session detail, renders <Code />
│   └── NotFound.razor           # Catch-all 404 page
├── Components/
│   ├── Code.razor               # Monaco editor wrapper; language + theme dropdowns
│   ├── SessionView.razor        # MudDataGrid of sessions with edit/delete action buttons
│   └── Submissions.razor        # Legacy component (unused on any page)
├── Shared/
│   ├── NewSessionDialog.razor   # MudDialog — enter name, closes with name string
│   ├── EditorComponentBase.cs   # Abstract base: Running flag, injected services, RunLogException
│   └── MainLayout.razor / NavMenu.razor
├── Layout/
│   ├── MainLayout.razor         # Shell layout with sidebar
│   └── NavMenu.razor            # Navigation sidebar
├── Resources/
│   └── NordDark.json            # Nord dark theme definition (EmbeddedResource)
└── Services/
    ├── SessionService.cs        # All session API calls (GET list, GET by id, POST, DELETE)
    ├── SubmissionService.cs     # Placeholder for future submission API calls
    ├── CodeUtils.cs             # Static helpers: language list, theme list, SetLanguage/SetTheme
    ├── ThemeFactory.cs          # Custom Monaco theme registry + one-time registration
    ├── ApiOptions.cs            # Record bound from "Api" config section (BaseUrl)
    └── Models/
        ├── SessionSubmissionResponse.cs  # record(SubmissionId?, SessionId, SessionName, Language?, Status?, SessionCreatedAt)
        ├── SessionDetailResponse.cs      # record(Id, Name, CreatedAt, CodeContent?, Language?, Status?)
        ├── SessionResponse.cs            # record(Id, Name, CreatedAt) — returned from POST /api/sessions
        └── SubmissionResponse.cs         # Legacy model (unused)
```

---

## Navigation & Pages

| Route | Component | What it shows |
|---|---|---|
| `/` | `Index.razor` | Session list via `<SessionView />` |
| `/session/{Id}` | `Session.razor` | Session name, status chip, `<Code />` editor |
| `*` | `NotFound.razor` | 404 message |

---

## Pages

### `Index.razor`
Owns all session action handlers — no decision logic lives in child components.

- On init: calls `SessionService.GetAllAsync(pageSize: 50)` and passes result to `<SessionView />`.
- **New** button: opens `NewSessionDialog`, gets name string back, calls `SessionService.CreateAsync(name)`, navigates to `/session/{id}`.
- **Edit** handler: `Navigation.NavigateTo($"/session/{sessionId}")`.
- **Delete** handler: calls `SessionService.DeleteAsync(sessionId)`, then removes from local `_sessions` list (no refetch needed).

### `Session.razor`
Route: `/session/{Id}` and `/session/` (no id).

- On params set: parses `Id` as Guid, calls `SessionService.GetByIdAsync(sessionId)`.
- Shows session name (h5) + UUID caption + status chip (color-coded by string value).
- Always renders `<Code Content Language />` — shows boilerplate when session has no submission.
- `StatusColor`: `"Completed"` → Success, `"Processing"` → Warning, `"Failed"` → Error, default → Default.

---

## Components

### `SessionView.razor`
Inherits `EditorComponentBase`. Renders a `MudDataGrid` with columns: Session Name, Language, Created At, Action.

Parameters (all `EditorRequired`):
- `Sessions` — `List<SessionSubmissionResponse>`
- `PageSize` — `int` (controls `RowsPerPage` on the grid)
- `OnEdit` — `EventCallback<Guid>` fired with `SessionId` on edit click
- `OnDelete` — `EventCallback<Guid>` fired with `SessionId` on delete click

Child component emits events only — all navigation and API calls happen in the parent (`Index.razor`).

### `Code.razor`
Wraps `StandaloneCodeEditor` from BlazorMonaco. Toolbar has language and theme dropdowns.

Parameters:
- `Content` — pre-fills editor (falls back to `CodeUtils.DefaultBoilerplate`)
- `Language` — pre-selects language (falls back to `CodeUtils.DefaultLanguage = "csharp"`)

On init: calls `ThemeFactory.EnsureAllAsync(JS)` inside `RunLogException` (theme failure is non-fatal, editor falls back to built-in themes). Language and theme changes call `CodeUtils.SetLanguageAsync` / `CodeUtils.SetThemeAsync` which invoke BlazorMonaco JS interop.

### `NewSessionDialog.razor`
MudDialog — user enters a session name. Buttons: Cancel (closes dialog cancelled), Confirm (closes dialog with name string). Does **not** make any API calls — the caller owns session creation.

---

## Services

### `SessionService`
Primary constructor; receives `HttpClient` (base address set to `Api:BaseUrl`).

```csharp
GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct)
  → GET /api/sessions?page={page}&pageSize={pageSize}
  → returns List<SessionSubmissionResponse>

GetByIdAsync(Guid sessionId, CancellationToken ct)
  → GET /api/sessions/{sessionId}
  → returns SessionDetailResponse

CreateAsync(string name, CancellationToken ct)
  → POST /api/sessions  { Name = name }
  → returns SessionResponse

DeleteAsync(Guid sessionId, CancellationToken ct)
  → DELETE /api/sessions/{sessionId}
```

### `SubmissionService`
Empty shell registered in `Program.cs`. Placeholder for future submission-related API calls (submit code, poll review, thread replies).

### `CodeUtils`
Static class in `Services/`. Centralises editor configuration:

```csharp
DefaultBoilerplate  // "// Paste your code here and click Submit to get a review.\n"
DefaultLanguage     // "csharp"
DefaultTheme        // "vs-dark"
Languages           // ["csharp", "javascript", "typescript", "json"]
Themes              // BuiltInThemes + ThemeFactory.CustomThemes
SetLanguageAsync(IJSRuntime, StandaloneCodeEditor, string language)
SetThemeAsync(IJSRuntime, string theme)
```

### `ThemeFactory`
Static class. Owns the custom Monaco theme registry.

- `Registry` — `Dictionary<string, (Label, ResourcePath)>` for each custom theme JSON (EmbeddedResource).
- `Defined` — `static HashSet<string>` guards one-time registration per app session; safe across component remounts.
- `CustomThemes` — exposes `(Key, Label)` pairs for use in dropdowns.
- `EnsureAllAsync(IJSRuntime)` — iterates registry, deserializes JSON from assembly manifest, calls `EditorGlobal.DefineTheme`. Skips already-defined themes.

To add a new theme: add a JSON file to `Resources/`, mark it `EmbeddedResource` in the csproj, add one entry to `ThemeFactory.Registry`.

Current custom themes: `nord-dark` (Nord palette — Polar Night backgrounds, Snow Storm foreground, Frost for keywords/types, Aurora for strings/numbers/errors).

---

## Base Class: `EditorComponentBase`

All pages and components inherit this.

- `Running` — set `true` at start of async op, `false` on completion; triggers `StateHasChanged()` automatically.
- `RunLogException(Func<Task>)` / `RunLogException(Action)` — wraps work in try/catch; shows `Snackbar` error on exception.
- Injects: `ISnackbar`, `IDialogService`, `NavigationManager`.

---

## UI Libraries

| Library | Version | Source |
|---|---|---|
| MudBlazor | 9.4.0 | NuGet |
| BlazorMonaco | 3.4.0 | NuGet |

No CDN dependencies — all UI libraries are NuGet packages.

---

## Conventions

- `.razor.css` scoped styles per component where needed.
- `_Imports.razor` holds all global `@using` statements.
- `EventCallback<T>` for child → parent communication. Child components emit events only; action logic lives in the owning page.
- `NavigationManager` injected via `EditorComponentBase` — no need to inject in individual pages/components.
- State mutations outside event handlers require explicit `StateHasChanged()`.
- To add a new API call: add a method to `SessionService` (session-related) or `SubmissionService` (submission-related), or register a new typed `HttpClient` in `Program.cs`.
- BlazorMonaco JS interop: use `@using EditorGlobal = BlazorMonaco.Editor.Global` alias to avoid ambiguity with `BlazorMonaco.Languages.Global`.
