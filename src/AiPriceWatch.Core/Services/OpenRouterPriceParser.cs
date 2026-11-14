using System.Globalization;
using System.Text.Json;
using AiPriceWatch.Core.Models;

namespace AiPriceWatch.Core.Services;

public static class OpenRouterPriceParser
{
    private const decimal OneMillion = 1_000_000m;

    public static IReadOnlyList<ModelPricingEntry> MergeLivePrices(
        IReadOnlyList<ModelPricingEntry> existingCatalog,
        string openRouterJson)
    {
        ArgumentNullException.ThrowIfNull(existingCatalog);

        if (string.IsNullOrWhiteSpace(openRouterJson))
        {
            return existingCatalog;
        }

        var liveLookup = new Dictionary<string, (decimal InputPerM, decimal OutputPerM, decimal? CachedPerM)>(
            StringComparer.OrdinalIgnoreCase);

        try
        {
            using JsonDocument doc = JsonDocument.Parse(openRouterJson);
            if (!doc.RootElement.TryGetProperty("data", out JsonElement dataArray) ||
                dataArray.ValueKind != JsonValueKind.Array)
            {
                return existingCatalog;
            }

            foreach (JsonElement item in dataArray.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out JsonElement idElem) ||
                    idElem.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string? fullId = idElem.GetString();
                if (string.IsNullOrWhiteSpace(fullId))
                {
                    continue;
                }

                if (!item.TryGetProperty("pricing", out JsonElement pricingElem) ||
                    pricingElem.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!TryReadPerMillionPrice(pricingElem, "prompt", out decimal inputPerM) ||
                    !TryReadPerMillionPrice(pricingElem, "completion", out decimal outputPerM))
                {
                    continue;
                }

                decimal? cachedPerM = null;
                if (TryReadPerMillionPrice(pricingElem, "input_cache_read", out decimal parsedCache))
                {
                    cachedPerM = parsedCache;
                }

                string shortId = fullId.Contains('/')
                    ? fullId[(fullId.IndexOf('/') + 1)..]
                    : fullId;

                liveLookup[shortId] = (inputPerM, outputPerM, cachedPerM);
                liveLookup[fullId] = (inputPerM, outputPerM, cachedPerM);
            }
        }
        catch (JsonException)
        {
            return existingCatalog;
        }

        var updated = new List<ModelPricingEntry>(existingCatalog.Count);
        foreach (ModelPricingEntry entry in existingCatalog)
        {
            if (liveLookup.TryGetValue(entry.Id, out var live))
            {
                updated.Add(entry with
                {
                    InputPerMillion = live.InputPerM,
                    OutputPerMillion = live.OutputPerM,
                    CachedInputPerMillion = live.CachedPerM ?? entry.CachedInputPerMillion
                });
            }
            else
            {
                updated.Add(entry);
            }
        }

        return updated;
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

        pricePerMillion = Math.Round(perToken * OneMillion, 4);
        return true;
    }
}
