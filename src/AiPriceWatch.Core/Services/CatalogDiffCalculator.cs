using AiPriceWatch.Core.Models;

namespace AiPriceWatch.Core.Services;

public sealed record CatalogDiffResult(
    Dictionary<string, PriceDiff> PriceDiffs,
    HashSet<string> NewOpenRouterIds,
    Dictionary<string, PriceSnapshot> UpdatedPriceSnapshots,
    HashSet<string> UpdatedKnownOpenRouterIds);

public static class CatalogDiffCalculator
{
    private const decimal NoiseFloorPercent = 0.1m;

    public static CatalogDiffResult ComputeDiffs(
        IReadOnlyList<ModelPricingEntry> previousModels,
        IReadOnlyList<ModelPricingEntry> currentModels,
        IReadOnlyList<OpenRouterModelCandidate> availableCandidates,
        IReadOnlyDictionary<string, PriceSnapshot> priorPrices,
        IReadOnlySet<string> priorKnownOpenRouterIds,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(previousModels);
        ArgumentNullException.ThrowIfNull(currentModels);
        ArgumentNullException.ThrowIfNull(availableCandidates);
        ArgumentNullException.ThrowIfNull(priorPrices);
        ArgumentNullException.ThrowIfNull(priorKnownOpenRouterIds);

        var previousById = new Dictionary<string, ModelPricingEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (ModelPricingEntry entry in previousModels)
        {
            previousById[entry.Id] = entry;
        }

        var priceDiffs = new Dictionary<string, PriceDiff>(StringComparer.OrdinalIgnoreCase);
        var updatedSnapshots = new Dictionary<string, PriceSnapshot>(StringComparer.OrdinalIgnoreCase);
        var existingOpenRouterKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ModelPricingEntry current in currentModels)
        {
            updatedSnapshots[current.Id] = new PriceSnapshot(
                current.InputPerMillion,
                current.OutputPerMillion,
                current.CachedInputPerMillion);

            existingOpenRouterKeys.Add(current.Id);
            if (!string.IsNullOrWhiteSpace(current.OpenRouterId))
            {
                existingOpenRouterKeys.Add(current.OpenRouterId);
            }

            decimal prevInput;
            decimal prevOutput;
            if (priorPrices.TryGetValue(current.Id, out PriceSnapshot? priorSnap))
            {
                prevInput = priorSnap.InputPerMillion;
                prevOutput = priorSnap.OutputPerMillion;
            }
            else if (previousById.TryGetValue(current.Id, out ModelPricingEntry? prevEntry))
            {
                prevInput = prevEntry.InputPerMillion;
                prevOutput = prevEntry.OutputPerMillion;
            }
            else
            {
                continue;
            }

            decimal? outputPct = ComputePercentChange(prevOutput, current.OutputPerMillion);
            decimal? inputPct = ComputePercentChange(prevInput, current.InputPerMillion);
            decimal? chosenPct = outputPct ?? inputPct;

            if (chosenPct.HasValue)
            {
                priceDiffs[current.Id] = new PriceDiff(
                    PreviousInputPerMillion: prevInput,
                    PreviousOutputPerMillion: prevOutput,
                    NewInputPerMillion: current.InputPerMillion,
                    NewOutputPerMillion: current.OutputPerMillion,
                    PercentChange: chosenPct.Value,
                    DetectedAt: now);
            }
        }

        bool hasPriorKnownIds = priorKnownOpenRouterIds.Count > 0;
        var updatedKnownIds = new HashSet<string>(priorKnownOpenRouterIds, StringComparer.OrdinalIgnoreCase);
        var newOpenRouterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (OpenRouterModelCandidate candidate in availableCandidates)
        {
            updatedKnownIds.Add(candidate.FullId);
            if (hasPriorKnownIds &&
                !priorKnownOpenRouterIds.Contains(candidate.FullId) &&
                !existingOpenRouterKeys.Contains(candidate.FullId))
            {
                newOpenRouterIds.Add(candidate.FullId);
            }
        }

        return new CatalogDiffResult(
            PriceDiffs: priceDiffs,
            NewOpenRouterIds: newOpenRouterIds,
            UpdatedPriceSnapshots: updatedSnapshots,
            UpdatedKnownOpenRouterIds: updatedKnownIds);
    }

    private static decimal? ComputePercentChange(decimal previous, decimal current)
    {
        if (previous == current)
        {
            return null;
        }

        decimal pct = previous <= 0m
            ? (current > 0m ? 100.0m : 0m)
            : Math.Round(((current - previous) / previous) * 100m, 1);

        return Math.Abs(pct) >= NoiseFloorPercent ? pct : null;
    }
}
