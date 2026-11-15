namespace AiPriceWatch.Core.Models;

public sealed record ModelPricingEntry(
    string Id,
    string Name,
    string Provider,
    TaskTier Tier,
    decimal InputPerMillion,
    decimal? CachedInputPerMillion,
    decimal OutputPerMillion,
    int ContextTokens,
    string Notes,
    decimal? FastInputPerMillion = null,
    decimal? FastOutputPerMillion = null,
    decimal? IntelligenceScore = null,
    string? OpenRouterId = null,
    FieldProvenance? PricingSource = null,
    FieldProvenance? CachedPricingSource = null,
    FieldProvenance? FastPricingSource = null,
    BenchmarkProvenance? BenchmarkSource = null,
    decimal? CursorBenchScore = null,
    BenchmarkProvenance? CursorBenchSource = null)
{
    public FieldProvenance EffectiveStandardProvenance =>
        PricingSource ?? FieldProvenance.Unknown;

    public FieldProvenance EffectiveCachedProvenance =>
        CachedPricingSource ?? (CachedInputPerMillion is null
            ? FieldProvenance.Unavailable
            : PricingSource ?? FieldProvenance.Unknown);

    public FieldProvenance EffectiveFastProvenance =>
        FastPricingSource ?? (FastInputPerMillion is null || FastOutputPerMillion is null
            ? FieldProvenance.Unavailable
            : PricingSource ?? FieldProvenance.Unknown);

    public BenchmarkProvenance EffectiveBenchmarkProvenance =>
        BenchmarkSource ?? (IntelligenceScore is null
            ? BenchmarkProvenance.Unavailable
            : BenchmarkProvenance.Unknown);

    public BenchmarkProvenance EffectiveCursorBenchProvenance =>
        CursorBenchSource ?? (CursorBenchScore is null
            ? BenchmarkProvenance.Unavailable
            : BenchmarkProvenance.Unknown);
}
