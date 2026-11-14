# AI PriceWatch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a terminal-sized (`760×480`) transparent desktop HUD overlay for Windows that lets users select an AI provider, compare model pricing per 1M tokens, filter by task tier, and estimate task costs at a glance.

**Architecture:** A two-project .NET 10 solution (`AiPriceWatch.sln`): `AiPriceWatch.Core` (pure .NET 10 library for model catalog loading, provider/tier filtering, sorting, task cost math, and OpenRouter live pricing parser) and `AiPriceWatch.App` (frameless per-pixel translucent WPF desktop overlay with independent background alpha control, `INotifyPropertyChanged` ViewModel, and Win32 global hotkey `Ctrl+Shift+Space`), verified by `AiPriceWatch.Tests`.

**Tech Stack:** C# 13 / .NET 10 (`net10.0` and `net10.0-windows`), WPF (`UseWPF`), `System.Text.Json`, `System.Net.Http.HttpClient`, Win32 `user32.dll` Hotkey P/Invoke.

**Spec:** `docs/superpowers/specs/2026-11-14-ai-pricewatch-design.md`

## Global Constraints

- Platform: Windows desktop (`net10.0-windows` for WPF app, `net10.0` for Core and Tests).
- Zero External Runtime Dependencies: Must build and run out of the box using the installed `.NET 10.0.103` SDK (`Microsoft.WindowsDesktop.App.WPF` and `Microsoft.NETCore.App`) with zero external NuGet packages required, ensuring 100% offline build and test execution.
- Default Window Geometry: `Width = 760`, `Height = 480`, `MinWidth = 560`, `MinHeight = 340`, centered on first launch.
- Providers supported: `All`, `OpenAI`, `Anthropic`, `Google Gemini`, `xAI`, `DeepSeek`.
- Task tiers: `Fast` (`Budget / Fast`), `Balanced` (`Balanced / Daily Driver`), `Reasoning` (`Frontier / Reasoning`).

## Review Focus

1. Negative or zero token counts in `TaskCostCalculator.CalculateCost` — must clamp negative token counts to `0` and never return negative dollar amounts or `NaN`.
2. Missing or `null` `CachedInputPerMillion` on models that do not offer prompt caching — must fall back to `InputPerMillion` when computing cached token costs and format display as `"—"` in the UI.
3. Malformed, negative, or non-numeric price strings in OpenRouter API JSON (`prompt` / `completion` fields like `"free"`, `"-1"`, or `null`) — `OpenRouterPriceParser.MergeLivePrices` must skip invalid entries gracefully without throwing or corrupting curated catalog models.
4. Case-insensitive and whitespace-padded search queries or provider names in `PricingCatalogService.Query` — must trim and match case-insensitively across `Name`, `Id`, `Provider`, and `Notes`.
5. Background opacity slider values outside `[0.35, 1.0]` — `HudViewModel.BackgroundOpacity` must clamp to `[0.35, 1.0]` so the HUD can never become completely invisible (`0.0`) and unclickable.

---

### Task 1: Solution Scaffolding, Domain Models & Task Cost Calculator

**Files:**
- Create: `.gitignore`
- Create: `AiPriceWatch.sln`
- Create: `src/AiPriceWatch.Core/AiPriceWatch.Core.csproj`
- Create: `src/AiPriceWatch.Core/Models/TaskTier.cs`
- Create: `src/AiPriceWatch.Core/Models/ModelPricingEntry.cs`
- Create: `src/AiPriceWatch.Core/Services/TaskCostCalculator.cs`
- Create: `tests/AiPriceWatch.Tests/AiPriceWatch.Tests.csproj`
- Create: `tests/AiPriceWatch.Tests/Program.cs`

