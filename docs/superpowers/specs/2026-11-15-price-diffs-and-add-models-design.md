# Price Change Diffs & OpenRouter Model Discovery Design

**Date:** 2026-11-15
**Status:** Draft (Pending Approval)
**Target:** AI PriceWatch (`AiPriceWatch.Core` & `AiPriceWatch.App`)

## 1. Intent & Why

Currently, `SYNC LIVE` fetches the full OpenRouter `/api/v1/models` catalog, updates standard/cached prices only for models already in the bundled `pricing-catalog.json`, and discards everything else in memory. Users have no visibility into **what prices changed** since their last sync, and no way to **discover or add** models that OpenRouter already serves without cluttering the default HUD table.

This feature adds:
1. **Price Change Diffs:** Surface `↓ -X%` / `↑ +X%` delta badges on tracked models when live sync detects a price change, persisted across restarts.
2. **New Model Discovery & "Add Models":** Retain parsed OpenRouter catalog entries in a local snapshot and provide an in-HUD **Add Models** overlay so users can browse unseen/new OpenRouter models and add them to their HUD list on demand.

## 2. Goals, Non-Goals & Anti-Goals

### Goals
- Detect and display percentage price changes (Input and/or Output `$/1M`) on tracked models after `SYNC LIVE`.
- Persist the last-synced baseline and user-added OpenRouter models locally in `%LOCALAPPDATA%\AiPriceWatch\sync-snapshot.json` so diffs and added models survive restarts and work offline.
- Provide an in-HUD **+ ADD** overlay (matching the existing Settings overlay pattern) with search, provider filter, `NEW` badge for models not seen in the prior sync, and 1-click Add / Remove.
- Preserve all existing provenance, sorting, cost calculation, and hide/unhide behaviors for user-added models.

### Non-Goals
- Automatically dumping all 200+ OpenRouter models into the main HUD table.
- Background/scheduled polling (network calls remain strictly manual via `SYNC LIVE` / `Retry Sync`).
- Fetching AAII `IntelligenceScore` or Fast/Priority pricing for arbitrary OpenRouter models (those remain `N/A`).

### Anti-Goals
- Manufacturing `TaskTier` or `Fast` multipliers for OpenRouter-discovered models (must obey strict `N/A` semantics).
- Breaking startup or corrupting the HUD if `sync-snapshot.json` is missing, stale, or malformed (must degrade gracefully to bundled catalog).

## 3. Constraints
- **Offline-first & atomic persistence:** `sync-snapshot.json` must use temp-file + `File.Move(..., overwrite: true)` (matching `UserPreferencesStore`) and silently fall back on read/write errors.
- **Strict Fast/Priority `N/A` rule (PW-02):** User-added OpenRouter models have `FastInputPerMillion = null`, `FastOutputPerMillion = null`, `IntelligenceScore = null`.
- **Provider tab compatibility:** Provider names parsed from OpenRouter IDs (e.g., `anthropic/...` → `Anthropic`, `google/...` → `Google Gemini`, `openai/...` → `OpenAI`, `x-ai/...` → `xAI`, `deepseek/...` → `DeepSeek`) should map to existing HUD provider tabs where applicable, and otherwise use a clean capitalized provider label (visible under `All` and searchable).
- **Zero new external NuGet dependencies:** Use `System.Text.Json` and existing WPF primitives.

## 4. Approaches Considered

