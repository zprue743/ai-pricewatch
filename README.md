# AI PriceWatch

<img width="880" height="503" alt="image" src="https://github.com/user-attachments/assets/eff5ff80-b56a-4bd6-bfb8-d62243402d94" />


A lightweight, transparent desktop HUD overlay for Windows that lets you select an AI provider, compare model token pricing (`$/1M` in both **Standard** and **Fast / Priority** modes), inspect published **Artificial Analysis Intelligence Index (AAII)** benchmark scores, filter by task tier, and calculate exact workload costs at a glance.

## Install (Windows 10 / 11)

No Git, GitHub navigation, or separate `.NET` runtime installation is required.

1. Open the **[AI PriceWatch Download Page](https://zprue743.github.io/ai-pricewatch/)** (or download **[AiPriceWatch-win-Setup.exe](https://github.com/zprue743/ai-pricewatch/releases/latest/download/AiPriceWatch-win-Setup.exe)** directly).
2. Double-click **`AiPriceWatch-win-Setup.exe`** to install and launch the app (creates Start Menu & Desktop shortcuts automatically).
   - *If Windows SmartScreen shows **"Windows protected your PC"**, click **More info** → **Run anyway**.*
3. Press **`Ctrl+Shift+Space`** anytime to show or hide the HUD.
4. **Updating:** When a new version is released, an **`UPDATE`** button appears in the top bar—click it to update and restart directly from inside the app.

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

## Build, Run & Test (Developers)

Requires the **.NET 10 SDK (`net10.0-windows`)**. Uses [Velopack](https://velopack.io/) for Windows installer packaging and in-app updates.

```powershell
# Build the entire solution
dotnet build AiPriceWatch.sln -c Release -m:1

# Launch the desktop overlay HUD
dotnet run --project src/AiPriceWatch.App

# Run the unit test suite
dotnet run --project tests/AiPriceWatch.Tests
```