**Interfaces:**
- Consumes: none
- Produces:
  - `namespace AiPriceWatch.Core.Models`
  - `public enum TaskTier { Fast, Balanced, Reasoning }`
  - `public sealed record ModelPricingEntry(string Id, string Name, string Provider, TaskTier Tier, decimal InputPerMillion, decimal? CachedInputPerMillion, decimal OutputPerMillion, int ContextTokens, string Notes)`
  - `namespace AiPriceWatch.Core.Services`
  - `public static class TaskCostCalculator`
    - `public static decimal CalculateCost(ModelPricingEntry model, int inputTokens, int cachedInputTokens, int outputTokens)`
    - `public static string FormatCost(decimal costUsd)`
    - `public static string FormatPricePerMillion(decimal? pricePerMillion)`
    - `public static string FormatContextWindow(int contextTokens)`

- [ ] **Step 1: Scaffold solution, `.gitignore`, Core library, and zero-dependency Test runner project**

Create `.gitignore` (ignoring `bin/`, `obj/`, `.vs/`, `*.user`), `AiPriceWatch.sln`, `src/AiPriceWatch.Core/AiPriceWatch.Core.csproj` (`TargetFramework: net10.0`), and `tests/AiPriceWatch.Tests/AiPriceWatch.Tests.csproj` (`OutputType: Exe`, `TargetFramework: net10.0`, referencing `AiPriceWatch.Core`).

- [ ] **Step 2: Write the failing tests for `TaskCostCalculator` in `tests/AiPriceWatch.Tests/Program.cs`**

```csharp
using AiPriceWatch.Core.Models;
using AiPriceWatch.Core.Services;

var gpt4oMini = new ModelPricingEntry(
    "gpt-4o-mini", "GPT-4o mini", "OpenAI", TaskTier.Fast,
    InputPerMillion: 0.15m, CachedInputPerMillion: 0.075m, OutputPerMillion: 0.60m,
    ContextTokens: 128_000, Notes: "Fast everyday tasks");

var noCacheModel = gpt4oMini with { CachedInputPerMillion = null };

// Standard cost calculation: 100k input ($0.015) + 200k cached ($0.015) + 50k output ($0.030) = $0.060
AssertEqual(0.060m, TaskCostCalculator.CalculateCost(gpt4oMini, 100_000, 200_000, 50_000), "Standard cost math");

// Fallback when CachedInputPerMillion is null: uses InputPerMillion
AssertEqual(0.030m, TaskCostCalculator.CalculateCost(noCacheModel, 100_000, 100_000, 0), "Null cached price fallback");

// Negative token clamping (Review Focus #1)
AssertEqual(0m, TaskCostCalculator.CalculateCost(gpt4oMini, -5000, -1000, -2000), "Negative tokens clamped to 0");

// Formatting checks
AssertEqual("$0.0000", TaskCostCalculator.FormatCost(0m), "Zero cost format");
AssertEqual("<$0.0001", TaskCostCalculator.FormatCost(0.00004m), "Sub-basis-point cost format");
AssertEqual("$0.0600", TaskCostCalculator.FormatCost(0.06m), "Sub-dollar cost format");
AssertEqual("$1.25", TaskCostCalculator.FormatCost(1.254m), "Dollar+ cost format");
AssertEqual("—", TaskCostCalculator.FormatPricePerMillion(null), "Null price per million format");
AssertEqual("$0.15", TaskCostCalculator.FormatPricePerMillion(0.15m), "Standard price per million format");
AssertEqual("$0.075", TaskCostCalculator.FormatPricePerMillion(0.075m), "Sub-cent price per million format");
AssertEqual("128K", TaskCostCalculator.FormatContextWindow(128_000), "Thousands context window");
AssertEqual("1M", TaskCostCalculator.FormatContextWindow(1_000_000), "Millions context window");
AssertEqual("2M", TaskCostCalculator.FormatContextWindow(2_000_000), "2M context window");
```

- [ ] **Step 3: Run test project to verify it fails to compile**

