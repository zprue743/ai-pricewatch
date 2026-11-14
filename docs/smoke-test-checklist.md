# Windows Installation, Upgrade & UI Smoke-Test Checklist (PW-08, PW-14, PW-18)

Record environment details and verification results before finalizing `v0.2.0`.

## Environment Matrix

| Target OS | Channel | DPI Scale | Status | Notes |
|---|---|---|---|---|
| Windows 10 x64 (clean) | Velopack EXE (`v0.1.0` -> `v0.2.0`) | 100% / 125% | Pending | |
| Windows 11 x64 (clean) | Velopack EXE (fresh install) | 100% / 150% / 200% | Pending | |
| Windows 11 x64 | Store-compatible MSIX (sideload / Partner Center) | 100% / 125% | Pending | |

## Functional Checks

- [ ] **Fresh install (Velopack EXE):** Installs without separate .NET 10 runtime; launches HUD.
- [ ] **v0.1.0 -> v0.2.0 upgrade:** Upgrades cleanly via Velopack; preserves existing `HiddenModelIds` in `%LOCALAPPDATA%\AiPriceWatch\user-preferences.json`.
- [ ] **Global hotkey (`Ctrl+Shift+Space`):** Toggles HUD visibility when focused, unfocused, and hidden.
- [ ] **System tray lifecycle (PW-10):**
  - [ ] Clicking `✕` hides to system tray and shows the one-time balloon notification.
  - [ ] Tray menu `Show / Hide HUD`, `Settings & Help`, `Check for Updates`, and `Exit` work.
  - [ ] `Exit` terminates the process cleanly, removes tray icon, and releases `Ctrl+Shift+Space`.
- [ ] **Settings & First-Run Guide (PW-11, PW-12):**
  - [ ] First-run banner can be dismissed and reopened from Settings.
  - [ ] `Start with Windows` is disabled by default; persists and updates `HKCU\...\Run` on Velopack installs (or shows Store guidance on MSIX).
  - [ ] Opacity, pin, provider, tier, sort, Fast mode, and token counts persist across restart.
- [ ] **Pricing & Sync (PW-01–PW-05, PW-13):**
  - [ ] Hovering rows/prices shows provenance source & verification date.
  - [ ] Unsupported Fast/Priority rates show `N/A` and are excluded from `LOWEST EST. COST`.
  - [ ] Offline `SYNC LIVE` shows failed-sync status, keeps prior data, and enables `Retry Sync`.
  - [ ] Empty filter state shows `Reset Filters` button.
- [ ] **DPI & Keyboard Navigation (PW-14):**
  - [ ] No clipped/overlapped controls at 100%, 125%, 150%, 200% scaling.
  - [ ] Keyboard shortcuts (`Ctrl+F`, `Ctrl+M`, `Alt+1..7`, `Esc`, `Tab`) work cleanly.
- [ ] **Uninstall / Reinstall:**
  - [ ] Uninstalling leaves no orphaned processes or broken shortcuts; user preferences in `%LOCALAPPDATA%\AiPriceWatch\` are documented as retained for reinstall/upgrade.
