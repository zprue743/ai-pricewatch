# AI PriceWatch — Design Specification

## 1. Intent & Why

Developers and power users working with multiple AI APIs and coding assistants constantly switch between models for different tasks (e.g., quick classification, code generation, deep architectural reasoning). Looking up token pricing across provider pricing pages interrupts flow.

**AI PriceWatch** is a lightweight, terminal-sized (`760×480` default) transparent desktop HUD overlay. With a quick glance or hotkey toggle, the user can select an AI provider, compare input/cached-input/output prices per 1M tokens, filter by task tier, and estimate prompt cost without leaving their active workspace.

## 2. Goals, Non-Goals, and Anti-Goals

### Goals
- **Terminal-sized translucent HUD (`760×480` default, resizable):** Frameless dark glass/terminal aesthetic with adjustable background opacity (`35%`–`100%`), Always-on-Top pin toggle, and drag-to-move header.
- **Provider Selector & Instant Filtering:** One-click tabs (plus `Alt+1..6` shortcuts) for **All**, **OpenAI**, **Anthropic**, **Google Gemini**, **xAI**, and **DeepSeek**, plus instant text search and sort (by Input Price, Output Price, Context Window, or Model Name).
- **Task-Oriented Price Guidance:** Each model row displays:
  - Model name & ID
  - Task Tier badge (`Budget / Fast`, `Balanced / Daily Driver`, `Frontier / Reasoning`)
  - Input price ($ / 1M tokens)
  - Cached Input price ($ / 1M tokens)
  - Output price ($ / 1M tokens)
  - Context window size (e.g., `128K`, `200K`, `1M`, `2M`)
- **Interactive Task Cost Estimator:** A compact footer bar where users can pick a typical task preset (e.g., *Quick Edit: 4K in / 1K out*, *Codebase Feature: 50K in / 8K out*, *Long Context Audit: 250K in / 16K out*) or enter custom token counts, and immediately see the exact dollar cost per call for every listed model.
- **Offline-First Curated Data + Live Sync:** Ships with an embedded `pricing-catalog.json` containing current pricing across major providers so the HUD works immediately offline, paired with a non-blocking **Sync Live Prices** action that fetches and merges live per-token pricing from OpenRouter's public models endpoint (`https://openrouter.ai/api/v1/models`) and caches it locally.
- **Global Hotkey (`Ctrl+Shift+Space`):** Summons or hides the HUD overlay from anywhere on the desktop.

### Non-Goals
- Sending prompts or acting as an AI chat client.
- Managing API keys or billing accounts (OpenRouter's models endpoint is public and requires no API key).

### Anti-Goals
- **Bloated resource usage:** The overlay must not consume hundreds of megabytes of RAM or lag when toggled.
- **Opaque or intrusive UI:** Must never steal focus unexpectedly or block reading underlying windows when translucent.

## 3. Constraints
- **Platform:** Windows desktop (`net10.0-windows`).
- **Zero External Runtime Dependencies:** Must build and run out of the box using the installed `.NET 10.0.103` SDK (`Microsoft.WindowsDesktop.App.WPF` and `Microsoft.NETCore.App`) with zero external NuGet packages required, ensuring 100% offline build and test execution.
- **Default Window Geometry:** `Width = 760`, `Height = 480`, `MinWidth = 560`, `MinHeight = 340`, centered on first launch.

## 4. Tech Stack Approaches Considered

| Approach | Pros | Cons | Verdict |
|---|---|---|---|
| **1. .NET 10 + WPF (C#) (Recommended)** | Native per-pixel window transparency (`AllowsTransparency="True"`, `WindowStyle="None"`), hardware-accelerated rendering, native Win32 `RegisterHotKey` P/Invoke, tiny memory footprint, `.NET 10.0.103` SDK already installed on machine, zero external NuGet packages needed. | Windows-specific UI layer (though core pricing/catalog logic is pure portable .NET). | **Selected (Recommended)** |
| **2. Python 3.12 + Tkinter / CustomTkinter** | Python 3.12 is installed on machine; fast scripting. | Tkinter sets window-wide alpha (`-alpha`) affecting text readability alongside background opacity; less crisp typography and table layout. | Viable alternative if Python is preferred |
| **3. Tauri v2 / Electron + React** | Familiar web CSS styling (`backdrop-filter`). | Node.js and Rust/Cargo are not currently on PATH; Electron adds ~150MB+ distribution & RAM overhead for a simple HUD. | Rejected due to toolchain & footprint overhead |

## 5. Architecture & Component Design

1. **`AiPriceWatch.Core` (`src/AiPriceWatch.Core/`)** — Pure .NET 10 class library containing:
   - `ModelPricingEntry`: Immutable record representing a model (`Id`, `Name`, `Provider`, `Tier`, `InputPerMillion`, `CachedInputPerMillion`, `OutputPerMillion`, `ContextTokens`, `Notes`).
   - `PricingCatalogService`: Loads embedded/local `pricing-catalog.json`, filters by provider/tier/search query, sorts by selected column, and merges live OpenRouter API payloads (`prompt` / `completion` per-token strings converted to $/1M tokens).
   - `TaskCostCalculator`: Computes exact dollar cost for a given `(InputTokens, CachedInputTokens, OutputTokens)` workload and formats human-readable cost strings (e.g., `$0.0042`, `<$0.0001`).
2. **`AiPriceWatch.App` (`src/AiPriceWatch.App/`)** — .NET 10 WPF desktop overlay application:
   - `MainWindow.xaml` / `MainWindow.xaml.cs`: Frameless translucent terminal-inspired HUD (`#CC0D1117` default background with live opacity slider controlling background alpha independently of crisp high-contrast foreground text).
   - `HudViewModel`: Observable state binding provider tabs, search box, sort selector, task preset estimator, opacity slider, and always-on-top pin state.
   - `GlobalHotkeyService`: Win32 `user32.dll` (`RegisterHotKey` / `UnregisterHotKey`) hook registered on `Ctrl+Shift+Space` to toggle HUD visibility.
3. **`AiPriceWatch.Tests` (`tests/AiPriceWatch.Tests/`)** — Zero-dependency deterministic test runner verifying catalog loading, filtering, sorting, cost estimation math, edge-case inputs, and OpenRouter JSON price parsing.
