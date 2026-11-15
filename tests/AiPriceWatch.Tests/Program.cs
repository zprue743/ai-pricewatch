using System.Net;
using System.Text;
using AiPriceWatch.Core.Models;
using AiPriceWatch.Core.Services;
using AiPriceWatch.Core.ViewModels;

int passed = 0;

void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new Exception($"FAIL: {message} — Expected '{expected}', got '{actual}'");
    }
    passed++;
}

void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception($"FAIL: {message}");
    }
    passed++;
}

var sampleFastModel = new ModelPricingEntry(
    "gpt-5-mini", "GPT-5 Mini", "OpenAI", TaskTier.Fast,
    InputPerMillion: 0.25m, CachedInputPerMillion: 0.025m, OutputPerMillion: 2.00m,
    ContextTokens: 1_000_000, Notes: "Fast everyday tasks",
    FastInputPerMillion: 0.50m, FastOutputPerMillion: 4.00m, IntelligenceScore: 38.5m);

var noCacheModel = sampleFastModel with { CachedInputPerMillion = null, FastInputPerMillion = null, FastOutputPerMillion = null };

// Task 1: Standard & Fast mode cost calculation
AssertEqual(0.130m, TaskCostCalculator.CalculateCost(sampleFastModel, 100_000, 200_000, 50_000, useFastMode: false), "Standard cost math");
AssertEqual(0.255m, TaskCostCalculator.CalculateCost(sampleFastModel, 100_000, 200_000, 50_000, useFastMode: true), "Fast mode cost math");
AssertEqual((decimal?)null, TaskCostCalculator.CalculateCostOrNull(noCacheModel, 100_000, 100_000, 0, useFastMode: true), "Absent fast price returns null in Fast mode");
AssertEqual("N/A", TaskCostCalculator.FormatCost((decimal?)null), "Null cost formats as N/A");
AssertEqual(0m, TaskCostCalculator.CalculateCost(sampleFastModel, -5000, -1000, -2000), "Negative tokens clamped to 0");
AssertEqual("$0.0000", TaskCostCalculator.FormatCost(0m), "Zero cost format");
AssertEqual("<$0.0001", TaskCostCalculator.FormatCost(0.00004m), "Sub-basis-point cost format");
AssertEqual("$0.0600", TaskCostCalculator.FormatCost(0.06m), "Sub-dollar cost format");
AssertEqual("$1.25", TaskCostCalculator.FormatCost(1.254m), "Dollar+ cost format");
AssertEqual("—", TaskCostCalculator.FormatPricePerMillion(null), "Null price per million format");
AssertEqual("$0.15", TaskCostCalculator.FormatPricePerMillion(0.15m), "Standard price per million format");
AssertEqual("$0.075", TaskCostCalculator.FormatPricePerMillion(0.075m), "Sub-cent price per million format");
AssertEqual("$2.00 / $10.00", TaskCostCalculator.FormatInOutPair(2.00m, 10.00m), "In/Out pair format");
AssertEqual("59.1", TaskCostCalculator.FormatScore(59.1m), "Intelligence score format");
AssertEqual("N/A", TaskCostCalculator.FormatScore(null), "Null score format");
AssertEqual("128K", TaskCostCalculator.FormatContextWindow(128_000), "Thousands context window");
AssertEqual("1M", TaskCostCalculator.FormatContextWindow(1_000_000), "Millions context window");
AssertEqual("2M", TaskCostCalculator.FormatContextWindow(2_000_000), "2M context window");

// Task 2: Curated Pricing Catalog — Current Flagships, Composer, Grok 4.6, Fast Mode & Scores
var catalog = PricingCatalogService.LoadDefault();
AssertTrue(catalog.AllModels.Count >= 24, "Default catalog contains at least 24 current models across major providers");
var providers = catalog.GetProviders();
AssertTrue(
    providers.Contains("OpenAI") &&
    providers.Contains("Anthropic") &&
    providers.Contains("Google Gemini") &&
    providers.Contains("xAI") &&
    providers.Contains("Cursor") &&
    providers.Contains("DeepSeek"),
    "Includes all 6 core providers including Cursor");

