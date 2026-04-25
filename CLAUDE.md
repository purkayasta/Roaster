# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Solution

`Roaster.slnx` at the repo root is the solution file. The `Editor/` project is the Blazor WASM frontend; backend projects will be added under a `Backend` solution folder in later phases.

## Commands

All commands run from the `Editor/` subdirectory (where the `.csproj` lives):

```bash
dotnet run          # Run the app (http://localhost:5094, https://localhost:7052)
dotnet watch run    # Run with hot reload
dotnet build        # Build the solution
dotnet publish      # Publish for production
```

There are no test projects currently in this solution.

## Architecture

This is a **Blazor WebAssembly** (.NET 10) single-page application (code review/analysis tool) that runs entirely in the browser via WebAssembly.

**Startup flow:** `wwwroot/index.html` loads the Blazor runtime → `Program.cs` bootstraps the WebAssembly host and registers services → `App.razor` handles client-side routing → `Layout/MainLayout.razor` wraps all pages.

**Key directories:**
- `Pages/` — Routable page components (`@page "/route"`). Core pages: `RoastMyCode.razor` (main analysis UI), `CodeWorkspace.razor` (PR review), `CodeEditor.razor` (code input)
- `Components/` — Reusable components. `RoastThread.razor` renders inline AI-generated comments
- `Layout/` — `MainLayout.razor` (app chrome with dark mode toggle persisted to `localStorage`) and `NavMenu.razor`
- `Services/` — `IRoastApiService` interface + `MockRoastApiService` (hardcoded 2s latency, returns comments for lines 3 and 5). `Dtos/RoastComment.cs` and `Dtos/SubmissionRequest.cs` are the data contracts
- `wwwroot/monaco-interop.js` — JS bridge for injecting Blazor components into Monaco view zones
- `wwwroot/syntax-highlight.js` — Highlight.js wrapper

**UI Libraries (loaded from CDN in `index.html`):**
- **MudBlazor** (v9.4.0) — Material Design components; theme/dialog/snackbar providers are in `App.razor`
- **BlazorMonaco** (v3.4.0) — Monaco editor (VS Code engine), v0.54.0 loaded via RequireJS
- **Fluent UI** (v4.14.1) — Microsoft's design system components

## Core Architectural Patterns

### "Hidden Div" Monaco Integration

`RoastThread` components can't be placed directly inside Monaco's DOM. The solution:

1. Blazor renders `RoastThread` components inside a hidden `<div>` in `RoastMyCode.razor`
2. After `StateHasChanged()` + a 100ms `Task.Delay` (to let Blazor flush to the DOM), `monaco-interop.js` is called
3. `injectViewZone()` in `monaco-interop.js` moves the hidden div into a Monaco view zone (reserves 6 lines of height) using `editor.changeViewZones()`
4. Blazor continues managing the component's state and event callbacks despite the DOM relocation

This proves Blazor's component tree and the visual DOM tree can diverge — components remain live after JS moves their DOM nodes.

### Dependency Injection

Services use interface-based DI registered in `Program.cs`. To switch from mock to a real backend, replace `MockRoastApiService` with an `HttpRoastApiService` in that one registration — no component code changes needed.

### JS Interop Conventions

- `IJSRuntime` is injected and called via `JSRuntime.InvokeVoidAsync()` / `InvokeAsync<T>()`
- Monaco editor instance retrieval has a built-in 5-second wait in `getMonacoEditorInstance()` to handle async initialization
- View zone IDs are tracked in component state to support removal/update

### Component Conventions
- `.razor.css` files are CSS-isolated to their paired component (Blazor scoping)
- `_Imports.razor` holds global `@using` statements for all components
- Mutating state outside event handlers requires calling `StateHasChanged()`
- `EventCallback<T>` is used for child→parent communication (e.g., `RoastThread` firing `OnClose`, `OnReply`)
