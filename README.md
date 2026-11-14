# AI PriceWatch

A lightweight, terminal-sized (`760×480`) transparent desktop HUD overlay for Windows that lets you select an AI provider, compare model token pricing (`$/1M` Input, Cached Input, and Output tokens), filter by task tier, and calculate exact workload costs at a glance.

## Features

- **Translucent Terminal HUD (`760×480` default, resizable):** Frameless dark overlay with an independent background opacity slider (`35%–100%`) that keeps text 100% crisp while letting you see your active windows behind it.
- **Provider Selector Tabs (`Alt+1..6`):** Instant switching across **All**, **OpenAI**, **Anthropic**, **Google Gemini**, **xAI**, and **DeepSeek**.
- **Task Tier Guidance & `BEST VALUE` Highlighting:** Models are categorized into `FAST / BUDGET`, `BALANCED`, and `REASONING`, and the most cost-effective model in the current view is automatically badged with `BEST VALUE`.
- **Interactive Task Cost Estimator:** Choose a workload preset (*Quick Prompt*, *Code Feature*, *Cached Agent Loop*, *Repo Audit*) or type custom `IN`, `CACHE`, and `OUT` token counts to see exact per-task dollar estimates for every model.
- **Offline-First + Live OpenRouter Sync:** Works immediately offline using the curated catalog, with a one-click **SYNC LIVE** button to pull updated per-token pricing from OpenRouter's public API.
- **Global Hotkey (`Ctrl+Shift+Space`):** Summon or hide the HUD from anywhere on your desktop.

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+Shift+Space` | Global hotkey to summon or hide the HUD overlay |
| `Alt+1` .. `Alt+6` | Switch provider tab (`All`, `OpenAI`, `Anthropic`, `Google Gemini`, `xAI`, `DeepSeek`) |
| `Ctrl+F` | Focus the search bar |
| `Esc` | Clear active search query |

## Build, Run & Test

Requires the **.NET 10 SDK (`net10.0-windows`)**. Zero external NuGet dependencies required.

```powershell
# Build the entire solution
dotnet build AiPriceWatch.sln -c Release

# Launch the desktop overlay HUD
dotnet run --project src/AiPriceWatch.App

# Run the unit test suite
dotnet run --project tests/AiPriceWatch.Tests
```
