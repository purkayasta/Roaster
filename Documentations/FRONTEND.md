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
│   ├── Code.razor               # Monaco editor: language/theme dropdowns, submit, inline review zones
│   ├── Comment.razor            # Inline review comment: severity badge, reply thread, send button
│   ├── SessionView.razor        # MudDataGrid of sessions with edit/delete action buttons
│   └── Submissions.razor        # "/code" — server-paginated submissions list
├── Shared/
│   ├── NewSessionDialog.razor   # MudDialog — enter name, closes with name string
│   └── EditorComponentBase.cs   # Abstract base: Running flag, injected services, RunLogException
├── Layout/
│   ├── MainLayout.razor         # Shell: dark/light toggle, sidebar, persists preference to localStorage
│   └── NavMenu.razor            # Sidebar links: Dashboard (/), Code (/code)
├── Resources/
│   └── NordDark.json            # Nord dark theme definition (EmbeddedResource)
├── wwwroot/
│   ├── index.html               # Script/CSS loading; inline styles for review zones
│   └── reviewZones.js           # Monaco ViewZone + decoration JS interop (window.RoasterReview)
└── Services/
    ├── SessionService.cs        # Session API calls (GET list, GET by id, POST, DELETE)
    ├── SubmissionService.cs     # Submission API calls (POST /api/submissions)
    ├── ReviewService.cs         # Review + thread API calls (GET review, POST reply, GET replies)
    ├── CodeUtils.cs             # Static helpers: editor config, JS interop wrappers, severity colors
    ├── ThemeFactory.cs          # Custom Monaco theme registry + one-time registration
    ├── ApiOptions.cs            # Record bound from "Api" config section (BaseUrl)
    └── Models/
        ├── ApiResponse.cs               # ApiResponse<T> — wraps all HTTP responses (status, data, error)
        ├── SessionSubmissionResponse.cs # record(SubmissionId?, SessionId, SessionName, Language?, Status?, SessionCreatedAt)
        ├── SessionDetailResponse.cs     # record(Id, Name, CreatedAt, CodeContent?, Language?, Status?, SubmissionId?)
        ├── SessionResponse.cs           # record(Id, Name, CreatedAt) — returned from POST /api/sessions
        ├── SubmissionResponse.cs        # record(Id, Language?, Status, CreatedAt, UpdatedAt)
        └── ReviewResponse.cs            # ReviewResponse, ReviewCommentResponse, ThreadReplyResponse, ThreadCreatedResponse
