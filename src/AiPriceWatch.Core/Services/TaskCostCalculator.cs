using System.Globalization;
using AiPriceWatch.Core.Models;

namespace AiPriceWatch.Core.Services;

public static class TaskCostCalculator
{
    private const decimal OneMillion = 1_000_000m;

    public static decimal CalculateCost(
        ModelPricingEntry model,
        int inputTokens,
        int cachedInputTokens,
        int outputTokens,
        bool useFastMode = false)
    {
        ArgumentNullException.ThrowIfNull(model);

        long clampedInput = Math.Max(0, inputTokens);
        long clampedCached = Math.Max(0, cachedInputTokens);
        long clampedOutput = Math.Max(0, outputTokens);

        decimal activeInputRate = useFastMode
            ? (model.FastInputPerMillion ?? model.InputPerMillion)
            : model.InputPerMillion;

        decimal activeOutputRate = useFastMode
            ? (model.FastOutputPerMillion ?? model.OutputPerMillion)
            : model.OutputPerMillion;

        decimal effectiveCachedRate = model.CachedInputPerMillion ?? activeInputRate;

        decimal inputCost = (clampedInput / OneMillion) * activeInputRate;
        decimal cachedCost = (clampedCached / OneMillion) * effectiveCachedRate;
        decimal outputCost = (clampedOutput / OneMillion) * activeOutputRate;

        return Math.Max(0m, inputCost + cachedCost + outputCost);
    }

    public static string FormatCost(decimal costUsd)
    {
        if (costUsd <= 0m)
        {
            return "$0.0000";
        }

        if (costUsd < 0.0001m)
        {
            return "<$0.0001";
        }

        if (costUsd < 1.00m)
        {
            return string.Create(CultureInfo.InvariantCulture, $"${costUsd:F4}");
        }

        return string.Create(CultureInfo.InvariantCulture, $"${costUsd:F2}");
    }

    public static string FormatPricePerMillion(decimal? pricePerMillion)
    {
        if (pricePerMillion is null)
        {
            return "—";
        }

        decimal value = pricePerMillion.Value;
        if (value == Math.Round(value, 2))
        {
            return string.Create(CultureInfo.InvariantCulture, $"${value:F2}");
        }

        return string.Create(CultureInfo.InvariantCulture, $"${value:0.00##}");
    }

    public static string FormatInOutPair(decimal? inputPerMillion, decimal? outputPerMillion)
    {
        if (inputPerMillion is null || outputPerMillion is null)
        {
            return "—";
        }

        return $"{FormatPricePerMillion(inputPerMillion)} / {FormatPricePerMillion(outputPerMillion)}";
    }

    public static string FormatScore(decimal? score)
    {
        if (score is null || score.Value <= 0m)
        {
            return "N/A";
        }

        return string.Create(CultureInfo.InvariantCulture, $"{score.Value:F1}");
    }

    public static string FormatContextWindow(int contextTokens)
    {
        if (contextTokens <= 0)
        {
            return "—";
        }

        if (contextTokens >= 1_000_000)
        {
            decimal millions = contextTokens / 1_000_000m;
            return millions == Math.Floor(millions)
                ? string.Create(CultureInfo.InvariantCulture, $"{millions:F0}M")
                : string.Create(CultureInfo.InvariantCulture, $"{millions:0.#}M");
        }

        int thousands = (int)Math.Round(contextTokens / 1_000.0);
        return string.Create(CultureInfo.InvariantCulture, $"{thousands}K");
    }
}