Run: `dotnet test` or `dotnet run --project tests/AiPriceWatch.Tests`
Expected: FAIL (`CS0234: The type or namespace name 'Models' does not exist in the namespace 'AiPriceWatch.Core'`)

- [ ] **Step 4: Implement `TaskTier`, `ModelPricingEntry`, and `TaskCostCalculator`**

Implement the records/enums in `src/AiPriceWatch.Core/Models/` and `TaskCostCalculator` in `src/AiPriceWatch.Core/Services/TaskCostCalculator.cs` using `CultureInfo.InvariantCulture`.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet run --project tests/AiPriceWatch.Tests`
Expected: PASS (`All tests passed`) with exit code `0`.

- [ ] **Step 6: Commit**

```bash
git add .gitignore AiPriceWatch.sln src/AiPriceWatch.Core tests/AiPriceWatch.Tests
git commit -m "feat(core): add model pricing domain types and task cost calculator"
```

---

### Task 2: Curated Pricing Catalog, Filtering, Sorting & Live OpenRouter Sync

**Files:**
- Create: `src/AiPriceWatch.Core/Data/pricing-catalog.json`
- Create: `src/AiPriceWatch.Core/Services/PricingSortField.cs`
- Create: `src/AiPriceWatch.Core/Services/PricingCatalogService.cs`
- Create: `src/AiPriceWatch.Core/Services/OpenRouterPriceParser.cs`
- Modify: `src/AiPriceWatch.Core/AiPriceWatch.Core.csproj`
- Modify: `tests/AiPriceWatch.Tests/Program.cs`

**Interfaces:**
- Consumes: `ModelPricingEntry`, `TaskTier` from Task 1
- Produces:
  - `namespace AiPriceWatch.Core.Services`
  - `public enum PricingSortField { InputPriceAsc, OutputPriceAsc, TotalTaskCostAsc, ContextDesc, NameAsc }`
  - `public sealed class PricingCatalogService`
    - `public static PricingCatalogService LoadDefault()`
    - `public static PricingCatalogService FromJson(string json)`
    - `public IReadOnlyList<ModelPricingEntry> AllModels { get; }`
    - `public IReadOnlyList<string> GetProviders()`
    - `public IReadOnlyList<ModelPricingEntry> Query(string? provider = null, TaskTier? tier = null, string? searchText = null, PricingSortField sort = PricingSortField.InputPriceAsc, int taskInputTokens = 10_000, int taskCachedTokens = 0, int taskOutputTokens = 2_000)`
    - `public void MergeEntries(IEnumerable<ModelPricingEntry> updatedEntries)`
  - `public static class OpenRouterPriceParser`
    - `public static IReadOnlyList<ModelPricingEntry> MergeLivePrices(IReadOnlyList<ModelPricingEntry> existingCatalog, string openRouterJson)`

- [ ] **Step 1: Write failing tests in `tests/AiPriceWatch.Tests/Program.cs` for catalog loading, provider/tier/search filtering, sorting, and OpenRouter JSON merging**

```csharp
var catalog = PricingCatalogService.LoadDefault();
AssertTrue(catalog.AllModels.Count >= 18, "Default catalog contains at least 18 models across major providers");
var providers = catalog.GetProviders();
AssertTrue(providers.Contains("OpenAI") && providers.Contains("Anthropic") && providers.Contains("Google Gemini") && providers.Contains("xAI") && providers.Contains("DeepSeek"), "Includes all 5 core providers");

// Case-insensitive & whitespace search + provider filter (Review Focus #4)
var anthropicFast = catalog.Query(provider: " anthropic ", tier: TaskTier.Fast, searchText: "HAIKU");
AssertTrue(anthropicFast.Count >= 1 && anthropicFast.All(m => m.Provider == "Anthropic" && m.Tier == TaskTier.Fast), "Filters by provider, tier, and case-insensitive search");

