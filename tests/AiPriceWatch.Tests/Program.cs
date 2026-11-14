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
AssertEqual(0.050m, TaskCostCalculator.CalculateCost(noCacheModel, 100_000, 100_000, 0, useFastMode: true), "Null cached & fast price fallback");
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
AssertEqual("—", TaskCostCalculator.FormatScore(null), "Null score format");
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
AssertTrue(catalog.AllModels.All(m => m.FastInputPerMillion is > 0m && m.FastOutputPerMillion is > 0m), "Every catalog model includes Fast mode input/output pricing");

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

Console.WriteLine($"All tests passed ({passed} assertions).");
