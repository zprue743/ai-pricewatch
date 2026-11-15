# Price Change Diffs & OpenRouter Model Discovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist OpenRouter sync snapshots to compute and display price change diffs (`↓ -X%` / `↑ +X%`) on tracked models and let users browse and add/remove models from the OpenRouter feed inside the HUD.

**Architecture:** Extend `OpenRouterPriceParser` to return all valid `OpenRouterModelCandidate` entries alongside updated catalog models. Introduce `SyncSnapshotStore` (persisting `%LOCALAPPDATA%\AiPriceWatch\sync-snapshot.json`) and `CatalogDiffCalculator` in `AiPriceWatch.Core` to track prior prices, seen OpenRouter IDs, user-added models, and active price diffs across restarts. Wire diff badges and the `+ ADD` modal into `HudViewModel`, `ModelRowViewModel`, and `MainWindow.xaml`.

**Tech Stack:** .NET 10 (`net10.0-windows`), C# 13, WPF, `System.Text.Json`, custom console test runner (`tests/AiPriceWatch.Tests`).

**Spec:** [2026-11-15-price-diffs-and-add-models-design.md](../specs/2026-11-15-price-diffs-and-add-models-design.md)

## Global Constraints

- **Offline-first & atomic persistence:** `sync-snapshot.json` uses temp-file + `File.Move(..., overwrite: true)` (matching `UserPreferencesStore`) and silently degrades to empty/default state on read/write errors.
- **Strict Fast/Priority `N/A` rule (PW-02):** User-added OpenRouter models have `FastInputPerMillion = null`, `FastOutputPerMillion = null`, and `IntelligenceScore = null`. Never manufacture Fast multipliers or `TaskTier` estimates.
- **No new external NuGet dependencies:** Use `System.Text.Json` and existing WPF primitives.
- **Manual network calls only:** No background polling; OpenRouter is contacted only on `SYNC LIVE` / `Retry Sync`.
- **Diff noise floor:** Percentage price changes with `Math.Abs(percentChange) < 0.1m` are ignored.

## Review Focus

- **Zero-price division in diff calculation:** A model transitioning from `$0.00` (free tier) to a paid rate (`> $0`) must not throw `DivideByZeroException`; treat `previous == 0m && current > 0m` as `+100%` (or handle safely without NaN/exception).
- **First-ever sync flooding `NEW` badges:** When no prior `KnownOpenRouterIds` exist in `sync-snapshot.json`, seed `KnownOpenRouterIds` with all fetched IDs and report `0` new models so day-one users are not flooded with 200+ `NEW` badges.
- **Corrupt or partial `sync-snapshot.json`:** Invalid JSON or null fields on disk must never crash startup; `SyncSnapshotStore.Load()` must return a clean `new SyncSnapshot()` and let the bundled catalog load normally.
- **Bundled vs. user-added ID collision:** Attempting to add an OpenRouter model whose ID or `OpenRouterId` already exists in the bundled catalog must be blocked/no-op so bundled metadata (AAII score, Fast pricing, tier) is never overwritten.
- **Failed `SYNC LIVE` preservation:** When `SYNC LIVE` fails (network timeout or invalid JSON), existing `AddedModels`, `ActivePriceDiffs`, and `KnownOpenRouterIds` in memory and on disk must remain intact.

---

### Task 1: Extract Full `OpenRouterModelCandidate` Metadata in `OpenRouterPriceParser`

**Files:**
- Modify: `src/AiPriceWatch.Core/Services/OpenRouterPriceParser.cs`
- Test: `tests/AiPriceWatch.Tests/Program.cs`

**Interfaces:**
- Produces:
  - `public sealed record OpenRouterModelCandidate(string FullId, string Name, string Provider, decimal InputPerMillion, decimal OutputPerMillion, decimal? CachedInputPerMillion, int ContextTokens)`
  - `OpenRouterSyncResult` extended with `IReadOnlyList<OpenRouterModelCandidate> AvailableModels` (default `Array.Empty<OpenRouterModelCandidate>()`)
  - `public static string MapProviderName(string fullId)` on `OpenRouterPriceParser`

- [ ] **Step 1: Write the failing test in `tests/AiPriceWatch.Tests/Program.cs`**

Add assertions verifying `AvailableModels` captures unmatched OpenRouter entries (name, mapped provider, context length, input/output/cached rates) and maps known slugs (`anthropic/...` → `Anthropic`, `google/...` → `Google Gemini`, `openai/...` → `OpenAI`, `x-ai/...` → `xAI`, `deepseek/...` → `DeepSeek`, `mistralai/...` → `Mistralai`).

