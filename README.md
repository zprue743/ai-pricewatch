# AI PriceWatch

<img width="880" height="503" alt="image" src="https://github.com/user-attachments/assets/eff5ff80-b56a-4bd6-bfb8-d62243402d94" />

A lightweight, transparent desktop HUD overlay for Windows 10/11 that lets you compare AI model token pricing (`$/1M` in **Standard** and **Fast / Priority** modes), inspect **Artificial Analysis Intelligence Index (AAII)** benchmark scores with source attribution, filter by task tier, and calculate deterministic workload costs at a glance.

## Install (Windows 10 / 11 x64)

No separate `.NET` runtime installation is required.

### Option 1: GitHub Releases (Velopack Installer)
1. Download **[AiPriceWatch-win-Setup.exe](https://github.com/zprue743/ai-pricewatch/releases/latest/download/AiPriceWatch-win-Setup.exe)** from [GitHub Releases](https://github.com/zprue743/ai-pricewatch/releases) or the **[Download Page](https://zprue743.github.io/ai-pricewatch/)**.
2. Verify the download's integrity and build provenance (recommended, as the GitHub EXE is **unsigned**):
   ```powershell
   Get-FileHash .\AiPriceWatch-win-Setup.exe -Algorithm SHA256
   gh attestation verify .\AiPriceWatch-win-Setup.exe --repo zprue743/ai-pricewatch
   ```
3. Run `AiPriceWatch-win-Setup.exe` to install and launch.
4. **Updating:** Velopack installs check GitHub Releases for updates and surface an **`UPDATE`** button in the top bar (and in the tray menu).

### Option 2: WinGet (Pending Upstream Review)
```powershell
winget install zprue743.AiPriceWatch
```
- Uses the GitHub-hosted Velopack installer (**unsigned**) with in-app Velopack updates (submitted in `microsoft/winget-pkgs#480344`).

See [docs/security-verification.md](docs/security-verification.md) for full details on Microsoft Defender scanning, SHA-256 checksums, GitHub Artifact Attestations, and VirusTotal verification.

## Features & Pricing Methodology

- **Transparent Provenance & Freshness:** Every pricing field and benchmark score carries source metadata (`Direct`, `OpenRouter`, `Estimated`, or `Unknown`) and a verification date (visible on hover).
- **Strict `N/A` Fast/Priority Semantics:** Fast/Priority API rates are displayed **only** when verified from an official provider source. Unsupported Fast/Priority rates strictly display `N/A` (never manufactured via arbitrary multipliers) and are excluded from Fast-mode cost calculations and rankings.
- **`LOWEST EST. COST` Highlighting:** Highlights the lowest-cost visible model with verified pricing for the active mode and token assumptions. Models with `N/A` rates are excluded.
- **System Tray Resident Lifecycle:** Clicking `✕` hides the HUD to the Windows system tray (with a one-time notification). Use the tray menu for **Show / Hide HUD**, **Settings & Help**, **Check for Updates**, and **Exit** (which terminates the process and releases `Ctrl+Shift+Space`).
- **Persistent Preferences & Optional Startup:** Opacity, pin state, provider, tier, sort, Fast mode, workload tokens, hidden models, and `Start with Windows` (disabled by default) persist across restarts and `v0.1 -> v0.2` upgrades in `%LOCALAPPDATA%\AiPriceWatch\user-preferences.json`.
- **Offline-First + Honest Live Sync:** Works offline using the bundled catalog. Clicking **SYNC LIVE** fetches standard rates from OpenRouter (`https://openrouter.ai/api/v1/models`). If offline or an error occurs, prior valid data is preserved and a visible failed-sync status with **Retry Sync** is shown.

## Privacy & Network Behavior

AI PriceWatch runs locally with no telemetry, no user accounts, and no background cloud sync:
- **OpenRouter (`https://openrouter.ai/api/v1/models`):** Contacted **only** when you click **SYNC LIVE** (or **Retry Sync**).
- **GitHub Releases:** Contacted on startup / manual update check for Velopack EXE installs only (disabled on Microsoft Store MSIX installs).

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+Shift+Space` | Global hotkey to summon or hide the HUD overlay |
| `Alt+1` .. `Alt+6` | Switch provider tab (`All`, `Anthropic`, `Google Gemini`, `OpenAI`, `SpaceXSI`, `DeepSeek`) |
| `Ctrl+M` | Toggle `⚡ FAST` mode pricing on/off |
| `Ctrl+F` | Focus the search bar |
| `Esc` | Close Settings or clear active search query |

## Troubleshooting & Reporting Defects

- **Cannot find window after closing:** Click the AI PriceWatch icon in the Windows system tray or press `Ctrl+Shift+Space`.
- **Live sync fails:** Check your internet connection and click **Retry Sync** in the bottom status bar; the HUD continues working with your last valid or bundled catalog.
- **Corrupt preferences:** Delete `%LOCALAPPDATA%\AiPriceWatch\user-preferences.json` and restart; defaults are restored automatically.
- **Report a bug:** Open an issue at [github.com/zprue743/ai-pricewatch/issues](https://github.com/zprue743/ai-pricewatch/issues) with your OS version, install channel (Store MSIX / WinGet / Velopack EXE), and app version.

## Build, Run & Test (Developers)

Requires the **.NET 10 SDK (`net10.0-windows`)**.

```powershell
# Build the entire solution
dotnet build AiPriceWatch.sln -c Release -m:1

# Launch the desktop overlay HUD
dotnet run --project src/AiPriceWatch.App

# Run unit & catalog validation tests
dotnet run --project tests/AiPriceWatch.Tests -c Release
```

See [docs/smoke-test-checklist.md](docs/smoke-test-checklist.md) for the Windows install/upgrade QA checklist.