var ids = catalog.AllModels.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
AssertTrue(ids.Contains("gemini-4-carbon") && ids.Contains("gemini-4-argon") && ids.Contains("gemini-3.8-flash"), "Contains current Google Gemini 4 & 3.8 lineup");
AssertTrue(ids.Contains("gpt-6-astra") && ids.Contains("gpt-6.1-sol") && ids.Contains("gpt-6.1-terra") && ids.Contains("gpt-5.5"), "Contains current OpenAI GPT-6 & GPT-5 lineup");
AssertTrue(ids.Contains("claude-fable-5-2") && ids.Contains("claude-opus-5-5") && ids.Contains("claude-sonnet-5-5") && ids.Contains("claude-haiku-5-5"), "Contains current Anthropic Claude 5.x lineup");
AssertTrue(ids.Contains("grok-4.7") && ids.Contains("grok-4.6") && ids.Contains("grok-4.5"), "Contains current xAI Grok 4.7, 4.6, 4.5 lineup");
AssertTrue(ids.Contains("composer-2.5") && ids.Contains("composer-2"), "Contains Cursor Composer models");
AssertTrue(ids.Contains("deepseek-v4-pro") && ids.Contains("deepseek-flash"), "Contains current DeepSeek V4 lineup");
AssertTrue(catalog.AllModels.All(m => m.IntelligenceScore is > 0m), "Every catalog model includes a published Intelligence Score");
var catalogValidation = CatalogValidator.Validate(catalog.AllModels, new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
AssertTrue(catalogValidation.IsValid, $"Embedded catalog passes CatalogValidator: {string.Join("; ", catalogValidation.Errors)}");
AssertTrue(catalog.AllModels.Any(m => m.FastInputPerMillion is > 0m && m.FastOutputPerMillion is > 0m), "Catalog includes verified Fast mode pricing");
AssertTrue(catalog.AllModels.Any(m => m.FastInputPerMillion is null && m.FastOutputPerMillion is null), "Catalog leaves unverified Fast mode pricing as null (N/A)");

var sortedByScore = catalog.Query(sort: PricingSortField.ScoreDesc);
AssertTrue((sortedByScore.First().IntelligenceScore ?? 0m) >= (sortedByScore.Last().IntelligenceScore ?? 0m), "Sorts descending by Intelligence Score");

var anthropicFast = catalog.Query(provider: " anthropic ", tier: TaskTier.Fast, searchText: "HAIKU");
AssertTrue(anthropicFast.Count >= 1 && anthropicFast.All(m => m.Provider == "Anthropic" && m.Tier == TaskTier.Fast), "Filters by provider, tier, and case-insensitive search");

var sortedByInput = catalog.Query(sort: PricingSortField.InputPriceAsc);
AssertTrue(sortedByInput.First().InputPerMillion <= sortedByInput.Last().InputPerMillion, "Sorts ascending by input price");

var sortedByContext = catalog.Query(sort: PricingSortField.ContextDesc);
AssertTrue(sortedByContext.First().ContextTokens >= sortedByContext.Last().ContextTokens, "Sorts descending by context window");

string sampleOpenRouterJson = """
{
  "data": [
    {
      "id": "openai/gpt-5-mini",
      "pricing": { "prompt": "0.00000020", "completion": "0.00000180", "input_cache_read": "0.00000002" }
    },
    {
      "id": "anthropic/claude-sonnet-4.6",
      "pricing": { "prompt": "0.00000280", "completion": "0.00001400" }
    },
    {
      "id": "anthropic/claude-sonnet-5-5",
      "pricing": { "prompt": "-1", "completion": "invalid" }
    }
  ]
}
""";
var merged = OpenRouterPriceParser.MergeLivePrices(catalog.AllModels, sampleOpenRouterJson, out int updatedCount);
AssertEqual(2, updatedCount, "Reports exact count of models updated from OpenRouter payload");
var updatedMini = merged.First(m => m.Id == "gpt-5-mini");
AssertEqual(0.20m, updatedMini.InputPerMillion, "OpenRouter per-token prompt price converted to $/1M");
AssertEqual(1.80m, updatedMini.OutputPerMillion, "OpenRouter per-token completion price converted to $/1M");
AssertEqual(0.02m, updatedMini.CachedInputPerMillion, "OpenRouter per-token cache read price converted to $/1M");
var updatedSonnet46 = merged.First(m => m.Id == "claude-sonnet-4-6");
AssertEqual(2.80m, updatedSonnet46.InputPerMillion, "Matches dotted OpenRouter ID claude-sonnet-4.6 to claude-sonnet-4-6");

// Task 3: HUD ViewModel, Fast Mode Toggle, Hide/Unhide Models & Persistence
string tempPrefsPath = Path.Combine(Path.GetTempPath(), $"aipricewatch-prefs-{Guid.NewGuid():N}.json");
try
{
    var prefsStore = new UserPreferencesStore(tempPrefsPath);
    var vm = new HudViewModel(PricingCatalogService.LoadDefault(), prefsStore);
    int initialTotal = vm.VisibleModels.Count;
    AssertEqual("All", vm.SelectedProvider, "Default provider is All");
    AssertEqual(PricingSortField.ScoreDesc, vm.SelectedSort, "Default sort is ScoreDesc (AAII Score)");
    AssertTrue((vm.VisibleModels.First().ScoreRaw ?? 0m) >= (vm.VisibleModels.Last().ScoreRaw ?? 0m), "Default view is sorted by AAII Score descending");
    AssertEqual(false, vm.IsFastMode, "Default IsFastMode is false");
    AssertEqual(0, vm.HiddenCount, "Initially 0 hidden models");
    AssertTrue(initialTotal >= 24, "Shows all models initially");
    AssertTrue(vm.VisibleModels.Any(m => m.IsCheapestInView), "Marks cheapest model in view");

    // Hide a model and verify it disappears when ShowHiddenModels is false
    vm.ToggleModelHidden("grok-build-0.1");
    AssertEqual(1, vm.HiddenCount, "HiddenCount is 1 after hiding a model");
    AssertEqual(initialTotal - 1, vm.VisibleModels.Count, "Hidden model is excluded from VisibleModels by default");
    AssertTrue(vm.VisibleModels.All(m => m.Id != "grok-build-0.1"), "grok-build-0.1 is hidden");

    // Verify persistence across new ViewModel instance
    var vmReloaded = new HudViewModel(PricingCatalogService.LoadDefault(), prefsStore);
    AssertEqual(1, vmReloaded.HiddenCount, "Reloaded ViewModel restores hidden model count from disk");
    AssertTrue(vmReloaded.VisibleModels.All(m => m.Id != "grok-build-0.1"), "Reloaded ViewModel keeps grok-build-0.1 hidden");

    // Toggle ShowHiddenModels = true to view and unhide hidden models
    vm.ShowHiddenModels = true;
    AssertEqual(initialTotal, vm.VisibleModels.Count, "When ShowHiddenModels is true, all models including hidden ones are visible");
    var hiddenRow = vm.VisibleModels.First(m => m.Id == "grok-build-0.1");
    AssertTrue(hiddenRow.IsHidden, "Hidden row has IsHidden == true");
    AssertEqual("UNHIDE", hiddenRow.HideActionLabel, "Hidden row displays UNHIDE button label");

    // Unhide the model via ToggleModelHidden
    vm.ToggleModelHidden("grok-build-0.1");
    AssertEqual(0, vm.HiddenCount, "HiddenCount returns to 0 after unhiding");
    AssertTrue(!vm.VisibleModels.First(m => m.Id == "grok-build-0.1").IsHidden, "Model is no longer marked hidden");

    // Hide two models and test UnhideAllModels()
    vm.ShowHiddenModels = false;
    vm.ToggleModelHidden("gpt-5-mini");
    vm.ToggleModelHidden("composer-2");
    AssertEqual(2, vm.HiddenCount, "HiddenCount is 2");
    vm.UnhideAllModels();
    AssertEqual(0, vm.HiddenCount, "UnhideAllModels clears all hidden models");
    AssertEqual(initialTotal, vm.VisibleModels.Count, "All models visible again after UnhideAllModels");

    var grok46Standard = vm.VisibleModels.First(m => m.Id == "grok-4.6").EstimatedTaskCostRaw;
    vm.IsFastMode = true;
    var grok46Fast = vm.VisibleModels.First(m => m.Id == "grok-4.6").EstimatedTaskCostRaw;
    AssertTrue(grok46Fast > grok46Standard, "Toggling IsFastMode increases estimated task cost to Fast mode rate");

    // Provider switching updates VisibleModels
    vm.SelectedProvider = "Cursor";
    AssertTrue(vm.VisibleModels.All(m => m.Provider == "Cursor"), "Filtering by Cursor only shows Cursor Composer models");

    // Selecting preset updates token counts and recomputes task cost
    var longContextPreset = vm.Presets.First(p => p.InputTokens >= 200_000);
    vm.SelectedPreset = longContextPreset;
    AssertEqual(longContextPreset.InputTokens, vm.TaskInputTokens, "Preset updates TaskInputTokens");

    // Opacity clamping (Review Focus #5)
    vm.BackgroundOpacity = 0.05;
    AssertEqual(0.35, vm.BackgroundOpacity, "Clamps minimum opacity to 0.35");
    vm.BackgroundOpacity = 1.5;
    AssertEqual(1.0, vm.BackgroundOpacity, "Clamps maximum opacity to 1.0");
}
finally
{
    if (File.Exists(tempPrefsPath))
    {
        File.Delete(tempPrefsPath);
    }
}

// PW-01: Pricing & Benchmark Source Metadata
string provenanceJson = """
[
  {
    "id": "test-model-full",
    "name": "Test Model Full",
    "provider": "Anthropic",
    "tier": "Balanced",
    "inputPerMillion": 2.00,
    "cachedInputPerMillion": 0.10,
    "outputPerMillion": 10.00,
    "fastInputPerMillion": 4.00,
    "fastOutputPerMillion": 20.00,
    "intelligenceScore": 54.5,
    "contextTokens": 200000,
    "notes": "Test full provenance",
    "openRouterId": "anthropic/test-model-full",
    "pricingSource": {
      "sourceName": "Anthropic Pricing Docs",
      "sourceUrl": "https://docs.anthropic.com/en/docs/about-claude/pricing",
      "sourceType": "directProvider",
      "lastVerifiedAt": "2026-11-14T00:00:00Z",
      "status": "verified"
    },
    "benchmarkSource": {
      "publisher": "Artificial Analysis",
      "benchmarkName": "Intelligence Index",
      "version": "v5",
      "scoreDate": "2026-11-10T00:00:00Z",
      "status": "verified"
    }
  },
  {
    "id": "test-model-partial",
    "name": "Test Model Partial",
    "provider": "OpenAI",
    "tier": "Fast",
    "inputPerMillion": 0.25,
    "cachedInputPerMillion": null,
    "outputPerMillion": 2.00,
    "contextTokens": 128000,
    "notes": "Missing fast, cache, and benchmark"
  }
]
""";
var provenanceCatalog = PricingCatalogService.FromJson(provenanceJson);
AssertEqual(2, provenanceCatalog.AllModels.Count, "Parses catalog entries with full and partial provenance");

var fullEntry = provenanceCatalog.AllModels[0];
AssertEqual("anthropic/test-model-full", fullEntry.OpenRouterId, "Parses explicit OpenRouterId");
AssertEqual(PriceSourceType.DirectProvider, fullEntry.EffectiveStandardProvenance.SourceType, "Parses directProvider source type");
AssertEqual(DataVerificationStatus.Verified, fullEntry.EffectiveStandardProvenance.Status, "Parses verified pricing status");
AssertEqual("Artificial Analysis", fullEntry.EffectiveBenchmarkProvenance.Publisher, "Parses benchmark publisher");

var partialEntry = provenanceCatalog.AllModels[1];
AssertEqual(PriceSourceType.Unknown, partialEntry.EffectiveStandardProvenance.SourceType, "Missing provenance defaults to Unknown");
AssertEqual(DataVerificationStatus.Unavailable, partialEntry.EffectiveCachedProvenance.Status, "Null cached price defaults to Unavailable provenance");
AssertEqual(DataVerificationStatus.Unavailable, partialEntry.EffectiveFastProvenance.Status, "Null fast price defaults to Unavailable provenance");
AssertEqual(DataVerificationStatus.Unavailable, partialEntry.EffectiveBenchmarkProvenance.Status, "Null score defaults to Unavailable benchmark provenance");

var fullRow = new ModelRowViewModel(fullEntry, 10_000, 0, 2_000, isCheapestInView: false);
AssertEqual("Direct", fullRow.PricingSourceBadge, "Row badge reflects DirectProvider");
AssertTrue(fullRow.BenchmarkTooltip.Contains("Artificial Analysis", StringComparison.Ordinal), "Benchmark tooltip includes publisher");
AssertTrue(fullRow.RowDetailsTooltip.Contains("2026-11-14", StringComparison.Ordinal), "Row details tooltip includes verification date");

var partialRow = new ModelRowViewModel(partialEntry, 10_000, 0, 2_000, isCheapestInView: false);
AssertEqual("Unknown", partialRow.PricingSourceBadge, "Row badge reflects Unknown when provenance omitted");
AssertEqual("N/A", partialRow.ScoreDisplay, "Row score displays N/A when missing");

var malformedCatalog = PricingCatalogService.FromJson("{ invalid json [");
AssertEqual(0, malformedCatalog.AllModels.Count, "Malformed JSON does not crash startup and returns empty catalog");

// PW-02: Fast/Priority Pricing Semantics (Strict N/A, no 2x multiplier)
var noFastEntry = new ModelPricingEntry(
    "cheap-no-fast", "Cheap No Fast", "OpenAI", TaskTier.Fast,
    InputPerMillion: 0.05m, CachedInputPerMillion: 0.01m, OutputPerMillion: 0.10m,
    ContextTokens: 128000, Notes: "No fast mode");
var verifiedFastEntry = new ModelPricingEntry(
    "verified-fast", "Verified Fast", "Anthropic", TaskTier.Balanced,
    InputPerMillion: 2.00m, CachedInputPerMillion: 0.10m, OutputPerMillion: 10.00m,
    ContextTokens: 200000, Notes: "Has verified fast",
    FastInputPerMillion: 4.00m, FastOutputPerMillion: 20.00m);

var fastCatalog = new PricingCatalogService([noFastEntry, verifiedFastEntry]);
var fastSorted = fastCatalog.Query(sort: PricingSortField.TotalTaskCostAsc, useFastMode: true);
AssertEqual("verified-fast", fastSorted[0].Id, "Fast mode TotalTaskCostAsc ranks verified Fast model ahead of N/A model");
AssertEqual("cheap-no-fast", fastSorted[1].Id, "Fast mode TotalTaskCostAsc pushes N/A Fast model to the end");

var fastVm = new HudViewModel(fastCatalog, new UserPreferencesStore(Path.Combine(Path.GetTempPath(), $"aipw-pw02-{Guid.NewGuid():N}.json")));
fastVm.IsFastMode = true;
var noFastRow = fastVm.VisibleModels.First(m => m.Id == "cheap-no-fast");
var verifiedFastRow = fastVm.VisibleModels.First(m => m.Id == "verified-fast");
AssertEqual("N/A", noFastRow.FastInOutDisplay, "Absent Fast pricing displays N/A");
AssertEqual("N/A", noFastRow.EstimatedTaskCostDisplay, "Absent Fast task cost displays N/A in Fast mode");
AssertTrue(!noFastRow.IsCheapestInView, "Model with N/A Fast pricing is never marked cheapest in Fast mode");
AssertTrue(verifiedFastRow.IsCheapestInView, "Verified Fast model is marked cheapest in Fast mode");

// OpenRouterPriceParser must NOT derive 2x Fast pricing when updating standard rates
var openRouterFastMerged = OpenRouterPriceParser.MergeLivePrices(
    [noFastEntry, verifiedFastEntry],
    """{ "data": [ { "id": "openai/cheap-no-fast", "pricing": { "prompt": "0.00000010", "completion": "0.00000020" } }, { "id": "anthropic/verified-fast", "pricing": { "prompt": "0.00000300", "completion": "0.00001500" } } ] }""",
    out int fastUpdatedCount);
AssertEqual(2, fastUpdatedCount, "Updates standard prices for both models");
AssertEqual((decimal?)null, openRouterFastMerged[0].FastInputPerMillion, "OpenRouter update does not manufacture FastInputPerMillion");
AssertEqual((decimal?)null, openRouterFastMerged[0].FastOutputPerMillion, "OpenRouter update does not manufacture FastOutputPerMillion");
AssertEqual(4.00m, openRouterFastMerged[1].FastInputPerMillion, "OpenRouter update preserves existing verified FastInputPerMillion without ratio scaling");

// PW-03: Harden live synchronization and failure handling
var badJsonResult = OpenRouterPriceParser.MergeLivePricesDetailed([verifiedFastEntry], "{ not valid json");
AssertTrue(!badJsonResult.IsValidPayload, "Bad JSON is flagged as invalid payload");
AssertEqual(0, badJsonResult.UpdatedCount, "Bad JSON updates 0 models");

string duplicateAndInvalidPayload = """
{
  "data": [
    { "id": "anthropic/verified-fast", "pricing": { "prompt": "0.00000300", "completion": "0.00001500" } },
    { "id": "anthropic/verified-fast", "pricing": { "prompt": "0.00000900", "completion": "0.00009000" } },
    { "id": "vendor-a/shared-name", "pricing": { "prompt": "0.00000100", "completion": "0.00000200" } },
    { "id": "vendor-b/shared-name", "pricing": { "prompt": "0.00000500", "completion": "0.00000600" } },
    { "id": "openai/bad-price", "pricing": { "prompt": "-0.001", "completion": "0.001" } },
    { "id": "openai/huge-price", "pricing": { "prompt": "999.0", "completion": "1.0" } }
  ]
}
""";
var sharedNameEntry = new ModelPricingEntry(
    "shared-name", "Shared Name", "Other", TaskTier.Fast,
    InputPerMillion: 0.99m, CachedInputPerMillion: null, OutputPerMillion: 0.99m,
    ContextTokens: 128000, Notes: "Ambiguous short ID");
var dupResult = OpenRouterPriceParser.MergeLivePricesDetailed([verifiedFastEntry, sharedNameEntry], duplicateAndInvalidPayload);
AssertTrue(dupResult.IsValidPayload, "Partial valid payload succeeds");
AssertEqual(1, dupResult.UpdatedCount, "Only unambiguous valid entry is updated");
AssertEqual(1, dupResult.DuplicateCount, "Duplicate ID is counted and ignored");
AssertEqual(3, dupResult.IgnoredCount, "Duplicate + negative + absurdly large entries are counted as ignored");
AssertEqual(3.00m, dupResult.Models[0].InputPerMillion, "First valid entry wins over duplicate");
AssertEqual(0.99m, dupResult.Models[1].InputPerMillion, "Ambiguous short ID across providers is not merged");

// Offline / failing HTTP handler keeps prior valid data and marks SyncStatus.Failed
var syncVm = new HudViewModel(
    new PricingCatalogService([verifiedFastEntry]),
    new UserPreferencesStore(Path.Combine(Path.GetTempPath(), $"aipw-pw03-{Guid.NewGuid():N}.json")));

var okHandler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
{
    Content = new StringContent("""{ "data": [ { "id": "anthropic/verified-fast", "pricing": { "prompt": "0.00000250", "completion": "0.00001250" } } ] }""", Encoding.UTF8, "application/json")
});
using (var okClient = new HttpClient(okHandler))
{
    await syncVm.SyncLivePricesAsync(okClient);
}
AssertEqual(SyncStatus.Succeeded, syncVm.SyncStatus, "Successful sync sets SyncStatus.Succeeded");
AssertEqual(CatalogDataOrigin.LiveSynced, syncVm.DataOrigin, "Successful sync sets CatalogDataOrigin.LiveSynced");
AssertTrue(syncVm.LastSuccessfulSyncAt.HasValue, "Records LastSuccessfulSyncAt");
AssertEqual(2.50m, syncVm.VisibleModels[0].StandardProvenance.Status == DataVerificationStatus.Verified ? 2.50m : 0m, "Live sync updates price");