```csharp
string candidateJson = """
{
  "data": [
    {
      "id": "anthropic/claude-sonnet-4.6",
      "name": "Anthropic: Claude Sonnet 4.6",
      "context_length": 200000,
      "pricing": { "prompt": "0.000003", "completion": "0.000015", "input_cache_read": "0.0000003" }
    },
    {
      "id": "mistralai/mistral-large-2512",
      "name": "Mistral Large",
      "context_length": 131072,
      "pricing": { "prompt": "0.000002", "completion": "0.000006" }
    }
  ]
}
""";
OpenRouterSyncResult candidateResult = OpenRouterPriceParser.MergeLivePricesDetailed(catalog.AllModels, candidateJson);
AssertTrue(candidateResult.IsValidPayload, "Candidate extraction payload valid");
AssertEqual(2, candidateResult.AvailableModels.Count, "AvailableModels captures all valid OpenRouter entries");
var mistral = candidateResult.AvailableModels.First(m => m.FullId == "mistralai/mistral-large-2512");
AssertEqual("Mistral Large", mistral.Name, "Candidate name parsed");
AssertEqual("Mistralai", mistral.Provider, "Unknown provider slug capitalized");
AssertEqual(131072, mistral.ContextTokens, "Candidate context_length parsed");
AssertEqual(2.00m, mistral.InputPerMillion, "Candidate input price parsed");
AssertEqual("Google Gemini", OpenRouterPriceParser.MapProviderName("google/gemini-2.5-pro"), "Google slug maps to Google Gemini");
```

- [ ] **Step 2: Run tests to verify it fails**

Run: `dotnet run --project tests/AiPriceWatch.Tests -c Release`
Expected: FAIL with compile error (`AvailableModels` / `MapProviderName` not defined).

- [ ] **Step 3: Implement `OpenRouterModelCandidate`, `MapProviderName`, and `AvailableModels` in `src/AiPriceWatch.Core/Services/OpenRouterPriceParser.cs`**

