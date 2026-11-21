# Changelog

## [0.4.0](https://github.com/zprue743/ai-pricewatch/compare/v0.3.0...v0.4.0) (2026-11-15)


### Features

* **app:** add price diff badges and OpenRouter Add Models overlay in HUD ([4014af9](https://github.com/zprue743/ai-pricewatch/commit/4014af92f779a2f777189d5042d41d6db33161b6))
* **core:** add CatalogDiffCalculator for price deltas and new OpenRouter IDs ([f0da09c](https://github.com/zprue743/ai-pricewatch/commit/f0da09c99bd2a6bcf71aafd6edfa30926d1b1783))
* **core:** add SyncSnapshotStore for atomic diff and custom model persistence ([15752e5](https://github.com/zprue743/ai-pricewatch/commit/15752e5a144ee1809558a5a8c19df3cef7bc4d59))
* **core:** extract OpenRouterModelCandidate metadata during live sync ([46607d0](https://github.com/zprue743/ai-pricewatch/commit/46607d07f33c8c4f834c29481c423b6a618c42e0))
* **core:** integrate price diffs and OpenRouter model add/remove into HudViewModel ([1d390f5](https://github.com/zprue743/ai-pricewatch/commit/1d390f552f9f7adcd1461d8579f01c8f695a0011))
* price change diffs and OpenRouter model discovery (+ ADD) ([31013c3](https://github.com/zprue743/ai-pricewatch/commit/31013c3a21a06f3d48cd26d0c350db463dc2b636))
* **settings:** display current app version in settings menu ([4514543](https://github.com/zprue743/ai-pricewatch/commit/4514543ac0696656844c4886d2ea8fee5fd62839))
* **settings:** display current app version in settings menu ([9b787a7](https://github.com/zprue743/ai-pricewatch/commit/9b787a7964fc96bd28ba8939236e701a2ccc9eb0))

## [0.3.0](https://github.com/zprue743/ai-pricewatch/compare/v0.2.0...v0.3.0) (2026-11-15)


### Features

* add CursorBench 4.0 scores and live sync ([e8e6a5a](https://github.com/zprue743/ai-pricewatch/commit/e8e6a5a5ca983a5a78055675df0d09346f25748b))
* add CursorBench 4.0 scores and live sync ([e97e1bb](https://github.com/zprue743/ai-pricewatch/commit/e97e1bbbbc2821337d538230432b1eed1f8fa4b6))

## [0.2.0](https://github.com/zprue743/ai-pricewatch/compare/v0.1.0...v0.2.0) (2026-11-14)


### Features

* **catalog:** audit embedded catalog and add CatalogValidator (PW-04, [#4](https://github.com/zprue743/ai-pricewatch/issues/4)) ([a3b49a5](https://github.com/zprue743/ai-pricewatch/commit/a3b49a578ff2271b5e8defeed1a36d73dc6391ee))
* **desktop:** add system tray lifecycle, settings/onboarding, retry/empty states, and a11y (PW-10, PW-12, PW-13, PW-14, [#10](https://github.com/zprue743/ai-pricewatch/issues/10), [#12](https://github.com/zprue743/ai-pricewatch/issues/12), [#13](https://github.com/zprue743/ai-pricewatch/issues/13), [#14](https://github.com/zprue743/ai-pricewatch/issues/14)) ([1ec7293](https://github.com/zprue743/ai-pricewatch/commit/1ec7293abec9cf41bc329dd4870acdd97ef31381))
* implement AI PriceWatch v0.2.0 release plan (PW-01..PW-20) ([9dd400c](https://github.com/zprue743/ai-pricewatch/commit/9dd400c0fed803b98a1511abcde58602876500b9))
* **prefs:** persist full user preferences with v0.1 migration (PW-11, [#11](https://github.com/zprue743/ai-pricewatch/issues/11)) ([29daeba](https://github.com/zprue743/ai-pricewatch/commit/29daeba40dcc090e77150c26bd6f15518df8235a))
* **pricing:** add field-level pricing and benchmark provenance metadata (PW-01, [#1](https://github.com/zprue743/ai-pricewatch/issues/1)) ([237d945](https://github.com/zprue743/ai-pricewatch/commit/237d945de7065b8151d249af3db4cf1fe023b9a9))
* **pricing:** replace BEST VALUE with LOWEST EST. COST and test boundaries (PW-05, [#5](https://github.com/zprue743/ai-pricewatch/issues/5)) ([3514147](https://github.com/zprue743/ai-pricewatch/commit/3514147a9915b5404fefb361e1d869489fee07fd))
* **sync:** harden OpenRouter live sync and failure handling (PW-03, [#3](https://github.com/zprue743/ai-pricewatch/issues/3)) ([772db0c](https://github.com/zprue743/ai-pricewatch/commit/772db0cda0d5e86fb3961eadc6c12a7d43fbbaf5))


### Bug Fixes

* **pricing:** enforce strict N/A for unavailable Fast/Priority pricing (PW-02, [#2](https://github.com/zprue743/ai-pricewatch/issues/2)) ([ddebf81](https://github.com/zprue743/ai-pricewatch/commit/ddebf811f3c5b5599616e3fbf65c8c2796f44689))