// Sorting by InputPriceAsc
var sortedByInput = catalog.Query(sort: PricingSortField.InputPriceAsc);
AssertTrue(sortedByInput.First().InputPerMillion <= sortedByInput.Last().InputPerMillion, "Sorts ascending by input price");

// OpenRouter live JSON merge & invalid price resilience (Review Focus #3)
string sampleOpenRouterJson = """
{
  "data": [
    {
      "id": "openai/gpt-4o-mini",
      "pricing": { "prompt": "0.00000012", "completion": "0.00000050", "input_cache_read": "0.00000006" }
    },
    {
      "id": "anthropic/claude-3.5-sonnet",
      "pricing": { "prompt": "-1", "completion": "invalid" }
    }
  ]
}
""";
var merged = OpenRouterPriceParser.MergeLivePrices(catalog.AllModels, sampleOpenRouterJson);
var updatedMini = merged.First(m => m.Id == "gpt-4o-mini");
AssertEqual(0.12m, updatedMini.InputPerMillion, "OpenRouter per-token prompt price converted to $/1M");
AssertEqual(0.50m, updatedMini.OutputPerMillion, "OpenRouter per-token completion price converted to $/1M");
AssertEqual(0.06m, updatedMini.CachedInputPerMillion, "OpenRouter per-token cache read price converted to $/1M");
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet run --project tests/AiPriceWatch.Tests`
Expected: FAIL (`PricingCatalogService` and `OpenRouterPriceParser` not defined)

- [ ] **Step 3: Create embedded `src/AiPriceWatch.Core/Data/pricing-catalog.json` and implement `PricingCatalogService` and `OpenRouterPriceParser`**

Embed `Data/pricing-catalog.json` as an `<EmbeddedResource>` in `AiPriceWatch.Core.csproj` with curated models for **OpenAI** (`GPT-4.1`, `GPT-4.1 mini`, `GPT-4.1 nano`, `GPT-4o`, `GPT-4o mini`, `o3`, `o4-mini`), **Anthropic** (`Claude Opus 4`, `Claude Sonnet 4`, `Claude 3.7 Sonnet`, `Claude 3.5 Haiku`), **Google Gemini** (`Gemini 2.5 Pro`, `Gemini 2.5 Flash`, `Gemini 2.5 Flash-Lite`, `Gemini 2.0 Flash`), **xAI** (`Grok 3`, `Grok 3 Mini`), and **DeepSeek** (`DeepSeek V3`, `DeepSeek R1`). Implement `PricingCatalogService` and `OpenRouterPriceParser` (matching OpenRouter `provider/model-id` suffixes to catalog `Id`s and multiplying per-token prices by `1_000_000m`, ignoring negative or unparseable values).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project tests/AiPriceWatch.Tests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/AiPriceWatch.Core tests/AiPriceWatch.Tests
git commit -m "feat(core): add embedded pricing catalog, query/sort engine, and OpenRouter sync parser"
```

---

### Task 3: HUD ViewModel & Task Presets

**Files:**
- Create: `src/AiPriceWatch.Core/ViewModels/TaskPreset.cs`
- Create: `src/AiPriceWatch.Core/ViewModels/ModelRowViewModel.cs`
- Create: `src/AiPriceWatch.Core/ViewModels/HudViewModel.cs`
- Modify: `tests/AiPriceWatch.Tests/Program.cs`