Update `OpenRouterSyncResult` to include `IReadOnlyList<OpenRouterModelCandidate>? AvailableModels = null` (or optional parameter defaulting to `null` / `Array.Empty<OpenRouterModelCandidate>()` so existing callers remain compatible). In `MergeLivePricesDetailed`, parse optional `name` (fallback to `fullId`) and `context_length` (int, default `0`), map provider via `MapProviderName(fullId)`, and populate `AvailableModels` in `OpenRouterSyncResult`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project tests/AiPriceWatch.Tests -c Release`
Expected: PASS (all existing + new assertions).

- [ ] **Step 5: Commit**

```powershell
git add src/AiPriceWatch.Core/Services/OpenRouterPriceParser.cs tests/AiPriceWatch.Tests/Program.cs
git commit -m "feat(core): extract OpenRouterModelCandidate metadata during live sync"
```

---

### Task 2: Add `SyncSnapshotStore` for Atomic Persistence

**Files:**
- Create: `src/AiPriceWatch.Core/Services/SyncSnapshotStore.cs`
- Test: `tests/AiPriceWatch.Tests/Program.cs`

**Interfaces:**
- Produces:
  - `public sealed record PriceSnapshot(decimal InputPerMillion, decimal OutputPerMillion, decimal? CachedInputPerMillion)`
  - `public sealed record PriceDiff(decimal PreviousInputPerMillion, decimal PreviousOutputPerMillion, decimal NewInputPerMillion, decimal NewOutputPerMillion, decimal PercentChange, DateTimeOffset DetectedAt)`
  - `public sealed record SyncSnapshot` with `SchemaVersion = 1`, `DateTimeOffset? LastSyncedAt`, `HashSet<string> KnownOpenRouterIds`, `Dictionary<string, PriceSnapshot> LastPricesByModelId`, `List<ModelPricingEntry> AddedModels`, `Dictionary<string, PriceDiff> ActivePriceDiffs`
  - `public sealed class SyncSnapshotStore(string? filePath = null)` with `SyncSnapshot Load()`, `void Save(SyncSnapshot snapshot)`, `void ClearDiffs()`

- [ ] **Step 1: Write the failing test in `tests/AiPriceWatch.Tests/Program.cs`**

Test round-trip `Save` / `Load` with a temp file path, corrupt JSON fallback returning an empty `SyncSnapshot`, and `ClearDiffs()` clearing `ActivePriceDiffs` while preserving `AddedModels` and `KnownOpenRouterIds`.

```csharp
string tempSnapshotPath = Path.Combine(Path.GetTempPath(), $"apw-snapshot-{Guid.NewGuid():N}.json");
try
{
    var snapshotStore = new SyncSnapshotStore(tempSnapshotPath);
    var addedEntry = new ModelPricingEntry(
        "mistralai/mistral-large-2512", "Mistral Large", "Mistralai", TaskTier.Balanced,
        2.00m, null, 6.00m, 131072, "Added from OpenRouter", OpenRouterId: "mistralai/mistral-large-2512");
    var snap = new SyncSnapshot
    {
        LastSyncedAt = DateTimeOffset.UtcNow,
        KnownOpenRouterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mistralai/mistral-large-2512" },
        LastPricesByModelId = new Dictionary<string, PriceSnapshot>(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-sonnet-4.6"] = new PriceSnapshot(3.00m, 15.00m, 0.30m)
        },
        AddedModels = new List<ModelPricingEntry> { addedEntry },
        ActivePriceDiffs = new Dictionary<string, PriceDiff>(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-sonnet-4.6"] = new PriceDiff(4.00m, 20.00m, 3.00m, 15.00m, -25.0m, DateTimeOffset.UtcNow)
        }
    };
    snapshotStore.Save(snap);
    SyncSnapshot loadedSnap = snapshotStore.Load();
    AssertEqual(1, loadedSnap.AddedModels.Count, "SyncSnapshotStore round-trips AddedModels");
    AssertTrue(loadedSnap.KnownOpenRouterIds.Contains("MISTRALAI/MISTRAL-LARGE-2512"), "KnownOpenRouterIds comparer is case-insensitive");
    AssertEqual(-25.0m, loadedSnap.ActivePriceDiffs["claude-sonnet-4.6"].PercentChange, "ActivePriceDiffs round-tripped");

    snapshotStore.ClearDiffs();
    SyncSnapshot afterClear = snapshotStore.Load();
    AssertEqual(0, afterClear.ActivePriceDiffs.Count, "ClearDiffs clears ActivePriceDiffs");
    AssertEqual(1, afterClear.AddedModels.Count, "ClearDiffs preserves AddedModels");

    File.WriteAllText(tempSnapshotPath, "{ invalid json");
    SyncSnapshot corruptFallback = snapshotStore.Load();
    AssertEqual(0, corruptFallback.AddedModels.Count, "Corrupt snapshot returns empty SyncSnapshot");
}
finally
{
    if (File.Exists(tempSnapshotPath)) File.Delete(tempSnapshotPath);
}
```

- [ ] **Step 2: Run tests to verify it fails**

Run: `dotnet run --project tests/AiPriceWatch.Tests -c Release`
Expected: FAIL with compile error (`SyncSnapshotStore` not defined).

- [ ] **Step 3: Implement `SyncSnapshotStore` in `src/AiPriceWatch.Core/Services/SyncSnapshotStore.cs`**

Follow `UserPreferencesStore` patterns: `%LOCALAPPDATA%\AiPriceWatch\sync-snapshot.json` default path, `JsonStringEnumConverter`, atomic temp-file + `File.Move(..., overwrite: true)`, normalize collections with `StringComparer.OrdinalIgnoreCase` on load, and silent fallback on `Load()` / `Save()` errors.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project tests/AiPriceWatch.Tests -c Release`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/AiPriceWatch.Core/Services/SyncSnapshotStore.cs tests/AiPriceWatch.Tests/Program.cs
git commit -m "feat(core): add SyncSnapshotStore for atomic diff and custom model persistence"
```

---

### Task 3: Add `CatalogDiffCalculator` for Price Deltas & New OpenRouter IDs

**Files:**
- Create: `src/AiPriceWatch.Core/Services/CatalogDiffCalculator.cs`
- Test: `tests/AiPriceWatch.Tests/Program.cs`

**Interfaces:**
- Consumes: `ModelPricingEntry`, `OpenRouterModelCandidate`, `PriceSnapshot`, `PriceDiff`
- Produces:
  - `public sealed record CatalogDiffResult(Dictionary<string, PriceDiff> PriceDiffs, HashSet<string> NewOpenRouterIds, Dictionary<string, PriceSnapshot> UpdatedPriceSnapshots, HashSet<string> UpdatedKnownOpenRouterIds)`
  - `public static class CatalogDiffCalculator` with `ComputeDiffs(IReadOnlyList<ModelPricingEntry> previousModels, IReadOnlyList<ModelPricingEntry> currentModels, IReadOnlyList<OpenRouterModelCandidate> availableCandidates, IReadOnlyDictionary<string, PriceSnapshot> priorPrices, IReadOnlySet<string> priorKnownOpenRouterIds, DateTimeOffset now)`

- [ ] **Step 1: Write the failing test in `tests/AiPriceWatch.Tests/Program.cs`**

Cover:
1. Price drop (`$10.00` → `$7.50` = `-25.0%`), price increase (`$1.00` → `$1.15` = `+15.0%`), rounding noise (`$1.00` → `$1.0005` = ignored), and `$0.00` → `$1.00` (`+100.0%`, no divide-by-zero).
2. First-sync seeding (`priorKnownOpenRouterIds` empty → `NewOpenRouterIds` empty, `UpdatedKnownOpenRouterIds` populated).
3. Subsequent sync (`priorKnownOpenRouterIds` non-empty → unseen `FullId` not in catalog appears in `NewOpenRouterIds`).

```csharp
var prevModels = new List<ModelPricingEntry> { sampleFastModel };
var currModels = new List<ModelPricingEntry> { sampleFastModel with { OutputPerMillion = 1.50m } };
var priorPrices = new Dictionary<string, PriceSnapshot>(StringComparer.OrdinalIgnoreCase)
{
    [sampleFastModel.Id] = new PriceSnapshot(0.25m, 2.00m, 0.025m)
};
var candidates = new List<OpenRouterModelCandidate>
{
    new("openai/gpt-5-mini", "GPT-5 Mini", "OpenAI", 0.25m, 1.50m, 0.025m, 1_000_000),
    new("cohere/command-a", "Command A", "Cohere", 2.50m, 10.00m, null, 256_000)
};

