using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiPriceWatch.Core.Models;

namespace AiPriceWatch.Core.Services;

public sealed class PricingCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    private List<ModelPricingEntry> _models;

    public PricingCatalogService(IEnumerable<ModelPricingEntry> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        _models = models.ToList();
    }

    public IReadOnlyList<ModelPricingEntry> AllModels => _models;

    public static PricingCatalogService LoadDefault()
    {
        Assembly assembly = typeof(PricingCatalogService).Assembly;
        string? resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("pricing-catalog.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException("Embedded pricing-catalog.json resource was not found.");
        }

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException("Unable to open embedded pricing-catalog.json stream.");
        }

        using StreamReader reader = new(stream);
        string json = reader.ReadToEnd();
        return FromJson(json);
    }

    public static PricingCatalogService FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new PricingCatalogService(Array.Empty<ModelPricingEntry>());
        }

        try
        {
            List<ModelPricingEntry>? entries = JsonSerializer.Deserialize<List<ModelPricingEntry>>(json, JsonOptions);
            if (entries is null)
            {
                return new PricingCatalogService(Array.Empty<ModelPricingEntry>());
            }

            // Filter out null or structurally invalid entries without crashing startup.
            var valid = entries
                .Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(e.Name))
                .ToList();
            return new PricingCatalogService(valid);
        }
        catch (JsonException)
        {
            return new PricingCatalogService(Array.Empty<ModelPricingEntry>());
        }
    }

    public IReadOnlyList<string> GetProviders()
    {
        return _models
            .Select(m => m.Provider)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ModelPricingEntry> Query(
        string? provider = null,
        TaskTier? tier = null,
        string? searchText = null,
        PricingSortField sort = PricingSortField.ScoreDesc,
        int taskInputTokens = 10_000,
        int taskCachedTokens = 0,
        int taskOutputTokens = 2_000,
        bool useFastMode = false)
    {
        IEnumerable<ModelPricingEntry> query = _models;

        string? normalizedProvider = provider?.Trim();
        if (!string.IsNullOrEmpty(normalizedProvider) &&
            !string.Equals(normalizedProvider, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(m => string.Equals(m.Provider, normalizedProvider, StringComparison.OrdinalIgnoreCase));
        }

        if (tier.HasValue)
        {
            query = query.Where(m => m.Tier == tier.Value);
        }

        string? normalizedSearch = searchText?.Trim();
        if (!string.IsNullOrEmpty(normalizedSearch))
        {
            query = query.Where(m =>
                m.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                m.Id.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                m.Provider.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                m.Notes.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
        }

        query = sort switch
        {
            PricingSortField.Featured => query,
            PricingSortField.ScoreDesc => query
                .OrderByDescending(m => m.IntelligenceScore ?? 0m)
                .ThenBy(m => m.InputPerMillion),
            PricingSortField.CursorBenchDesc => query
                .OrderByDescending(m => m.CursorBenchScore ?? 0m)
                .ThenByDescending(m => m.IntelligenceScore ?? 0m)
                .ThenBy(m => m.InputPerMillion),
            PricingSortField.InputPriceAsc => useFastMode
                ? query
                    .OrderBy(m => !m.FastInputPerMillion.HasValue)
                    .ThenBy(m => m.FastInputPerMillion ?? decimal.MaxValue)
                    .ThenBy(m => m.FastOutputPerMillion ?? decimal.MaxValue)
                : query
                    .OrderBy(m => m.InputPerMillion)
                    .ThenBy(m => m.OutputPerMillion),
            PricingSortField.OutputPriceAsc => useFastMode
                ? query
                    .OrderBy(m => !m.FastOutputPerMillion.HasValue)
                    .ThenBy(m => m.FastOutputPerMillion ?? decimal.MaxValue)
                    .ThenBy(m => m.FastInputPerMillion ?? decimal.MaxValue)
                : query
                    .OrderBy(m => m.OutputPerMillion)
                    .ThenBy(m => m.InputPerMillion),
            PricingSortField.TotalTaskCostAsc => query
                .Select(m => new
                {
                    Model = m,
                    Cost = TaskCostCalculator.CalculateCostOrNull(m, taskInputTokens, taskCachedTokens, taskOutputTokens, useFastMode)
                })
                .OrderBy(x => !x.Cost.HasValue)
                .ThenBy(x => x.Cost ?? decimal.MaxValue)
                .ThenByDescending(x => x.Model.IntelligenceScore ?? 0m)
                .Select(x => x.Model),
            PricingSortField.ContextDesc => query
                .OrderByDescending(m => m.ContextTokens)
                .ThenBy(m => m.InputPerMillion),
            PricingSortField.NameAsc => query
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase),
            _ => query
        };

        return query.ToList();
    }

    public void MergeEntries(IEnumerable<ModelPricingEntry> updatedEntries)
    {
        ArgumentNullException.ThrowIfNull(updatedEntries);
        _models = updatedEntries.ToList();
    }
}