**Interfaces:**
- Consumes: `PricingCatalogService`, `TaskCostCalculator`, `OpenRouterPriceParser`, `ModelPricingEntry`, `TaskTier`, `PricingSortField`
- Produces:
  - `namespace AiPriceWatch.Core.ViewModels`
  - `public sealed record TaskPreset(string Name, int InputTokens, int CachedInputTokens, int OutputTokens, string Description)`
  - `public sealed class ModelRowViewModel` (exposes formatted strings: `Id`, `Name`, `Provider`, `TierBadge`, `TierCode`, `InputPriceDisplay`, `CachedPriceDisplay`, `OutputPriceDisplay`, `ContextDisplay`, `EstimatedTaskCostDisplay`, `EstimatedTaskCostRaw`, `Notes`, `IsCheapestInView`)
  - `public sealed class HudViewModel : INotifyPropertyChanged`
    - `public IReadOnlyList<string> Providers { get; }` (starts with `"All"`)
    - `public string SelectedProvider { get; set; }`
    - `public TaskTier? SelectedTier { get; set; }`
    - `public string SearchText { get; set; }`
    - `public PricingSortField SelectedSort { get; set; }`
    - `public IReadOnlyList<TaskPreset> Presets { get; }`
    - `public TaskPreset SelectedPreset { get; set; }`
    - `public int TaskInputTokens { get; set; }`
    - `public int TaskCachedTokens { get; set; }`
    - `public int TaskOutputTokens { get; set; }`
    - `public double BackgroundOpacity { get; set; }` (clamped to `[0.35, 1.0]`, default `0.86`)
    - `public bool IsAlwaysOnTop { get; set; }` (default `true`)
    - `public string StatusText { get; private set; }`
    - `public IReadOnlyList<ModelRowViewModel> VisibleModels { get; private set; }`

- [ ] **Step 1: Write failing tests for `HudViewModel` state transitions, preset changes, cheapest-badge calculation, and opacity clamping in `tests/AiPriceWatch.Tests/Program.cs`**

```csharp
var vm = new HudViewModel(PricingCatalogService.LoadDefault());
AssertEqual("All", vm.SelectedProvider, "Default provider is All");
AssertTrue(vm.VisibleModels.Count >= 18, "Shows all models initially");
AssertTrue(vm.VisibleModels.Any(m => m.IsCheapestInView), "Marks cheapest model in view");

// Provider switching updates VisibleModels
vm.SelectedProvider = "Google Gemini";
AssertTrue(vm.VisibleModels.All(m => m.Provider == "Google Gemini"), "Filtering by Google Gemini only shows Gemini models");

// Selecting preset updates token counts and recomputes task cost
var longContextPreset = vm.Presets.First(p => p.InputTokens >= 200_000);
vm.SelectedPreset = longContextPreset;
AssertEqual(longContextPreset.InputTokens, vm.TaskInputTokens, "Preset updates TaskInputTokens");

// Opacity clamping (Review Focus #5)
vm.BackgroundOpacity = 0.05;
AssertEqual(0.35, vm.BackgroundOpacity, "Clamps minimum opacity to 0.35");
vm.BackgroundOpacity = 1.5;
AssertEqual(1.0, vm.BackgroundOpacity, "Clamps maximum opacity to 1.0");
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet run --project tests/AiPriceWatch.Tests`
Expected: FAIL (`HudViewModel` not defined)

- [ ] **Step 3: Implement `TaskPreset`, `ModelRowViewModel`, and `HudViewModel`**

