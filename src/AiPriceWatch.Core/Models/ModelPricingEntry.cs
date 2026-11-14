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
    decimal? IntelligenceScore = null);
