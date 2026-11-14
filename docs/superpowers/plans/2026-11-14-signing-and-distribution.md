# Windows Signing (SignPath Foundation) & In-App Auto-Update Plan

**Goal:** Give non-technical Windows users a 1-click installer (`AiPriceWatch-win-Setup.exe`) from a clean download page, sign it for free using **SignPath Foundation**, and enable **in-app self-updates** via **Velopack** so users never have to return to a browser or GitHub to update.

---

## What Is Already Set Up

- **MIT License:** `LICENSE` at repo root (required for SignPath Foundation open-source eligibility).
- **SemVer Single Source of Truth:** `Directory.Build.props` (`0.1.0`) + `docs/versioning.md`.
- **Automated Versioning & Enforcement:**
  - `.github/workflows/ci.yml`: enforces Conventional Commits on PRs and runs build + tests.
  - `.github/workflows/release-please.yml` + `release-please-config.json`: automatically bumps `Directory.Build.props`, updates `CHANGELOG.md`, and creates `vX.Y.Z` GitHub tags/releases.
- **Download Page:** `site/index.html` + `.github/workflows/pages.yml` (deploys to GitHub Pages).

---

## Phase 1: Add In-App Auto-Updates with Velopack

Velopack (`Velopack` NuGet package) packages the self-contained WPF app into `AiPriceWatch-win-Setup.exe` and provides an in-app `UpdateManager` that checks GitHub Releases and applies updates automatically on restart.

### Step 1.1: Install the Velopack NuGet Package
```powershell
dotnet add src/AiPriceWatch.App/AiPriceWatch.App.csproj package Velopack
```

### Step 1.2: Initialize Velopack on App Startup
In `src/AiPriceWatch.App/App.xaml.cs`, run `VelopackApp.Build().Run()` before WPF starts so Velopack can handle install/update/uninstall hooks cleanly:

```csharp
using System.Windows;
using Velopack;

namespace AiPriceWatch.App;

public partial class App : Application
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
```
*(Note: set `<StartupObject>AiPriceWatch.App.App</StartupObject>` in `AiPriceWatch.App.csproj` or remove `StartupUri` conflict if using an explicit `Main`.)*

### Step 1.3: Add an In-App Update Service
Create `src/AiPriceWatch.App/Services/AppUpdateService.cs`:

```csharp
using Velopack;
using Velopack.Sources;

namespace AiPriceWatch.App.Services;

public sealed class AppUpdateService
{
    private readonly UpdateManager _updateManager = new(
        new GithubSource("https://github.com/zprue743/ai-pricewatch", accessToken: null, prerelease: false));

    public bool IsInstalled => _updateManager.IsInstalled;

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (!_updateManager.IsInstalled)
            return null;

        return await _updateManager.CheckForUpdatesAsync();
    }

    public async Task DownloadAndRestartAsync(UpdateInfo update, Action<int>? onProgress = null)
    {
        await _updateManager.DownloadUpdatesAsync(update, onProgress);
        _updateManager.ApplyUpdatesAndRestart(update);
    }
}
```

### Step 1.4: Wire Up the HUD Button
In `MainWindow.xaml` / `MainWindow.xaml.cs`:
- On window loaded (non-blocking background task), call `CheckForUpdatesAsync()`.
- If an update is available, reveal a subtle **`UPDATE vX.Y.Z`** pill button in the HUD header.
- Clicking it calls `DownloadAndRestartAsync(update)` and restarts directly into the new version.

---

## Phase 2: Apply for Free Open-Source Signing (SignPath Foundation)

1. **Push `main` to GitHub** so `LICENSE` (MIT) and `README.md` are publicly visible at `https://github.com/zprue743/ai-pricewatch`.
2. **Enable GitHub Pages** in GitHub Repo **Settings → Pages → Source: GitHub Actions** so `site/index.html` is live.
3. **Apply at [signpath.org/open-source](https://signpath.org/open-source)**:
   - Provide your GitHub repo URL (`https://github.com/zprue743/ai-pricewatch`).
   - Wait for approval (typically ~1 week).
4. **Configure SignPath.io** once approved:
   - Link your GitHub repository as a Project (e.g., `ai-pricewatch`).
   - Create a **Signing Policy** (`release-signing`) restricted to `refs/tags/v*` on GitHub Actions.
   - Create an **Artifact Configuration** (`windows-installer`) covering `.exe` / `.dll` / `.nupkg` as needed.
5. **Add GitHub Repository Secrets** (**Settings → Secrets and variables → Actions**):
   - `SIGNPATH_ORG_ID`
   - `SIGNPATH_API_TOKEN`

---

## Phase 3: Automated Release & Signing Workflow

Create `.github/workflows/release.yml` triggered on `v*` tags. It builds self-contained `win-x64`, packs with Velopack (`vpk`), signs with SignPath (when `SIGNPATH_API_TOKEN` is present), and uploads assets to the GitHub Release:

```yaml
name: Release Windows Installer

on:
  push:
    tags:
      - 'v*'

permissions:
  contents: write

jobs:
  build-pack-sign:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Extract version from tag
        id: ver
        shell: pwsh
        run: |
          $tag = "${{ github.ref_name }}"
          $version = $tag.TrimStart('v')
          echo "version=$version" >> $env:GITHUB_OUTPUT

      - name: Install Velopack CLI (vpk)
        run: dotnet tool install -g vpk

      - name: Publish self-contained win-x64
        run: >
          dotnet publish src/AiPriceWatch.App/AiPriceWatch.App.csproj
          -c Release
          -r win-x64
          --self-contained true
          -p:Version=${{ steps.ver.outputs.version }}
          -o publish

      - name: Pack Velopack installer
        run: >
          vpk pack
          --packId AiPriceWatch
          --packVersion ${{ steps.ver.outputs.version }}
          --packDir publish
          --mainExe AiPriceWatch.App.exe
          --packTitle "AI PriceWatch"
          --outputDir releases

      - name: Sign installer with SignPath (when configured)
        if: env.SIGNPATH_API_TOKEN != ''
        env:
          SIGNPATH_API_TOKEN: ${{ secrets.SIGNPATH_API_TOKEN }}
        uses: SignPath/github-action-submit-signing-request@v1
        with:
          signpath-api-token: ${{ secrets.SIGNPATH_API_TOKEN }}
          organization-id: ${{ secrets.SIGNPATH_ORG_ID }}
          project-slug: ai-pricewatch
          signing-policy-slug: release-signing
          artifact-configuration-slug: windows-installer
          input-file: releases/AiPriceWatch-win-Setup.exe
          output-file: releases/AiPriceWatch-win-Setup.exe
          wait-for-completion: true

      - name: Upload release assets to GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: |
            releases/AiPriceWatch-win-Setup.exe
            releases/AiPriceWatch-*-full.nupkg
            releases/AiPriceWatch-*-delta.nupkg
            releases/releases.win.json
            releases/RELEASES
```

---

## Verification Checklist

1. `dotnet build AiPriceWatch.sln -c Release -m:1` succeeds.
2. `dotnet run --project tests/AiPriceWatch.Tests` passes.
3. GitHub Pages URL serves `site/index.html` and the download button points to `releases/latest/download/AiPriceWatch-win-Setup.exe`.
4. After installing `v0.1.0` via `AiPriceWatch-win-Setup.exe` and publishing `v0.1.1`, launching the app shows the in-app update prompt and updates without opening a browser.