| Approach | Summary | Trade-offs | Decision |
|---|---|---|---|
| **A: Persisted Sync Snapshot + In-HUD "Add Models" Drawer** | Persist `sync-snapshot.json` in `%LOCALAPPDATA%\AiPriceWatch\` with last-synced prices, user-added `ModelPricingEntry` items, and previously seen OpenRouter IDs. Compute price & new-model diffs against the persisted snapshot. | Adds one local cache file, but user-added models work offline on restart and price diffs persist across launches. | **Selected** |
| **B: Session-Only Diffs + ID-Only Persistence** | Store only `AddedOpenRouterIds` in `user-preferences.json`; compute diffs in memory against the bundled catalog. | No extra cache file, but added models lack prices offline on restart and price diffs reset every relaunch. | Rejected |

## 5. Architecture & Design Details

### 5.1 OpenRouter Feed Parsing (`OpenRouterPriceParser`)
Extend `OpenRouterPriceParser` to capture full metadata for every valid OpenRouter model in `data[]` (not just rates):
- `OpenRouterModelCandidate`: `FullId` (e.g. `openai/gpt-5-mini`), `Name` (fallback to `FullId`), `ProviderSlug` / mapped `Provider`, `InputPerMillion`, `OutputPerMillion`, `CachedInputPerMillion`, `ContextTokens` (from `context_length`, default `0` if absent).
- Existing `MergeLivePricesDetailed` continues to update matching models in the active catalog, and now also returns `IReadOnlyList<OpenRouterModelCandidate> AvailableModels`.

### 5.2 Sync Snapshot & Diff Engine (`SyncSnapshotStore` & `CatalogDiffCalculator`)
Stored at `%LOCALAPPDATA%\AiPriceWatch\sync-snapshot.json` (`SchemaVersion = 1`):
- `LastSyncedAt`: `DateTimeOffset?`
- `KnownOpenRouterIds`: `HashSet<string>` (all OpenRouter `FullId`s seen in the previous sync; on first-ever sync, seeded with all IDs from that sync so the user isn't flooded with 200+ `NEW` badges on day one—or flagged as "first sync"). *Builder call: on first sync with no prior snapshot, mark `NewOpenRouterIds` empty and only surface `NEW` badges on subsequent syncs.*
- `LastPricesByModelId`: `Dictionary<string, PriceSnapshot>` (`InputPerMillion`, `OutputPerMillion`, `CachedInputPerMillion`).
- `AddedModels`: `List<ModelPricingEntry>` (user-selected OpenRouter models, refreshed on each `SYNC LIVE` and loaded at startup alongside `pricing-catalog.json`).
- `ActivePriceDiffs`: `Dictionary<string, PriceDiff>` (`PreviousInputPerMillion`, `PreviousOutputPerMillion`, `PercentChange`, `DetectedAt`) persisted until the user clears diffs or a subsequent sync supersedes them.

**Diff calculation rule:**
- Compare new `InputPerMillion` + `OutputPerMillion` against prior snapshot (or bundled price if no prior snapshot exists for that model).
- Compute `PercentChange` based on blended/max-relevant change (specifically: use standard `OutputPerMillion` if changed, or `InputPerMillion` if only input changed; show exact old → new for both Input and Output in the tooltip). Ignore negligible rounding noise (`< 0.1%`).

### 5.3 ViewModel & Catalog Integration (`HudViewModel`, `ModelRowViewModel`, `PricingCatalogService`)
- **Startup:** `HudViewModel` loads `PricingCatalogService.LoadDefault()`, merges any `AddedModels` and `LastPricesByModelId` from `SyncSnapshotStore`, and restores `ActivePriceDiffs`.
- **On `SYNC LIVE`:**
  1. Compute `PriceDiff`s against the previous snapshot.
  2. Identify `NewOpenRouterIds` (IDs in `AvailableModels` not in `KnownOpenRouterIds` and not already in the catalog).
  3. Update `AddedModels` prices if they exist in `AvailableModels`.
  4. Save updated `sync-snapshot.json`.
  5. Update status bar summary: e.g., `Live sync OK • 24 prices • 3 changed • 4 new on OpenRouter`.
- **Row UI (`ModelRowViewModel`):**
  - Expose `PriceDiffBadge` (e.g., `↓ -25%`, `↑ +12%`), `IsPriceDrop`, `IsPriceIncrease`, `HasPriceDiff`, `PriceDiffTooltip` (`Input: $X → $Y | Output: $A → $B`), and `IsUserAdded` (allowing a `Remove` action or badge).
- **Add Models Overlay (`HudViewModel` + `MainWindow.xaml`):**
  - Top bar `+ ADD` button (shows a `NEW` count pill when `NewOpenRouterCount > 0`).
  - Overlay modal (matching `SettingsOverlay` style, `Esc` to close) with:
    - Search box + "New since last sync" toggle + Provider filter.
    - List of `OpenRouterCandidateViewModel` rows showing Name, Provider, In/Out `$/1M`, Context, `NEW` badge, and **Add** / **Added (Remove)** toggle button.
    - Note when `AvailableModels` is empty (pre-first-sync): prompt user to click `SYNC LIVE` to fetch the OpenRouter model list.

### 5.4 Error Handling & Edge Cases
- **Corrupt or missing `sync-snapshot.json`:** Silently ignored; app boots with bundled `pricing-catalog.json`.
- **Failed `SYNC LIVE`:** Existing `AddedModels`, `ActivePriceDiffs`, and `KnownOpenRouterIds` remain untouched.
- **Duplicate ID collision:** Adding an OpenRouter model whose ID matches a bundled catalog entry is prevented (bundled entry takes precedence).
- **Removing a user-added model:** Removes it from `AddedModels`, `_catalog`, and `sync-snapshot.json` immediately.

## 6. Left to Implementation / Verification
- Exact WPF XAML styling/colors for the delta pill (`#3FB950` for price drops, `#D29922` for increases) and `+ ADD` modal layout.
- Unit tests in `tests/AiPriceWatch.Tests` covering: `OpenRouterPriceParser` candidate extraction, `SyncSnapshotStore` round-trip & corrupt-file fallback, `CatalogDiffCalculator` drop/increase/no-op thresholds, and `HudViewModel` add/remove/sync flows.
