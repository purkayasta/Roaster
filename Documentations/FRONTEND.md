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
├── Program.cs                   # WASM host: HttpClient, MudBlazor DI, ApiOptions
├── Pages/
│   ├── Index.razor              # "/" — dashboard, renders <Submissions />
│   ├── Session.razor            # "/session/{Id}/{Name}" — session detail view
│   └── NotFound.razor           # Catch-all 404 page
├── Components/
│   ├── Code.razor               # Monaco editor (BlazorMonaco); vs-dark, csharp, 14px
│   └── Submissions.razor        # MudDataGrid listing all submissions with pagination
├── Shared/
│   ├── NewSessionDialog.razor   # MudDialog — enter name, generates client-side UUID, navigates to session
│   └── EditorComponentBase.cs  # Abstract base: Running flag, Snackbar/DialogService injection, RunLogException
├── Layout/
│   ├── MainLayout.razor         # Shell layout
│   └── NavMenu.razor            # Navigation sidebar
└── Services/
    ├── SubmissionService.cs     # HttpClient wrapper: GET /api/submissions (page, pageSize)
    ├── ApiOptions.cs            # Record bound from "Api" config section (BaseUrl)
    └── Models/
        └── SubmissionResponse.cs  # record(Id, Language, Status, CreatedAt, UpdatedAt)
```

---

## Navigation & Pages

| Route | Component | What it shows |
|---|---|---|
| `/` | `Index.razor` | `<Submissions />` grid of all submissions |
| `/session/{Id}/{Name}` | `Session.razor` | Session name + UUID; Id defaults to a new Guid if omitted |
| `*` | `NotFound.razor` | 404 message |

---

## Components

### `Submissions.razor`
Inherits `EditorComponentBase`. On init, calls `SubmissionService.GetAllAsync()` and renders a `MudDataGrid` (columns: ID, Language, Status, Created At). Shows a `MudProgressCircular` spinner while `Running`. Has a "New" button that opens `NewSessionDialog`.

### `NewSessionDialog.razor`
MudDialog — user enters a session name. On "Start Now", generates a `Guid.NewGuid()` client-side, closes the dialog, and navigates to `/session/{id}/{encodedName}`. Does **not** call `POST /api/sessions` — session is registered in the URL only at this stage.

### `Code.razor`
Wraps `StandaloneCodeEditor` from BlazorMonaco. Options: `AutomaticLayout=true`, `Language="csharp"`, `Theme="vs-dark"`, `WordWrap="on"`, minimap disabled, `LineNumbers="on"`, `FontSize=14`. Default placeholder: `// Paste your code here`.

---

## Services

### `SubmissionService`
Primary constructor; receives `HttpClient` (base address set to `Api:BaseUrl`).

```csharp
GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct)
  → GET /api/submissions?page={page}&pageSize={pageSize}
  → returns List<SubmissionResponse>
```

---

## Base Class: `EditorComponentBase`

All page/component classes that need async error handling should inherit this.

- `Running` — set `true` at start of async op, `false` on completion; triggers `StateHasChanged()` automatically.
- `RunLogException(Func<Task>)` / `RunLogException(Action)` — wraps work in try/catch; shows `Snackbar` error on exception.
- Injects `ISnackbar` and `IDialogService`.

---

## UI Libraries

| Library | Version | Source |
|---|---|---|
| MudBlazor | 9.4.0 | NuGet |
| BlazorMonaco | 3.4.0 | NuGet |

No CDN dependencies — all UI libraries are NuGet packages.

---

## Conventions

- `.razor.css` scoped styles per component.
- `_Imports.razor` holds all global `@using` statements.
- `IJSRuntime` used via `InvokeVoidAsync` / `InvokeAsync<T>` where needed.
- `EventCallback<T>` for child→parent communication.
- State mutations outside event handlers require explicit `StateHasChanged()`.
- To add more API calls: add methods to `SubmissionService` or create a new service registered in `Program.cs`. No component changes needed for service swaps.
