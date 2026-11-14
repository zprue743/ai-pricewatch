# AI PriceWatch

A lightweight, terminal-sized (`860×510`) transparent desktop HUD overlay for Windows that lets you select an AI provider, compare model token pricing (`$/1M` in both **Standard** and **Fast / Priority** modes), inspect published **Artificial Analysis Intelligence Index (AAII)** benchmark scores, filter by task tier, and calculate exact workload costs at a glance.

## Features

- **Translucent Terminal HUD (`860×510` default, resizable):** Frameless dark overlay with an independent background opacity slider (`35%–100%`) that keeps text 100% crisp while letting you see your active windows behind it.
- **Provider Selector Tabs (`Alt+1..7`):** Instant switching across **All**, **Anthropic**, **Google Gemini**, **OpenAI**, **Cursor**, **xAI**, and **DeepSeek**.
- **Standard vs. Fast Mode Pricing (`⚡ FAST: OFF / ON`):** Every model row displays both `STD (IN / OUT)` and `⚡ FAST (IN / OUT)` per-million token rates side-by-side. Toggle `⚡ FAST` (`Ctrl+M`) to switch the live task cost calculator and price sorting between Standard and Fast/Priority rates.
- **Published Benchmark Scores (`AAII SCORE`):** Includes each model's **Artificial Analysis Intelligence Index (v5)** composite score badge and supports sorting by score descending.
- **Task Tier Guidance & `BEST VALUE` Highlighting:** Models are categorized into `FAST / BUDGET`, `BALANCED`, and `REASONING`, and the most cost-effective model in the current view is automatically badged with `BEST VALUE`.
- **Interactive Task Cost Estimator:** Choose a workload preset (*Quick Prompt*, *Code Feature*, *Cached Agent Loop*, *Repo Audit*) or type custom `IN`, `CACHE`, and `OUT` token counts to see exact per-task dollar estimates for every model.
- **Offline-First + Live OpenRouter Sync:** Works immediately offline using the curated catalog, with a one-click **SYNC LIVE** button to pull updated per-token pricing from OpenRouter's public API.
- **Global Hotkey (`Ctrl+Shift+Space`):** Summon or hide the HUD from anywhere on your desktop.

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+Shift+Space` | Global hotkey to summon or hide the HUD overlay |
| `Alt+1` .. `Alt+7` | Switch provider tab (`All`, `Anthropic`, `Google Gemini`, `OpenAI`, `Cursor`, `xAI`, `DeepSeek`) |
| `Ctrl+M` | Toggle `⚡ FAST` mode pricing on/off |
| `Ctrl+F` | Focus the search bar |
| `Esc` | Clear active search query |

## Build, Run & Test

Requires the **.NET 10 SDK (`net10.0-windows`)**. Zero external NuGet dependencies required.

```powershell
# Build the entire solution
dotnet build AiPriceWatch.sln -c Release -m:1

# Launch the desktop overlay HUD
dotnet run --project src/AiPriceWatch.App

# Run the unit test suite
dotnet run --project tests/AiPriceWatch.Tests
```