Implement `HudViewModel` in `src/AiPriceWatch.Core/ViewModels/HudViewModel.cs` with built-in task presets:
1. `Quick Prompt (4K in / 1K out)`
2. `Code Feature (50K in / 8K out)`
3. `Cached Agent Loop (20K new / 100K cached / 4K out)`
4. `Repo Audit (250K in / 16K out)`
And include `SyncLivePricesAsync(HttpClient httpClient, CancellationToken ct = default)` that fetches `https://openrouter.ai/api/v1/models` and updates the catalog via `OpenRouterPriceParser.MergeLivePrices`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project tests/AiPriceWatch.Tests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/AiPriceWatch.Core tests/AiPriceWatch.Tests
git commit -m "feat(core): implement HUD viewmodel, task presets, and live sync action"
```

---

### Task 4: Transparent WPF Desktop Overlay HUD (`AiPriceWatch.App`)

**Files:**
- Create: `src/AiPriceWatch.App/AiPriceWatch.App.csproj`
- Create: `src/AiPriceWatch.App/App.xaml`
- Create: `src/AiPriceWatch.App/App.xaml.cs`
- Create: `src/AiPriceWatch.App/Services/GlobalHotkeyService.cs`
- Create: `src/AiPriceWatch.App/MainWindow.xaml`
- Create: `src/AiPriceWatch.App/MainWindow.xaml.cs`
- Modify: `AiPriceWatch.sln`
- Modify: `README.md`

**Interfaces:**
- Consumes: `HudViewModel`, `ModelRowViewModel`, `TaskPreset`, `TaskTier`, `PricingSortField` from `AiPriceWatch.Core`
- Produces:
  - Executable WPF desktop application `src/AiPriceWatch.App` (`net10.0-windows`, `UseWPF=true`)
  - Terminal-sized (`760×480`) frameless translucent overlay window with:
    - Drag-to-move title bar, live `Opacity` slider (`35%–100%`) adjusting background alpha while keeping text 100% crisp, `Always-on-Top` pin button, `Sync Live` button, and minimize/close controls.
    - Provider filter bar (`All`, `OpenAI`, `Anthropic`, `Google Gemini`, `xAI`, `DeepSeek`) + keyboard shortcuts (`Alt+1`..`Alt+6`, `Ctrl+F` to focus search, `Esc` to clear search or hide HUD).
    - Tier filter pills (`All Tiers`, `Budget / Fast`, `Balanced`, `Reasoning`) + Sort dropdown.
    - Scrollable monospace-styled model pricing table displaying Model Name, Tier badge, Input $/1M, Cached $/1M, Output $/1M, Context Window, and highlighted **Est. Task Cost** (with a `BEST VALUE` badge on `IsCheapestInView`).
    - Footer task cost estimator bar with preset selector and interactive Input / Cached / Output token inputs.
    - Global Win32 hotkey `Ctrl+Shift+Space` via `GlobalHotkeyService` to summon/hide the overlay from anywhere.

- [ ] **Step 1: Create `src/AiPriceWatch.App/AiPriceWatch.App.csproj` and add it to `AiPriceWatch.sln`**

Configure `TargetFramework: net10.0-windows`, `UseWPF: true`, `OutputType: WinExe`, referencing `../AiPriceWatch.Core/AiPriceWatch.Core.csproj`.

- [ ] **Step 2: Implement `GlobalHotkeyService.cs`, `App.xaml`, `MainWindow.xaml`, and `MainWindow.xaml.cs`**

Build the translucent dark terminal HUD in `MainWindow.xaml` (`WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`, `Topmost="{Binding IsAlwaysOnTop}"`, `Width="760"`, `Height="480"`, `ResizeMode="CanResizeWithGrip"`) where the outer `Border` background color alpha channel binds to `BackgroundOpacity` so text stays 100% legible even at 35% background opacity. Wire `GlobalHotkeyService` (`RegisterHotKey` with `MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT` and `VK_SPACE`) and keyboard shortcuts in `MainWindow.xaml.cs`.

- [ ] **Step 3: Update `README.md` with build/run instructions, keyboard shortcuts, and architecture overview**

Document `dotnet build`, `dotnet run --project src/AiPriceWatch.App`, `dotnet run --project tests/AiPriceWatch.Tests`, and HUD shortcuts (`Ctrl+Shift+Space`, `Alt+1..6`, `Ctrl+F`, `Esc`).

- [ ] **Step 4: Build entire solution and run all tests to verify zero errors or warnings**

Run: `dotnet build AiPriceWatch.sln -c Release && dotnet run --project tests/AiPriceWatch.Tests`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` and `All tests passed`.

- [ ] **Step 5: Commit**

```bash
git add AiPriceWatch.sln src/AiPriceWatch.App README.md docs/superpowers
git commit -m "feat(app): build transparent WPF desktop overlay HUD and global hotkey"
```