```

---

## Navigation & Pages

| Route | Component | What it shows |
|---|---|---|
| `/` | `Index.razor` | Session list via `<SessionView />` |
| `/session/{Id}` | `Session.razor` | Session name, status chip, `<Code />` editor |
| `/code` | `Submissions.razor` | Server-paginated list of all submissions |
| `*` | `NotFound.razor` | 404 message |

---

## Pages

### `Index.razor`
Owns all session action handlers — no decision logic lives in child components.

- On init: calls `SessionService.GetAllAsync(pageSize: 50)`, passes result to `<SessionView />`.
- **New** button: opens `NewSessionDialog`, calls `SessionService.CreateAsync(name)`, navigates to `/session/{id}`.
- **Edit** handler: `Navigation.NavigateTo($"/session/{sessionId}")`.
- **Delete** handler: calls `SessionService.DeleteAsync(sessionId)`, removes from local `_sessions` list.

### `Session.razor`
Route: `/session/{Id}` and `/session/` (no id).

- On params set: parses `Id` as Guid, calls `SessionService.GetByIdAsync`.
- Shows session name + UUID caption + status chip (color-coded).
- Renders `<Code SessionId SubmissionId Content Language />`. `SubmissionId` is populated from `SessionDetailResponse` and drives initial review load.
- `StatusColor`: `"Completed"` → Success, `"Processing"` → Warning, `"Failed"` → Error.

---

## Components

### `SessionView.razor`
Inherits `EditorComponentBase`. `MudDataGrid` with columns: Session Name, Language, Created At, Actions.

Parameters (`EditorRequired`): `Sessions (List<SessionSubmissionResponse>)`, `PageSize (int)`, `OnEdit (EventCallback<Guid>)`, `OnDelete (EventCallback<Guid>)`.

Child emits events only — all navigation and API calls live in `Index.razor`.

### `Submissions.razor`
Page at `/code`. Server-side paginated `MudDataGrid` showing all sessions+submission status. Uses `SessionService.GetAllAsync(page, pageSize + 1)` with the fetch-n+1 trick to determine if a next page exists (API does not return total count).

### `Code.razor`
Monaco editor wrapper. Implements `IAsyncDisposable`.

Parameters:
- `SessionId (Guid)` — required for submit
- `SubmissionId (long?)` — if set, tries to load an existing review on init
- `Content (string?)` — pre-fills editor (falls back to `CodeUtils.DefaultBoilerplate`)
- `Language (string?)` — pre-selects language dropdown (falls back to `"csharp"`)

**Review flow:**
1. User clicks **Review** → `SubmissionService.SubmitAsync` → stores `_submissionId`, starts polling.
2. Polling (`Task.Run`, 3 s interval): calls `ReviewService.GetReviewAsync`. Stops on 200 (render review) or non-204 error (show snackbar).
3. On init with `SubmissionId` set: calls `TryLoadReviewAsync` immediately (handles already-completed reviews on page load).
4. `ApplyReview` pre-populates `_commentRefs` dictionary, sets `_reviewDirty = true`.
5. `OnAfterRenderAsync` fires when `_reviewDirty`: clears old zones/decorations, injects new ViewZones and glyph decorations via `CodeUtils` JS interop helpers.

Off-screen DOM pattern: `<Comment>` components render in a `position:absolute; left:-9999px` container. Their `ElementReference`s are passed to `RoasterReview.addZone` in JS, which hands them to Monaco as ViewZone `domNode`s.

**Dispose:** cancels polling CTS, clears all zones and decorations.

### `Comment.razor`
Inherits `EditorComponentBase`. Renders inside a Monaco ViewZone.

Parameters (`EditorRequired`): `SubmissionId (long)`, `ReviewId (long)`, `CommentData (ReviewCommentResponse)`, `OnZoneResize (EventCallback)`.

- On init: if `CommentData.ThreadId` is set, loads existing replies via `ReviewService.GetRepliesAsync`.
- **Send:** posts reply via `ReviewService.PostReplyAsync`, captures returned `threadId` (stored in `_threadId` field) so subsequent replies reload from server even when the thread was just created.
- Calls `OnZoneResize` after replies change so `Code.razor` can call `RoasterReview.updateZoneHeight`.
- Uses `Running` from base class for button disabled state (no separate `_sending` field).

### `NewSessionDialog.razor`
MudDialog — enter a session name. Does **not** make API calls; caller owns creation.

---

## Services

All services use primary constructors receiving a typed `HttpClient`. All return `ApiResponse<T>`.

### `ApiResponse<T>`
Wraps every HTTP response. Built via the static `FromAsync(HttpResponseMessage)` factory:
- 2xx (except 204): `IsSuccess = true`, deserializes body to `Data`.
- 204 No Content: `IsSuccess = true`, `Data = default`.
- 4xx/5xx: `IsSuccess = false`, reads error body into `Error` (trims surrounding quotes ASP.NET adds to string responses).

### `SessionService`
```
GetAllAsync(page, pageSize, ct)   → GET /api/sessions?page=&pageSize=   → ApiResponse<List<SessionSubmissionResponse>>
GetByIdAsync(sessionId, ct)       → GET /api/sessions/{id}              → ApiResponse<SessionDetailResponse>
CreateAsync(name, ct)             → POST /api/sessions                  → ApiResponse<SessionResponse>
DeleteAsync(sessionId, ct)        → DELETE /api/sessions/{id}           → ApiResponse<object?>
```

### `SubmissionService`
```
SubmitAsync(sessionId, code, language, ct)  → POST /api/submissions  → ApiResponse<SubmissionResponse>
```
Returns 202 Accepted on success, 409 Conflict if already processing, 404 if session not found.

### `ReviewService`
```
GetReviewAsync(submissionId, ct)                              → GET /api/submissions/{id}/review
                                                              → ApiResponse<ReviewResponse>
                                                              → 200 OK (ready), 204 No Content (not ready), 404 (not found)

PostReplyAsync(submissionId, reviewId, lineNumber, content, ct)
  → POST /api/submissions/{submissionId}/reviews/{reviewId}/lines/{lineNumber}/threads
  → ApiResponse<ThreadCreatedResponse>   { ThreadId, ReplyId }

