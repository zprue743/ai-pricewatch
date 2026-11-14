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
    ContextTokens: 1_000_000, Notes: "Fast everyday tasks");

var noCacheModel = sampleFastModel with { CachedInputPerMillion = null };

// Task 1: Standard cost calculation: 100k input ($0.025) + 200k cached ($0.005) + 50k output ($0.100) = $0.130
AssertEqual(0.130m, TaskCostCalculator.CalculateCost(sampleFastModel, 100_000, 200_000, 50_000), "Standard cost math");
AssertEqual(0.050m, TaskCostCalculator.CalculateCost(noCacheModel, 100_000, 100_000, 0), "Null cached price fallback");
AssertEqual(0m, TaskCostCalculator.CalculateCost(sampleFastModel, -5000, -1000, -2000), "Negative tokens clamped to 0");
AssertEqual("$0.0000", TaskCostCalculator.FormatCost(0m), "Zero cost format");
AssertEqual("<$0.0001", TaskCostCalculator.FormatCost(0.00004m), "Sub-basis-point cost format");
AssertEqual("$0.0600", TaskCostCalculator.FormatCost(0.06m), "Sub-dollar cost format");
AssertEqual("$1.25", TaskCostCalculator.FormatCost(1.254m), "Dollar+ cost format");
AssertEqual("—", TaskCostCalculator.FormatPricePerMillion(null), "Null price per million format");
AssertEqual("$0.15", TaskCostCalculator.FormatPricePerMillion(0.15m), "Standard price per million format");
AssertEqual("$0.075", TaskCostCalculator.FormatPricePerMillion(0.075m), "Sub-cent price per million format");
AssertEqual("128K", TaskCostCalculator.FormatContextWindow(128_000), "Thousands context window");
AssertEqual("1M", TaskCostCalculator.FormatContextWindow(1_000_000), "Millions context window");
AssertEqual("2M", TaskCostCalculator.FormatContextWindow(2_000_000), "2M context window");

// Task 2: Curated Pricing Catalog — Current Flagships & Defaults
var catalog = PricingCatalogService.LoadDefault();
AssertTrue(catalog.AllModels.Count >= 20, "Default catalog contains at least 20 current models across major providers");
var providers = catalog.GetProviders();
AssertTrue(
    providers.Contains("OpenAI") &&
    providers.Contains("Anthropic") &&
    providers.Contains("Google Gemini") &&
    providers.Contains("xAI") &&
    providers.Contains("DeepSeek"),
    "Includes all 5 core providers");

// Verify current flagships & defaults are present and legacy 2024 models are removed
var ids = catalog.AllModels.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
AssertTrue(ids.Contains("gemini-4-argon") && ids.Contains("gemini-3.8-flash") && ids.Contains("gemini-3.5-flash-lite"), "Contains current Google Gemini 4 & 3.8 lineup");
AssertTrue(ids.Contains("gpt-6-astra") && ids.Contains("gpt-6.1-sol") && ids.Contains("gpt-6.1-terra") && ids.Contains("gpt-5.5") && ids.Contains("gpt-5-mini"), "Contains current OpenAI GPT-6 & GPT-5 lineup");
AssertTrue(ids.Contains("claude-fable-5-2") && ids.Contains("claude-opus-5-5") && ids.Contains("claude-sonnet-5-5") && ids.Contains("claude-haiku-5-5"), "Contains current Anthropic Claude 5.x lineup");
AssertTrue(ids.Contains("grok-4.7") && ids.Contains("grok-4.5") && ids.Contains("grok-4.3"), "Contains current xAI Grok 4.x lineup");
AssertTrue(ids.Contains("deepseek-v4-pro") && ids.Contains("deepseek-flash"), "Contains current DeepSeek V4 lineup");
AssertTrue(!ids.Contains("gpt-4o") && !ids.Contains("gemini-2.0-flash") && !ids.Contains("claude-3.5-haiku"), "Excludes legacy 2024 models");

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
      "id": "anthropic/claude-sonnet-5-5",
      "pricing": { "prompt": "-1", "completion": "invalid" }
    }
  ]
}
""";
var merged = OpenRouterPriceParser.MergeLivePrices(catalog.AllModels, sampleOpenRouterJson);
var updatedMini = merged.First(m => m.Id == "gpt-5-mini");
AssertEqual(0.20m, updatedMini.InputPerMillion, "OpenRouter per-token prompt price converted to $/1M");
AssertEqual(1.80m, updatedMini.OutputPerMillion, "OpenRouter per-token completion price converted to $/1M");
AssertEqual(0.02m, updatedMini.CachedInputPerMillion, "OpenRouter per-token cache read price converted to $/1M");

// Task 3: HUD ViewModel & Task Presets
var vm = new HudViewModel(PricingCatalogService.LoadDefault());
AssertEqual("All", vm.SelectedProvider, "Default provider is All");
AssertTrue(vm.VisibleModels.Count >= 20, "Shows all models initially");
AssertTrue(vm.VisibleModels.Any(m => m.IsCheapestInView), "Marks cheapest model in view");

// Provider switching updates VisibleModels
vm.SelectedProvider = "Google Gemini";
AssertTrue(vm.VisibleModels.All(m => m.Provider == "Google Gemini"), "Filtering by Google Gemini only shows Gemini models");

// Selecting preset updates token counts and recomputes task cost
var longContextPreset = vm.Presets.First(p => p.InputTokens >= 200_000);
vm.SelectedPreset = longContextPreset;
AssertEqual(longContextPreset.InputTokens, vm.TaskInputTokens, "Preset updates TaskInputTokens");

// Opacity clamping (Review Focus #5)
vm.BackgroundOpacity = 0.05;
AssertEqual(0.35, vm.BackgroundOpacity, "Clamps minimum opacity to 0.35");
vm.BackgroundOpacity = 1.5;
AssertEqual(1.0, vm.BackgroundOpacity, "Clamps maximum opacity to 1.0");

Console.WriteLine($"All tests passed ({passed} assertions).");
