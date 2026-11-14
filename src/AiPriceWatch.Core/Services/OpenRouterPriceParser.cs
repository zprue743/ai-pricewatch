using System.Globalization;
using System.Text.Json;
using AiPriceWatch.Core.Models;

namespace AiPriceWatch.Core.Services;

public sealed record OpenRouterSyncResult(
    IReadOnlyList<ModelPricingEntry> Models,
    bool IsValidPayload,
    int UpdatedCount,
    int IgnoredCount,
    int DuplicateCount,
    string? ErrorMessage = null);

public static class OpenRouterPriceParser
{
    private const decimal OneMillion = 1_000_000m;
    private const decimal MaxReasonablePricePerMillion = 100_000m;

    public static IReadOnlyList<ModelPricingEntry> MergeLivePrices(
        IReadOnlyList<ModelPricingEntry> existingCatalog,
        string openRouterJson)
    {
        return MergeLivePrices(existingCatalog, openRouterJson, out _);
    }

    public static IReadOnlyList<ModelPricingEntry> MergeLivePrices(
        IReadOnlyList<ModelPricingEntry> existingCatalog,
        string openRouterJson,
        out int updatedCount)
    {
        OpenRouterSyncResult result = MergeLivePricesDetailed(existingCatalog, openRouterJson);
        updatedCount = result.UpdatedCount;
        return result.Models;
    }

    public static OpenRouterSyncResult MergeLivePricesDetailed(
        IReadOnlyList<ModelPricingEntry> existingCatalog,
        string openRouterJson)
    {
        ArgumentNullException.ThrowIfNull(existingCatalog);

        if (string.IsNullOrWhiteSpace(openRouterJson))
        {
            return new OpenRouterSyncResult(
                existingCatalog,
                IsValidPayload: false,
                UpdatedCount: 0,
                IgnoredCount: 0,
                DuplicateCount: 0,
                ErrorMessage: "OpenRouter response was empty.");
        }

        var fullIdLookup = new Dictionary<string, (decimal InputPerM, decimal OutputPerM, decimal? CachedPerM)>(
            StringComparer.OrdinalIgnoreCase);
        var shortIdCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var shortIdLookup = new Dictionary<string, (decimal InputPerM, decimal OutputPerM, decimal? CachedPerM)>(
            StringComparer.OrdinalIgnoreCase);

        int ignoredCount = 0;
        int duplicateCount = 0;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(openRouterJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("data", out JsonElement dataArray) ||
                dataArray.ValueKind != JsonValueKind.Array)
            {
                return new OpenRouterSyncResult(
                    existingCatalog,
                    IsValidPayload: false,
                    UpdatedCount: 0,
                    IgnoredCount: 0,
                    DuplicateCount: 0,
                    ErrorMessage: "OpenRouter payload missing 'data' array.");
            }

            foreach (JsonElement item in dataArray.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("id", out JsonElement idElem) ||
                    idElem.ValueKind != JsonValueKind.String)
                {
                    ignoredCount++;
                    continue;
                }

                string? fullId = idElem.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(fullId))
                {
                    ignoredCount++;
                    continue;
                }

                if (fullIdLookup.ContainsKey(fullId))
                {
                    duplicateCount++;
                    ignoredCount++;
                    continue;
                }

                if (!item.TryGetProperty("pricing", out JsonElement pricingElem) ||
                    pricingElem.ValueKind != JsonValueKind.Object)
                {
                    ignoredCount++;
                    continue;
                }

                if (!TryReadPerMillionPrice(pricingElem, "prompt", out decimal inputPerM) ||
                    !TryReadPerMillionPrice(pricingElem, "completion", out decimal outputPerM))
                {
                    ignoredCount++;
                    continue;
                }

                decimal? cachedPerM = null;
                if (pricingElem.TryGetProperty("input_cache_read", out _) &&
                    TryReadPerMillionPrice(pricingElem, "input_cache_read", out decimal parsedCache))
                {
                    cachedPerM = parsedCache;
                }