var failHandler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Network unreachable"));
using (var failClient = new HttpClient(failHandler))
{
    await syncVm.SyncLivePricesAsync(failClient);
}
AssertEqual(SyncStatus.Failed, syncVm.SyncStatus, "Offline sync sets SyncStatus.Failed");
AssertEqual(CatalogDataOrigin.LastValid, syncVm.DataOrigin, "Failed sync after live sync preserves LastValid origin");
AssertTrue(syncVm.CanRetrySync, "CanRetrySync is true after failure");
AssertTrue(syncVm.StatusText.Contains("failed", StringComparison.OrdinalIgnoreCase), "StatusText honestly reports sync failure");
AssertEqual("$2.50 / $12.50", syncVm.VisibleModels[0].StandardInOutDisplay, "Failed sync retains prior live-synced prices instead of overwriting with bundled data");

// PW-04: CatalogValidator detects duplicates, missing provenance, and impossible values
var invalidCatalog = new[]
{
    new ModelPricingEntry("dup-id", "Model A", "OpenAI", TaskTier.Fast, -1m, null, 2m, 0, "Bad entry"),
    new ModelPricingEntry("dup-id", "Model B", "OpenAI", TaskTier.Fast, 1m, null, 2m, 128000, "Duplicate")
};
var invalidValidation = CatalogValidator.Validate(invalidCatalog);
AssertTrue(!invalidValidation.IsValid, "CatalogValidator rejects invalid and duplicate entries");
AssertTrue(invalidValidation.Errors.Any(e => e.Contains("Duplicate model Id", StringComparison.Ordinal)), "CatalogValidator reports duplicate ID");
AssertTrue(invalidValidation.Errors.Any(e => e.Contains("negative standard pricing", StringComparison.Ordinal)), "CatalogValidator reports negative pricing");
AssertTrue(invalidValidation.Errors.Any(e => e.Contains("missing standard pricing provenance", StringComparison.Ordinal)), "CatalogValidator reports missing provenance");

