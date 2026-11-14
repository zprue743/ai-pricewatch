using AiPriceWatch.Core.Models;
using AiPriceWatch.Core.Services;

namespace AiPriceWatch.Core.ViewModels;

public sealed class ModelRowViewModel
{
    public ModelRowViewModel(
        ModelPricingEntry entry,
        int taskInputTokens,
        int taskCachedTokens,
        int taskOutputTokens,
        bool isCheapestInView,
        bool isFastMode = false,
        bool isHidden = false)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Id = entry.Id;
        Name = entry.Name;
        Provider = entry.Provider;
        Tier = entry.Tier;
        TierCode = entry.Tier.ToString();
        TierBadge = entry.Tier switch
        {
            TaskTier.Fast => "FAST / BUDGET",
            TaskTier.Balanced => "BALANCED",
            TaskTier.Reasoning => "REASONING",
            _ => entry.Tier.ToString().ToUpperInvariant()
        };

        ScoreRaw = entry.IntelligenceScore;
        ScoreDisplay = TaskCostCalculator.FormatScore(entry.IntelligenceScore);

        InputPriceDisplay = TaskCostCalculator.FormatPricePerMillion(entry.InputPerMillion);
        CachedPriceDisplay = TaskCostCalculator.FormatPricePerMillion(entry.CachedInputPerMillion);
        OutputPriceDisplay = TaskCostCalculator.FormatPricePerMillion(entry.OutputPerMillion);
        StandardInOutDisplay = TaskCostCalculator.FormatInOutPair(entry.InputPerMillion, entry.OutputPerMillion);

        FastInOutDisplay = TaskCostCalculator.FormatInOutPair(entry.FastInputPerMillion, entry.FastOutputPerMillion);

        ContextDisplay = TaskCostCalculator.FormatContextWindow(entry.ContextTokens);

        EstimatedTaskCostRaw = TaskCostCalculator.CalculateCostOrNull(
            entry,
            taskInputTokens,
            taskCachedTokens,
            taskOutputTokens,
            useFastMode: isFastMode);

        EstimatedTaskCostDisplay = TaskCostCalculator.FormatCost(EstimatedTaskCostRaw);
        Notes = entry.Notes;
        StandardProvenance = entry.EffectiveStandardProvenance;
        CachedProvenance = entry.EffectiveCachedProvenance;
        FastProvenance = entry.EffectiveFastProvenance;
        BenchmarkProvenance = entry.EffectiveBenchmarkProvenance;

        PricingSourceBadge = StandardProvenance.SourceType switch
        {
            PriceSourceType.DirectProvider => "Direct",
            PriceSourceType.OpenRouter => "OpenRouter",
            PriceSourceType.Estimate => "Estimated",
            _ => "Unknown"
        };

        StandardPricingTooltip = $"Standard: {StandardProvenance.FormatSummary()}";
        CachedPricingTooltip = $"Cached: {CachedProvenance.FormatSummary()}";
        FastPricingTooltip = $"Fast/Priority: {FastProvenance.FormatSummary()}";
        BenchmarkTooltip = BenchmarkProvenance.FormatSummary();
        RowDetailsTooltip = string.IsNullOrWhiteSpace(entry.Notes)
            ? $"{StandardPricingTooltip}\n{CachedPricingTooltip}\n{FastPricingTooltip}\nBenchmark: {BenchmarkTooltip}"
            : $"{entry.Notes}\n\n{StandardPricingTooltip}\n{CachedPricingTooltip}\n{FastPricingTooltip}\nBenchmark: {BenchmarkTooltip}";

        IsCheapestInView = isCheapestInView;
        IsFastModeActive = isFastMode;
        IsHidden = isHidden;
    }

    public string Id { get; }
    public string Name { get; }
    public string Provider { get; }
    public TaskTier Tier { get; }
    public string TierCode { get; }
    public string TierBadge { get; }
    public decimal? ScoreRaw { get; }
    public string ScoreDisplay { get; }
    public string InputPriceDisplay { get; }
    public string CachedPriceDisplay { get; }
    public string OutputPriceDisplay { get; }
    public string StandardInOutDisplay { get; }
    public string FastInOutDisplay { get; }
    public string ContextDisplay { get; }
    public decimal? EstimatedTaskCostRaw { get; }
    public string EstimatedTaskCostDisplay { get; }
    public string Notes { get; }
    public FieldProvenance StandardProvenance { get; }
    public FieldProvenance CachedProvenance { get; }
    public FieldProvenance FastProvenance { get; }
    public BenchmarkProvenance BenchmarkProvenance { get; }
    public string PricingSourceBadge { get; }
    public string StandardPricingTooltip { get; }
    public string CachedPricingTooltip { get; }
    public string FastPricingTooltip { get; }
    public string BenchmarkTooltip { get; }
    public string RowDetailsTooltip { get; }
    public bool IsCheapestInView { get; }
    public bool IsFastModeActive { get; }
    public bool IsHidden { get; }
    public string HideActionLabel => IsHidden ? "UNHIDE" : "HIDE";
    public string HideActionTooltip => IsHidden
        ? "Unhide this model so it appears in the HUD"
        : "Hide this model from the HUD";
    public double RowOpacity => IsHidden ? 0.52 : 1.0;
}