                var rate = (inputPerM, outputPerM, cachedPerM);
                fullIdLookup[fullId] = rate;

                string shortId = fullId.Contains('/')
                    ? fullId[(fullId.IndexOf('/') + 1)..]
                    : fullId;

                shortIdCounts[shortId] = shortIdCounts.GetValueOrDefault(shortId) + 1;
                if (shortIdCounts[shortId] == 1)
                {
                    shortIdLookup[shortId] = rate;
                }
                else
                {
                    // Ambiguous short ID across multiple providers: never match by short ID alone.
                    shortIdLookup.Remove(shortId);
                }
            }
        }
        catch (JsonException ex)
        {
            return new OpenRouterSyncResult(
                existingCatalog,
                IsValidPayload: false,
                UpdatedCount: 0,
                IgnoredCount: ignoredCount,
                DuplicateCount: duplicateCount,
                ErrorMessage: $"Invalid OpenRouter JSON: {ex.Message}");
        }

        if (fullIdLookup.Count == 0)
        {
            return new OpenRouterSyncResult(
                existingCatalog,
                IsValidPayload: false,
                UpdatedCount: 0,
                IgnoredCount: ignoredCount,
                DuplicateCount: duplicateCount,
                ErrorMessage: "OpenRouter response contained no valid model pricing entries.");
        }

        int updatedCount = 0;
        DateTimeOffset syncedAt = DateTimeOffset.UtcNow;
        var openRouterProvenance = new FieldProvenance(
            SourceName: "OpenRouter API",
            SourceUrl: "https://openrouter.ai/api/v1/models",
            SourceType: PriceSourceType.OpenRouter,
            LastVerifiedAt: syncedAt,
            Status: DataVerificationStatus.Verified);

        var updated = new List<ModelPricingEntry>(existingCatalog.Count);
        foreach (ModelPricingEntry entry in existingCatalog)
        {
            bool matched = false;
            (decimal InputPerM, decimal OutputPerM, decimal? CachedPerM) live = default;

            if (!string.IsNullOrWhiteSpace(entry.OpenRouterId) &&
                fullIdLookup.TryGetValue(entry.OpenRouterId, out live))
            {
                matched = true;
            }
            else if (fullIdLookup.TryGetValue(entry.Id, out live))
            {
                matched = true;
            }
            else if (string.IsNullOrWhiteSpace(entry.OpenRouterId) &&
                     shortIdLookup.TryGetValue(entry.Id, out live))
            {
                matched = true;
            }

            if (matched)
            {
                updatedCount++;
                // PW-02: Never derive or overwrite Fast/Priority pricing from OpenRouter standard rates.
                updated.Add(entry with
                {
                    InputPerMillion = live.InputPerM,
                    OutputPerMillion = live.OutputPerM,
                    CachedInputPerMillion = live.CachedPerM ?? entry.CachedInputPerMillion,
                    PricingSource = openRouterProvenance,
                    CachedPricingSource = live.CachedPerM.HasValue ? openRouterProvenance : entry.CachedPricingSource
                });
            }
            else
            {
                updated.Add(entry);
            }
        }

        return new OpenRouterSyncResult(
            updated,
            IsValidPayload: true,
            UpdatedCount: updatedCount,
            IgnoredCount: ignoredCount,
            DuplicateCount: duplicateCount);
    }

    private static bool TryReadPerMillionPrice(
        JsonElement pricingObj,
        string propertyName,
        out decimal pricePerMillion)
    {
        pricePerMillion = 0m;
        if (!pricingObj.TryGetProperty(propertyName, out JsonElement prop))
        {
            return false;
        }

        string? raw = prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(raw) ||
            !decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal perToken) ||
            perToken < 0m)
        {
            return false;
        }

        decimal scaled = Math.Round(perToken * OneMillion, 4);
        if (scaled > MaxReasonablePricePerMillion)
        {
            return false;
        }

        pricePerMillion = scaled;
        return true;
    }
}
