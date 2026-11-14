# Release Security, Signing & Provenance Verification

This document describes how AI PriceWatch releases are built, scanned, verified, and distributed across channels.

## 1. Distribution Channels & Signing Status

| Channel | Format | Code Signing | Update Mechanism |
|---|---|---|---|
| **Microsoft Store** | MSIX (`AiPriceWatch-<ver>-win-x64.msix`) | Signed by Microsoft during Store certification | Microsoft Store automatic updates (Velopack disabled) |
| **WinGet** | Velopack EXE (`AiPriceWatch-win-Setup.exe`) | **Unsigned** | Velopack in-app updates via GitHub Releases |
| **GitHub Releases** | Velopack EXE + MSIX + nupkgs | EXE is **unsigned**; MSIX is for Store upload | Velopack in-app updates for EXE installs |

> **Important:** Antivirus scans, SHA-256 checksums, and GitHub build attestations verify file integrity and build provenance. They do **not** substitute for Authenticode code signing or eliminate Windows SmartScreen warnings on unsigned EXE installers.

## 2. Mandatory CI Release Gates (PW-07, PW-20)

Every `v*` tag release in `.github/workflows/release.yml` must pass:

1. **Automated Tests & Catalog Validation:** `dotnet build AiPriceWatch.sln -c Release` and `dotnet run --project tests/AiPriceWatch.Tests -c Release` (including `CatalogValidator`).
2. **Version Consistency:** `Directory.Build.props` `<Version>` must match the Git tag (`vX.Y.Z`) and MSIX `Version` (`X.Y.Z.0`).
3. **Microsoft Defender Malware Scan:**
   - Verifies Defender (`AntivirusEnabled` and `AMServiceEnabled`) is active on the runner.
   - Updates signatures (`Update-MpSignature`).
   - Scans both extracted binaries (`publish/`) and final distributables (`releases/`).
   - Blocks publication if any threat is detected or if Defender is unavailable.
   - Publishes `SECURITY-REPORT.md` with UTC timestamp, engine version, and signature version.
4. **SHA-256 Checksums:** Generates and verifies `SHA256SUMS.txt` for all release artifacts.
5. **GitHub Artifact Attestations:** Generates cryptographic build provenance via `actions/attest-build-provenance@v2` for `AiPriceWatch-win-Setup.exe`, `AiPriceWatch-*-full.nupkg`, and `AiPriceWatch-*-win-x64.msix`.

## 3. Verifying a Downloaded Release

### Verify SHA-256 Checksum (PowerShell)

```powershell
Get-FileHash .\AiPriceWatch-win-Setup.exe -Algorithm SHA256
# Compare output against SHA256SUMS.txt from the GitHub Release
```

### Verify GitHub Build Provenance (GitHub CLI)

```powershell
gh attestation verify .\AiPriceWatch-win-Setup.exe --repo zprue743/ai-pricewatch
```

### Supplemental VirusTotal Verification

After a release is published, maintainers or users may submit `AiPriceWatch-win-Setup.exe` or `AiPriceWatch-<ver>-win-x64.msix` to VirusTotal and link the public report URL in the release notes. Never upload private build secrets or pre-release internal artifacts.

## 4. Microsoft Store MSIX Submission Procedure (PW-06)

1. Register/verify a Microsoft Partner Center developer account and reserve the app name **AI PriceWatch**.
2. Set repository variables `MSIX_IDENTITY_NAME`, `MSIX_PUBLISHER`, and `MSIX_PUBLISHER_DISPLAY_NAME` to match the Store-assigned values in Partner Center.
3. Download `AiPriceWatch-<ver>-win-x64.msix` from the GitHub Release (or `Build MSIX Package` workflow artifact).
4. Upload the `.msix` package in Partner Center; Microsoft signs it during certification.

## 5. Rollback & Recovery Procedure (PW-09)

If a broken or bad release ships:
1. Mark the faulty GitHub Release as **Pre-release** or delete the bad release tag if it has not propagated, and restore the prior good release as `latest` so `releases/latest/download/AiPriceWatch-win-Setup.exe` points to the known-good installer.
2. Publish a patch release (`vX.Y.Z+1`) reverting or fixing the regression so Velopack users on the broken version can auto-update forward.
3. For Microsoft Store MSIX, use Partner Center's package flight/rollback controls to halt rollout and submit the patched `.msix`.