// First sync: no prior known IDs -> 0 NewOpenRouterIds
CatalogDiffResult firstSync = CatalogDiffCalculator.ComputeDiffs(
    prevModels, currModels, candidates, priorPrices, new HashSet<string>(StringComparer.OrdinalIgnoreCase), DateTimeOffset.UtcNow);
AssertEqual(-25.0m, firstSync.PriceDiffs[sampleFastModel.Id].PercentChange, "Price drop computed as -25%");
AssertEqual(0, firstSync.NewOpenRouterIds.Count, "First sync seeds known IDs without flooding NEW badges");
AssertEqual(2, firstSync.UpdatedKnownOpenRouterIds.Count, "First sync records all seen OpenRouter IDs");

// Subsequent sync with new model
var candidates2 = candidates.Concat(new[] {
    new OpenRouterModelCandidate("meta-llama/llama-4-maverick", "Llama 4 Maverick", "Meta-llama", 0.20m, 0.60m, null, 1_000_000)
}).ToList();
CatalogDiffResult secondSync = CatalogDiffCalculator.ComputeDiffs(
    prevModels, currModels, candidates2, priorPrices, firstSync.UpdatedKnownOpenRouterIds, DateTimeOffset.UtcNow);
AssertTrue(secondSync.NewOpenRouterIds.Contains("meta-llama/llama-4-maverick"), "Subsequent sync flags unseen OpenRouter ID as NEW");

// Zero-price to paid transition
var freePrior = new Dictionary<string, PriceSnapshot>(StringComparer.OrdinalIgnoreCase)
{
    [sampleFastModel.Id] = new PriceSnapshot(0m, 0m, null)
};
CatalogDiffResult freeToPaid = CatalogDiffCalculator.ComputeDiffs(
    prevModels, currModels, candidates2, freePrior, firstSync.UpdatedKnownOpenRouterIds, DateTimeOffset.UtcNow);
