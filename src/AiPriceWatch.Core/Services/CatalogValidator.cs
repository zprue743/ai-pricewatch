using AiPriceWatch.Core.Models;

namespace AiPriceWatch.Core.Services;

public sealed record CatalogValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public static CatalogValidationResult Success(IReadOnlyList<string>? warnings = null) =>
        new(true, Array.Empty<string>(), warnings ?? Array.Empty<string>());
}

public static class CatalogValidator
{
    private static readonly TimeSpan MaxStaleAge = TimeSpan.FromDays(180);

    public static CatalogValidationResult Validate(
        IEnumerable<ModelPricingEntry> entries,
        DateTimeOffset? referenceNow = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        DateTimeOffset now = referenceNow ?? DateTimeOffset.UtcNow;

        var errors = new List<string>();
        var warnings = new List<string>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ModelPricingEntry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                errors.Add("Catalog entry has empty Id.");
                continue;
            }

            if (!seenIds.Add(entry.Id))
            {
                errors.Add($"Duplicate model Id '{entry.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                errors.Add($"Model '{entry.Id}' has empty Name.");
            }

            if (string.IsNullOrWhiteSpace(entry.Provider))
            {
                errors.Add($"Model '{entry.Id}' has empty Provider.");
            }

            if (entry.InputPerMillion < 0m || entry.OutputPerMillion < 0m)
            {
                errors.Add($"Model '{entry.Id}' has negative standard pricing.");
            }

            if (entry.CachedInputPerMillion is < 0m)
            {
                errors.Add($"Model '{entry.Id}' has negative cached input pricing.");
            }

            if (entry.FastInputPerMillion.HasValue != entry.FastOutputPerMillion.HasValue)
            {
                errors.Add($"Model '{entry.Id}' must define both FastInputPerMillion and FastOutputPerMillion or neither.");
            }

            if (entry.FastInputPerMillion is < 0m || entry.FastOutputPerMillion is < 0m)
            {
                errors.Add($"Model '{entry.Id}' has negative Fast pricing.");
            }

            if (entry.ContextTokens <= 0)
            {
                errors.Add($"Model '{entry.Id}' has non-positive ContextTokens ({entry.ContextTokens}).");
            }

            if (entry.IntelligenceScore is < 0m or > 100m)
            {
                errors.Add($"Model '{entry.Id}' has out-of-range IntelligenceScore ({entry.IntelligenceScore}).");
            }

            FieldProvenance stdProv = entry.EffectiveStandardProvenance;
            if (stdProv.Status == DataVerificationStatus.Unknown ||
                string.IsNullOrWhiteSpace(stdProv.SourceName) ||
                string.IsNullOrWhiteSpace(stdProv.SourceUrl))
            {
                errors.Add($"Model '{entry.Id}' is missing standard pricing provenance (SourceName, SourceUrl, or Status).");
            }
            else if (!stdProv.LastVerifiedAt.HasValue)
            {
                errors.Add($"Model '{entry.Id}' is missing LastVerifiedAt timestamp on standard pricing.");
            }
            else if (stdProv.LastVerifiedAt.Value > now.AddDays(1))
            {
                errors.Add($"Model '{entry.Id}' has future LastVerifiedAt ({stdProv.LastVerifiedAt.Value:O}).");
            }
            else if (now - stdProv.LastVerifiedAt.Value > MaxStaleAge)
            {
                warnings.Add($"Model '{entry.Id}' pricing verification is older than {MaxStaleAge.Days} days ({stdProv.LastVerifiedAt.Value:yyyy-MM-dd}).");
            }

            if (entry.FastInputPerMillion.HasValue && entry.FastOutputPerMillion.HasValue)
            {
                FieldProvenance fastProv = entry.EffectiveFastProvenance;
                if (fastProv.Status is DataVerificationStatus.Unknown or DataVerificationStatus.Unavailable ||
                    string.IsNullOrWhiteSpace(fastProv.SourceName))
                {
                    errors.Add($"Model '{entry.Id}' defines Fast pricing without Fast/Standard provenance.");
                }
            }

            if (entry.IntelligenceScore.HasValue)
            {
                BenchmarkProvenance benchProv = entry.EffectiveBenchmarkProvenance;
                if (benchProv.Status is DataVerificationStatus.Unknown or DataVerificationStatus.Unavailable ||
                    string.IsNullOrWhiteSpace(benchProv.Publisher) ||
                    string.IsNullOrWhiteSpace(benchProv.BenchmarkName) ||
                    !benchProv.ScoreDate.HasValue)
                {
                    errors.Add($"Model '{entry.Id}' has IntelligenceScore without complete BenchmarkSource metadata.");
                }
            }
        }

        return new CatalogValidationResult(errors.Count == 0, errors, warnings);
    }
}
