using AiPriceWatch.Core.Models;
using AiPriceWatch.Core.Services;

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

var gpt4oMini = new ModelPricingEntry(
    "gpt-4o-mini", "GPT-4o mini", "OpenAI", TaskTier.Fast,
    InputPerMillion: 0.15m, CachedInputPerMillion: 0.075m, OutputPerMillion: 0.60m,
    ContextTokens: 128_000, Notes: "Fast everyday tasks");

var noCacheModel = gpt4oMini with { CachedInputPerMillion = null };

// Task 1: Standard cost calculation
AssertEqual(0.060m, TaskCostCalculator.CalculateCost(gpt4oMini, 100_000, 200_000, 50_000), "Standard cost math");
AssertEqual(0.030m, TaskCostCalculator.CalculateCost(noCacheModel, 100_000, 100_000, 0), "Null cached price fallback");
AssertEqual(0m, TaskCostCalculator.CalculateCost(gpt4oMini, -5000, -1000, -2000), "Negative tokens clamped to 0");
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

// Task 2: Curated Pricing Catalog, Filtering, Sorting & Live OpenRouter Sync
var catalog = PricingCatalogService.LoadDefault();
AssertTrue(catalog.AllModels.Count >= 18, "Default catalog contains at least 18 models across major providers");
var providers = catalog.GetProviders();
AssertTrue(
    providers.Contains("OpenAI") &&
    providers.Contains("Anthropic") &&
    providers.Contains("Google Gemini") &&
    providers.Contains("xAI") &&
    providers.Contains("DeepSeek"),
    "Includes all 5 core providers");

// Case-insensitive & whitespace search + provider filter (Review Focus #4)
var anthropicFast = catalog.Query(provider: " anthropic ", tier: TaskTier.Fast, searchText: "HAIKU");
AssertTrue(anthropicFast.Count >= 1 && anthropicFast.All(m => m.Provider == "Anthropic" && m.Tier == TaskTier.Fast), "Filters by provider, tier, and case-insensitive search");

// Sorting by InputPriceAsc
var sortedByInput = catalog.Query(sort: PricingSortField.InputPriceAsc);
AssertTrue(sortedByInput.First().InputPerMillion <= sortedByInput.Last().InputPerMillion, "Sorts ascending by input price");

// Sorting by ContextDesc
var sortedByContext = catalog.Query(sort: PricingSortField.ContextDesc);
AssertTrue(sortedByContext.First().ContextTokens >= sortedByContext.Last().ContextTokens, "Sorts descending by context window");

// OpenRouter live JSON merge & invalid price resilience (Review Focus #3)
string sampleOpenRouterJson = """
{
  "data": [
    {
      "id": "openai/gpt-4o-mini",
      "pricing": { "prompt": "0.00000012", "completion": "0.00000050", "input_cache_read": "0.00000006" }
    },
    {
      "id": "anthropic/claude-3.5-sonnet",
      "pricing": { "prompt": "-1", "completion": "invalid" }
    }
  ]
}
""";
var merged = OpenRouterPriceParser.MergeLivePrices(catalog.AllModels, sampleOpenRouterJson);
var updatedMini = merged.First(m => m.Id == "gpt-4o-mini");
AssertEqual(0.12m, updatedMini.InputPerMillion, "OpenRouter per-token prompt price converted to $/1M");
AssertEqual(0.50m, updatedMini.OutputPerMillion, "OpenRouter per-token completion price converted to $/1M");
AssertEqual(0.06m, updatedMini.CachedInputPerMillion, "OpenRouter per-token cache read price converted to $/1M");

Console.WriteLine($"All tests passed ({passed} assertions).");