AssertEqual(100.0m, freeToPaid.PriceDiffs[sampleFastModel.Id].PercentChange, "Zero-to-paid transition returns +100% without DivideByZeroException");
```

- [ ] **Step 2: Run tests to verify it fails**

Run: `dotnet run --project tests/AiPriceWatch.Tests -c Release`
Expected: FAIL with compile error (`CatalogDiffCalculator` not defined).

- [ ] **Step 3: Implement `CatalogDiffCalculator` in `src/AiPriceWatch.Core/Services/CatalogDiffCalculator.cs`**

For each model in `currentModels`:
- Look up `prior` in `priorPrices` (or fall back to matching `previousModels` entry).
- Compare `OutputPerMillion` first; if unchanged (diff `< 0.1%`), compare `InputPerMillion`.
- Compute `pct = prev == 0m ? 100m : Math.Round(((curr - prev) / prev) * 100m, 1)`. Only emit `PriceDiff` if `Math.Abs(pct) >= 0.1m`.
- For `NewOpenRouterIds`: if `priorKnownOpenRouterIds.Count > 0`, include every candidate `FullId` not in `priorKnownOpenRouterIds` and not already matching an existing model's `OpenRouterId`/`Id`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project tests/AiPriceWatch.Tests -c Release`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/AiPriceWatch.Core/Services/CatalogDiffCalculator.cs tests/AiPriceWatch.Tests/Program.cs
git commit -m "feat(core): add CatalogDiffCalculator for price deltas and new OpenRouter IDs"
```

---

### Task 4: Wire Diffs, `AddedModels`, and `+ ADD` Picker State into `ModelRowViewModel` & `HudViewModel`

**Files:**
- Modify: `src/AiPriceWatch.Core/ViewModels/ModelRowViewModel.cs`
- Modify: `src/AiPriceWatch.Core/ViewModels/HudViewModel.cs`
- Test: `tests/AiPriceWatch.Tests/Program.cs`

**Interfaces:**
- Consumes: `SyncSnapshotStore`, `CatalogDiffCalculator`, `OpenRouterModelCandidate`, `PriceDiff`
- Produces:
  - `ModelRowViewModel`: `PriceDiffBadge` (`string`), `HasPriceDiff` (`bool`), `IsPriceDrop` (`bool`), `IsPriceIncrease` (`bool`), `PriceDiffTooltip` (`string`), `IsUserAdded` (`bool`)
  - `OpenRouterCandidateViewModel`: `FullId`, `Name`, `Provider`, `InOutDisplay`, `ContextDisplay`, `IsNew`, `IsAdded`, `CanAdd`
  - `HudViewModel`:
    - Constructor accepts optional `SyncSnapshotStore? snapshotStore = null`
    - Properties: `NewOpenRouterCount`, `HasNewOpenRouterModels`, `IsAddModelsOpen`, `AddModelsSearchText`, `OnlyNewCandidates`, `AvailableCandidates` (`IReadOnlyList<OpenRouterCandidateViewModel>`), `HasPriceDiffs`
    - Methods: `bool AddOpenRouterModel(string fullId)`, `bool RemoveAddedModel(string modelId)`, `void ClearPriceDiffs()`

- [ ] **Step 1: Write the failing tests in `tests/AiPriceWatch.Tests/Program.cs`**

Test:
1. `HudViewModel` constructor loads `AddedModels` & `ActivePriceDiffs` from `SyncSnapshotStore` on startup.
2. `SyncLivePricesAsync` (with mocked `HttpClient` returning OpenRouter JSON) computes `PriceDiff`s, updates `NewOpenRouterCount`, and persists `sync-snapshot.json`.
3. `AddOpenRouterModel` adds a candidate to `_catalog` and `AddedModels`, enforces strict `N/A` Fast/Score fields, blocks adding a duplicate bundled model ID, and `RemoveAddedModel` removes it.
4. `ClearPriceDiffs()` clears `HasPriceDiffs` and row `HasPriceDiff` flags.

- [ ] **Step 2: Run tests to verify it fails**

Run: `dotnet run --project tests/AiPriceWatch.Tests -c Release`
Expected: FAIL with compile error (missing properties/methods on `ModelRowViewModel` / `HudViewModel`).

- [ ] **Step 3: Update `ModelRowViewModel.cs` and `HudViewModel.cs`**

- In `ModelRowViewModel`: add optional `PriceDiff? priceDiff = null` and `bool isUserAdded = false` constructor parameters. Format `PriceDiffBadge` as `$"↓ {diff.PercentChange:0.#}%"` or `$"↑ +{diff.PercentChange:0.#}%"`, and `PriceDiffTooltip` showing `Input: $X → $Y | Output: $A → $B`.
- In `HudViewModel`:
  - Inject/create `SyncSnapshotStore` (default `new SyncSnapshotStore()`).
  - On startup, load `SyncSnapshot`, merge `AddedModels` into `_catalog` (skipping any IDs already in bundled catalog), apply `LastPricesByModelId` if needed, and restore `_activePriceDiffs`.
  - In `SyncLivePricesAsync`: on valid OpenRouter payload, run `CatalogDiffCalculator.ComputeDiffs(...)`, update `_availableCandidates`, `_newOpenRouterIds`, `_activePriceDiffs`, refresh `AddedModels` prices, save `SyncSnapshot`, and append diff/new counts to `StatusText` (e.g., `• 2 changed • 3 new`). On failure, leave snapshot untouched.
  - Implement `AddOpenRouterModel(string fullId)`, `RemoveAddedModel(string modelId)`, `ClearPriceDiffs()`, and filtered `AvailableCandidates` property.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project tests/AiPriceWatch.Tests -c Release`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/AiPriceWatch.Core/ViewModels/ModelRowViewModel.cs src/AiPriceWatch.Core/ViewModels/HudViewModel.cs tests/AiPriceWatch.Tests/Program.cs