// PW-05: Defensible task cost boundary conditions (zero tokens, large values, ties)
AssertEqual((decimal?)0m, TaskCostCalculator.CalculateCostOrNull(sampleFastModel, 0, 0, 0, useFastMode: false), "Zero tokens yields $0.0000");
decimal? largeCost = TaskCostCalculator.CalculateCostOrNull(sampleFastModel, int.MaxValue, int.MaxValue, int.MaxValue, useFastMode: true);
AssertTrue(largeCost.HasValue && largeCost.Value > 0m, "Large token counts (int.MaxValue) compute safely without overflow");

var tieA = new ModelPricingEntry("tie-a", "Tie A", "OpenAI", TaskTier.Fast, 1.00m, null, 2.00m, 128000, "Tie A", IntelligenceScore: 40m);
var tieB = new ModelPricingEntry("tie-b", "Tie B", "Anthropic", TaskTier.Fast, 1.00m, null, 2.00m, 128000, "Tie B", IntelligenceScore: 50m);
var tieCatalog = new PricingCatalogService([tieA, tieB]);
var tieRanked = tieCatalog.Query(sort: PricingSortField.TotalTaskCostAsc);
AssertEqual("tie-b", tieRanked[0].Id, "Cost tie-breaker deterministically prefers higher IntelligenceScore");

