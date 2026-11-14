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

var gpt4oMini = new ModelPricingEntry(
    "gpt-4o-mini", "GPT-4o mini", "OpenAI", TaskTier.Fast,
    InputPerMillion: 0.15m, CachedInputPerMillion: 0.075m, OutputPerMillion: 0.60m,
    ContextTokens: 128_000, Notes: "Fast everyday tasks");

var noCacheModel = gpt4oMini with { CachedInputPerMillion = null };

// Standard cost calculation: 100k input ($0.015) + 200k cached ($0.015) + 50k output ($0.030) = $0.060
AssertEqual(0.060m, TaskCostCalculator.CalculateCost(gpt4oMini, 100_000, 200_000, 50_000), "Standard cost math");

// Fallback when CachedInputPerMillion is null: uses InputPerMillion
AssertEqual(0.030m, TaskCostCalculator.CalculateCost(noCacheModel, 100_000, 100_000, 0), "Null cached price fallback");

// Negative token clamping (Review Focus #1)
AssertEqual(0m, TaskCostCalculator.CalculateCost(gpt4oMini, -5000, -1000, -2000), "Negative tokens clamped to 0");

// Formatting checks
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

Console.WriteLine($"All tests passed ({passed} assertions).");