GetRepliesAsync(threadId, ct)  → GET /api/threads/{threadId}/replies  → ApiResponse<List<ThreadReplyResponse>>
```

### `CodeUtils`
Static class. Single source of truth for editor configuration and all JS interop.

```csharp
// Constants
EditorId            // "code-editor-main"
DefaultBoilerplate  // "// Paste your code here…"
DefaultLanguage     // "csharp"
DefaultTheme        // "vs-dark"
Languages           // ["csharp", "javascript", "typescript", "json"]
Themes              // BuiltInThemes + ThemeFactory.CustomThemes

// Editor interop
SetLanguageAsync(js, editor, language)
SetThemeAsync(js, theme)

// Severity
GetSeverityColor(severity)  → MudBlazor.Color  (Error/Warning/Info/Default)

// ViewZone / decoration interop (all target EditorId)
AddReviewZoneAsync(js, lineNumber, domRef)           → Task<string?> zoneId
UpdateZoneHeightAsync(js, zoneId, domRef)            → Task
RemoveZonesAsync(js, zoneIds)                        → Task
AddReviewDecorationsAsync(js, comments)              → Task<string[]> decorationIds
ClearDecorationsAsync(js, decorationIds)             → Task
```

### `ThemeFactory`
Static class. Custom Monaco theme registry.

- `Defined` — `HashSet<string>` guards one-time registration per app session.
- `CustomThemes` — `(Key, Label)` pairs for dropdowns.
- `EnsureAllAsync(IJSRuntime)` — deserializes JSON from embedded resources, calls `EditorGlobal.DefineTheme`. Skips already-defined themes.

To add a new theme: add JSON to `Resources/`, mark `EmbeddedResource` in csproj, add entry to `ThemeFactory.Registry`.

Current custom themes: `nord-dark`.

---

## JS Interop: `reviewZones.js`

Exposes `window.RoasterReview`. Accesses Monaco editor instances via `window.blazorMonaco.editor.getEditor(editorId)` (BlazorMonaco's internal registry).

| Function | What it does |
|---|---|
| `addZone(editorId, lineNumber, domNode)` | Calls `editor.changeViewZones`, registers zone config in `_zoneConfigs` map. Returns zone ID string. |
| `updateZoneHeight(editorId, zoneId, domNode)` | Mutates stored `config.heightInPx = domNode.scrollHeight`, calls `layoutZone(zoneId)`. |
| `removeZones(editorId, zoneIds)` | Removes each zone and cleans `_zoneConfigs`. |
| `addDecorations(editorId, lineNumbers)` | `editor.deltaDecorations` — glyph margin dot + line background per severity. |
| `clearDecorations(editorId, decorationIds)` | `editor.deltaDecorations(ids, [])`. |

**Height resize pattern:** `layoutZone(id)` re-reads `heightInPx` from the zone config object. The config must be mutated (not replaced) before calling `layoutZone` — the stored reference in `_zoneConfigs` is used for this.

**CSS classes** (defined in `index.html`):
- Glyph dots: `.roaster-glyph-error/warning/info`
- Line highlights: `.roaster-line-error/warning/info`
- Comment box: `.roaster-review-comment`, `.roaster-reply-ai`, `.roaster-reply-user`

---

## Base Class: `EditorComponentBase`

All pages and components inherit this (`Shared/EditorComponentBase.cs`).

- `Running` — `true` during async ops; triggers `StateHasChanged()` automatically. Used to disable buttons.
- `RunLogException(Func<Task>)` / `RunLogException(Action)` — manages `Running`, wraps in try/catch, shows `Snackbar` on exception.
- Injects: `ISnackbar`, `IDialogService`, `NavigationManager`.

---

## UI Libraries

| Library | Version |
|---|---|
| MudBlazor | 9.4.0 |
| BlazorMonaco | 3.4.0 |

No CDN dependencies.

---

## Conventions

- All MudBlazor bindings use `@bind-Value` + `@bind-Value:after` (not the old `Value` + `ValueChanged`).
- `EventCallback<T>` for child → parent communication. Child components emit events only.
- `NavigationManager` injected via `EditorComponentBase` — no need to re-inject in components.
- To add a new API call: add a method to the appropriate service and return `ApiResponse<T>`. Register a new typed `HttpClient` in `Program.cs` if adding a new service.
- BlazorMonaco JS interop: use `@using EditorGlobal = BlazorMonaco.Editor.Global` alias in `_Imports.razor` to avoid ambiguity.
- All editor JS interop goes through `CodeUtils` static methods — do not call `JS.InvokeAsync("RoasterReview.*")` directly from components.