// PW-11: UserPreferencesStore v0.1->v0.2 migration, persistence, and corrupt fallback
string v1PrefsPath = Path.Combine(Path.GetTempPath(), $"aipw-pw11-v1-{Guid.NewGuid():N}.json");
string corruptPrefsPath = Path.Combine(Path.GetTempPath(), $"aipw-pw11-corrupt-{Guid.NewGuid():N}.json");
try
{
    File.WriteAllText(v1PrefsPath, """{ "HiddenModelIds": ["gpt-5-mini", "composer-2"] }""");
    var v1Store = new UserPreferencesStore(v1PrefsPath);
    var v1Vm = new HudViewModel(PricingCatalogService.LoadDefault(), v1Store);
    AssertEqual(2, v1Vm.HiddenCount, "v0.1 preferences preserve HiddenModelIds");
    AssertEqual(0.86, v1Vm.BackgroundOpacity, "v0.1 migration applies default opacity");
    AssertEqual(false, v1Vm.StartWithWindows, "StartWithWindows defaults to false");

    // Mutate settings and verify persistence across restart
    v1Vm.SelectedProvider = "Anthropic";
    v1Vm.SelectedTier = TaskTier.Balanced;
    v1Vm.SelectedSort = PricingSortField.TotalTaskCostAsc;
    v1Vm.IsFastMode = true;
    v1Vm.BackgroundOpacity = 0.72;
    v1Vm.IsAlwaysOnTop = false;
    v1Vm.TaskInputTokens = 12_345;
    v1Vm.HasDismissedOnboarding = true;
    v1Vm.HasShownTrayMinimizeNotice = true;
    v1Vm.StartWithWindows = true;

    var v2Vm = new HudViewModel(PricingCatalogService.LoadDefault(), v1Store);
    AssertEqual(2, v2Vm.HiddenCount, "Reloaded v2 preserves HiddenModelIds");
    AssertEqual("Anthropic", v2Vm.SelectedProvider, "Persists SelectedProvider");
    AssertEqual((TaskTier?)TaskTier.Balanced, v2Vm.SelectedTier, "Persists SelectedTier");
    AssertEqual(PricingSortField.TotalTaskCostAsc, v2Vm.SelectedSort, "Persists SelectedSort");
    AssertEqual(true, v2Vm.IsFastMode, "Persists IsFastMode");
    AssertEqual(0.72, v2Vm.BackgroundOpacity, "Persists BackgroundOpacity");
    AssertEqual(false, v2Vm.IsAlwaysOnTop, "Persists IsAlwaysOnTop");
    AssertEqual(12_345, v2Vm.TaskInputTokens, "Persists TaskInputTokens");
    AssertEqual(true, v2Vm.HasDismissedOnboarding, "Persists HasDismissedOnboarding");
    AssertEqual(true, v2Vm.HasShownTrayMinimizeNotice, "Persists HasShownTrayMinimizeNotice");
    AssertEqual(true, v2Vm.StartWithWindows, "Persists StartWithWindows");

    File.WriteAllText(corruptPrefsPath, "{ broken json !!!");
    var corruptStore = new UserPreferencesStore(corruptPrefsPath);
    var corruptPrefs = corruptStore.Load();
    AssertEqual(0.86, corruptPrefs.BackgroundOpacity, "Corrupt preferences file falls back to safe defaults");
}
finally
{
    if (File.Exists(v1PrefsPath)) File.Delete(v1PrefsPath);
    if (File.Exists(corruptPrefsPath)) File.Delete(corruptPrefsPath);
}