git commit -m "feat(core): integrate price diffs and OpenRouter model add/remove into HudViewModel"
```

---

### Task 5: Add Price Diff Badges & `+ ADD` Overlay UI in `MainWindow.xaml`

**Files:**
- Modify: `src/AiPriceWatch.App/MainWindow.xaml`
- Modify: `src/AiPriceWatch.App/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `HudViewModel.IsAddModelsOpen`, `HudViewModel.NewOpenRouterCount`, `HudViewModel.HasNewOpenRouterModels`, `HudViewModel.AddModelsSearchText`, `HudViewModel.OnlyNewCandidates`, `HudViewModel.AvailableCandidates`, `HudViewModel.AddOpenRouterModel`, `HudViewModel.RemoveAddedModel`, `HudViewModel.ClearPriceDiffs`, `ModelRowViewModel.PriceDiffBadge`, `ModelRowViewModel.HasPriceDiff`, `ModelRowViewModel.IsPriceDrop`, `ModelRowViewModel.IsPriceIncrease`, `ModelRowViewModel.PriceDiffTooltip`, `ModelRowViewModel.IsUserAdded`

- [ ] **Step 1: Add `+ ADD` button (with `NEW` badge) and `Clear Diffs` action in `MainWindow.xaml` top bar / status bar**

- Place `+ ADD` button next to `SYNC LIVE` in the top bar, showing a small accent pill when `HasNewOpenRouterModels` is true (`{Binding NewOpenRouterCount} NEW`).
- Add a subtle `Clear diffs` button in the bottom status bar visible when `HasPriceDiffs` is true.

- [ ] **Step 2: Add inline price diff pill and user-added remove action to the model row template in `MainWindow.xaml`**

- Inside the model name/pricing cell, render a compact pill bound to `PriceDiffBadge` (`Visibility="{Binding HasPriceDiff, Converter=...}"`, green `#3FB950` for `IsPriceDrop`, amber `#D29922` for `IsPriceIncrease`, `ToolTip="{Binding PriceDiffTooltip}"`).
- For rows where `IsUserAdded` is true, allow removing via a compact `Remove` action or tooltip-documented button.

- [ ] **Step 3: Add `AddModelsOverlay` modal in `MainWindow.xaml` and handlers in `MainWindow.xaml.cs`**

- Mirror `SettingsOverlay` (`Grid.RowSpan="3"`, dark translucent backdrop, `Esc` closes overlay).
- Include search `TextBox` (`AddModelsSearchText`), `"New since last sync"` `CheckBox` (`OnlyNewCandidates`), `ItemsControl` / `ListView` bound to `AvailableCandidates` showing Name, Provider, In/Out `$/1M`, Context, `NEW` badge, and **Add** / **Remove** button (`Tag="{Binding FullId}"`).
- Show a friendly empty-state message when `AvailableCandidates.Count == 0` prompting the user to click `SYNC LIVE` first.

- [ ] **Step 4: Build & run full test suite + verify app builds cleanly**

Run:
```powershell
dotnet build AiPriceWatch.sln -c Release -m:1
dotnet run --project tests/AiPriceWatch.Tests -c Release
```
Expected: `Build succeeded. 0 Error(s)` and all tests PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/AiPriceWatch.App/MainWindow.xaml src/AiPriceWatch.App/MainWindow.xaml.cs
git commit -m "feat(app): add price diff badges and OpenRouter Add Models overlay in HUD"
```