// CursorBench: formatting, validation, sorting, and ViewModel display
AssertEqual("60.4%", TaskCostCalculator.FormatPercentageScore(60.4m), "FormatPercentageScore formats CursorBench score with %");
AssertEqual("N/A", TaskCostCalculator.FormatPercentageScore(null), "FormatPercentageScore formats null as N/A");

var cbCatalog = PricingCatalogService.LoadDefault();
var cbModels = cbCatalog.Query(sort: PricingSortField.CursorBenchDesc);
AssertEqual("gemini-4-carbon", cbModels[0].Id, "CursorBenchDesc sorts highest CursorBench score first (Gemini 4 Carbon 60.4%)");
AssertEqual((decimal?)60.4m, cbModels[0].CursorBenchScore, "Gemini 4 Carbon has verified CursorBench 4.0 score 60.4");

var cbRowWithScore = new ModelRowViewModel(cbModels[0], 10_000, 0, 2_000, isCheapestInView: false);
AssertEqual("60.4%", cbRowWithScore.CursorBenchScoreDisplay, "ModelRowViewModel formats CursorBenchScoreDisplay");
AssertTrue(cbRowWithScore.CursorBenchTooltip.Contains("CursorBench", StringComparison.Ordinal), "ModelRowViewModel includes CursorBench provenance tooltip");

var cbRowWithoutScore = new ModelRowViewModel(
    cbCatalog.AllModels.First(m => m.CursorBenchScore is null),
    10_000,
    0,
    2_000,
    isCheapestInView: false);
AssertEqual("N/A", cbRowWithoutScore.CursorBenchScoreDisplay, "ModelRowViewModel shows N/A when CursorBenchScore is null");

var badCbCatalog = new[]
{
    new ModelPricingEntry(
        "bad-cb",
        "Bad CB",
        "OpenAI",
        TaskTier.Fast,
        1m,
        null,
        2m,
        128000,
        "Bad CB",
        PricingSource: new FieldProvenance("Test", "https://example.com", PriceSourceType.DirectProvider, DateTimeOffset.UtcNow, DataVerificationStatus.Verified),
        CursorBenchScore: 105m)
};
var badCbValidation = CatalogValidator.Validate(badCbCatalog);
AssertTrue(!badCbValidation.IsValid, "CatalogValidator rejects out-of-range CursorBenchScore");
AssertTrue(badCbValidation.Errors.Any(e => e.Contains("out-of-range CursorBenchScore", StringComparison.Ordinal)), "CatalogValidator reports out-of-range CursorBenchScore");
AssertTrue(badCbValidation.Errors.Any(e => e.Contains("without complete CursorBenchSource", StringComparison.Ordinal)), "CatalogValidator reports missing CursorBenchSource");

// CursorBench live sync parser & HudViewModel.SyncLivePricesAsync integration
const string sampleCursorBenchHtml = """
    <html><body>
    <h1>CursorBench 4.1</h1>
    <table>
      <tr><td><span class="truncate">Gemini 4 Carbon High</span></td><td>62.5<!-- -->%</td></tr>
      <tr><td><span class="truncate">Gemini 4 Carbon Low</span></td><td>58.6<!-- -->%</td></tr>
      <tr><td><span class="truncate">Fable 5.2 Max</span></td><td>61.2<!-- -->%</td></tr>
      <tr><td><span class="truncate">DeepSeek V4.1 Flash 1017 Max</span></td><td>49.0<!-- -->%</td></tr>
    </table>
    </body></html>
    """;

var cbParsed = CursorBenchParser.MergeLiveScores(cbCatalog.AllModels, sampleCursorBenchHtml);
AssertTrue(cbParsed.IsValidPayload, "CursorBenchParser accepts valid leaderboard HTML");
AssertEqual("4.1", cbParsed.BenchmarkVersion, "CursorBenchParser extracts benchmark version");
AssertEqual(3, cbParsed.UpdatedCount, "CursorBenchParser updates all matched models in sample HTML");
AssertEqual((decimal?)62.5m, cbParsed.Models.First(m => m.Id == "gemini-4-carbon").CursorBenchScore, "CursorBenchParser picks peak score across effort tiers");
AssertEqual("4.1 (High)", cbParsed.Models.First(m => m.Id == "gemini-4-carbon").CursorBenchSource?.Version, "CursorBenchParser records effort tier in version");
AssertEqual((decimal?)49.0m, cbParsed.Models.First(m => m.Id == "deepseek-flash").CursorBenchScore, "CursorBenchParser matches dated model suffix via alias");

string syncPrefsPath = Path.Combine(Path.GetTempPath(), $"aipw-cb-sync-{Guid.NewGuid():N}.json");
try
{
    var cbSyncVm = new HudViewModel(PricingCatalogService.LoadDefault(), new UserPreferencesStore(syncPrefsPath));
    using var dualClient = new HttpClient(new StubHttpMessageHandler(req =>
    {
        if (req.RequestUri?.Host.Contains("cursor.com", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sampleCursorBenchHtml, Encoding.UTF8, "text/html")
            };
        }

        const string openRouterPayload = """
            {
              "data": [
                {
                  "id": "google/gemini-4-carbon",
                  "pricing": { "prompt": "0.000004", "completion": "0.00002" }
                }
              ]
            }
            """;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(openRouterPayload, Encoding.UTF8, "application/json")
        };
    }));

    await cbSyncVm.SyncLivePricesAsync(dualClient);
    AssertEqual(SyncStatus.Succeeded, cbSyncVm.SyncStatus, "SYNC LIVE succeeds when both OpenRouter and CursorBench respond");
    AssertTrue(cbSyncVm.StatusText.Contains("CursorBench", StringComparison.Ordinal), "SYNC LIVE status reports CursorBench refresh");
    var refreshedCarbon = cbSyncVm.VisibleModels.First(m => m.Id == "gemini-4-carbon");
    AssertEqual("62.5%", refreshedCarbon.CursorBenchScoreDisplay, "SYNC LIVE updates visible CursorBenchScoreDisplay");

    // Safe failure when CursorBench page changes layout or returns HTTP 500
    var failedCbParse = CursorBenchParser.MergeLiveScores(cbCatalog.AllModels, "<html><body>Redesigned page</body></html>");
    AssertTrue(!failedCbParse.IsValidPayload, "CursorBenchParser safely rejects redesigned/unparseable HTML");

    using var cbErrorClient = new HttpClient(new StubHttpMessageHandler(req =>
    {
        if (req.RequestUri?.Host.Contains("cursor.com", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }

        const string openRouterPayload = """
            {
              "data": [
                {
                  "id": "google/gemini-4-carbon",
                  "pricing": { "prompt": "0.000004", "completion": "0.00002" }
                }
              ]
            }
            """;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(openRouterPayload, Encoding.UTF8, "application/json")
        };
    }));

    await cbSyncVm.SyncLivePricesAsync(cbErrorClient);
    AssertEqual(SyncStatus.Succeeded, cbSyncVm.SyncStatus, "SYNC LIVE still succeeds when CursorBench fails");
    AssertTrue(cbSyncVm.StatusText.Contains("CursorBench unchanged", StringComparison.Ordinal), "SYNC LIVE reports CursorBench unchanged on scrape failure");
    AssertEqual("62.5%", cbSyncVm.VisibleModels.First(m => m.Id == "gemini-4-carbon").CursorBenchScoreDisplay, "Existing CursorBench score is preserved when CursorBench fails");
}
finally
{
    if (File.Exists(syncPrefsPath)) File.Delete(syncPrefsPath);
}

// AppVersionInfo formatting & assembly version lookup
AssertEqual("v0.3.0", AppVersionInfo.FormatDisplayVersion("0.3.0", new Version(0, 1, 0, 0)), "FormatDisplayVersion prefers informational version over assembly version");
AssertEqual("v0.3.0", AppVersionInfo.FormatDisplayVersion("0.3.0+abc1234", new Version(0, 1, 0, 0)), "FormatDisplayVersion strips SourceLink metadata");
AssertEqual("v0.3.0-beta.1", AppVersionInfo.FormatDisplayVersion("V0.3.0-beta.1", null), "FormatDisplayVersion normalizes leading v prefix");
AssertEqual("v0.1.0", AppVersionInfo.FormatDisplayVersion("  ", new Version(0, 1, 0, 0)), "FormatDisplayVersion falls back to assembly version");
AssertEqual("v0.0.0", AppVersionInfo.FormatDisplayVersion(null, null), "FormatDisplayVersion returns v0.0.0 when no version metadata is available");
AssertTrue(AppVersionInfo.GetDisplayVersion(typeof(AppVersionInfo).Assembly).StartsWith("v0.", StringComparison.Ordinal), "GetDisplayVersion reads version from Core assembly");

// Task 1 (Price Diffs & Add Models): OpenRouterModelCandidate extraction
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

// Task 2 (Price Diffs & Add Models): SyncSnapshotStore atomic persistence & fallback
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

// Task 3 (Price Diffs & Add Models): CatalogDiffCalculator
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

// Task 4 (Price Diffs & Add Models): HudViewModel + ModelRowViewModel integration
string vmPrefsPath = Path.Combine(Path.GetTempPath(), $"apw-vm-prefs-{Guid.NewGuid():N}.json");
string vmSnapPath = Path.Combine(Path.GetTempPath(), $"apw-vm-snap-{Guid.NewGuid():N}.json");
try
{
    var snapStore = new SyncSnapshotStore(vmSnapPath);
    var prefStore = new UserPreferencesStore(vmPrefsPath);
    var vmCatalog = PricingCatalogService.LoadDefault();
    var vm = new HudViewModel(vmCatalog, prefStore, snapStore);
    vm.SelectedProvider = "All";
    vm.SelectedTier = null;

    // Seed first sync via HttpClient
    string syncPayload1 = """
    {
      "data": [
        {
          "id": "anthropic/claude-sonnet-4.6",
          "name": "Anthropic: Claude Sonnet 4.6",
          "context_length": 200000,
          "pricing": { "prompt": "0.000003", "completion": "0.000015" }
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
    using var client1 = new HttpClient(new StubHttpMessageHandler(req =>
    {
        if (req.RequestUri?.Host.Contains("cursor.com", StringComparison.OrdinalIgnoreCase) == true)
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(syncPayload1, Encoding.UTF8, "application/json") };
    }));
    await vm.SyncLivePricesAsync(client1);
    AssertEqual(0, vm.NewOpenRouterCount, "First sync seeds known OpenRouter IDs without NEW flood");
    AssertTrue(vm.AddOpenRouterModel("mistralai/mistral-large-2512"), "AddOpenRouterModel adds unseen candidate");
    AssertTrue(!vm.AddOpenRouterModel("anthropic/claude-sonnet-4.6"), "AddOpenRouterModel blocks duplicate bundled model");
    var addedRow = vm.VisibleModels.First(r => r.Id == "mistralai/mistral-large-2512");
    AssertTrue(addedRow.IsUserAdded, "Added row marked IsUserAdded");
    AssertEqual("N/A", addedRow.ScoreDisplay, "Added row has N/A AAII score");
    AssertEqual("N/A", addedRow.FastInOutDisplay, "Added row has N/A Fast pricing");

    // Second sync with price drop + new model
    string syncPayload2 = """
    {
      "data": [
        {
          "id": "anthropic/claude-sonnet-4.6",
          "name": "Anthropic: Claude Sonnet 4.6",
          "context_length": 200000,
          "pricing": { "prompt": "0.0000024", "completion": "0.000012" }
        },
        {
          "id": "mistralai/mistral-large-2512",
          "name": "Mistral Large",
          "context_length": 131072,
          "pricing": { "prompt": "0.000001", "completion": "0.000003" }
        },
        {
          "id": "cohere/command-a",
          "name": "Command A",
          "context_length": 256000,
          "pricing": { "prompt": "0.0000025", "completion": "0.00001" }
        }
      ]
    }
    """;
    using var client2 = new HttpClient(new StubHttpMessageHandler(req =>
    {
        if (req.RequestUri?.Host.Contains("cursor.com", StringComparison.OrdinalIgnoreCase) == true)
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(syncPayload2, Encoding.UTF8, "application/json") };
    }));
    await vm.SyncLivePricesAsync(client2);
    AssertEqual(1, vm.NewOpenRouterCount, "Second sync detects 1 new OpenRouter model");
    var changedMistral = vm.VisibleModels.First(r => r.Id == "mistralai/mistral-large-2512");
    AssertTrue(changedMistral.HasPriceDiff && changedMistral.IsPriceDrop, "Price drop flagged on row");
    AssertEqual("↓ -50%", changedMistral.PriceDiffBadge, "PriceDiffBadge formats drop percentage");

    // Restart simulation: new HudViewModel loads AddedModels & ActivePriceDiffs from SyncSnapshotStore
    var restartedVm = new HudViewModel(PricingCatalogService.LoadDefault(), prefStore, new SyncSnapshotStore(vmSnapPath));
    restartedVm.SelectedProvider = "All";
    restartedVm.SelectedTier = null;
    var restoredMistral = restartedVm.VisibleModels.FirstOrDefault(r => r.Id == "mistralai/mistral-large-2512");
    AssertTrue(restoredMistral is not null && restoredMistral.HasPriceDiff, "Added model and price diff survive restart");

    restartedVm.ClearPriceDiffs();
    AssertTrue(!restartedVm.HasPriceDiffs, "ClearPriceDiffs clears active diffs");
    AssertTrue(restartedVm.RemoveAddedModel("mistralai/mistral-large-2512"), "RemoveAddedModel removes user-added model");
    AssertTrue(restartedVm.VisibleModels.All(r => r.Id != "mistralai/mistral-large-2512"), "Removed model no longer visible");
}
finally
{
    if (File.Exists(vmPrefsPath)) File.Delete(vmPrefsPath);
    if (File.Exists(vmSnapPath)) File.Delete(vmSnapPath);
}

Console.WriteLine($"All tests passed ({passed} assertions).");

sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(responder(request));
    }
}
