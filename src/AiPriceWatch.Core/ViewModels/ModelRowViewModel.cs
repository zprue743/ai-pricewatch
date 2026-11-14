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

        decimal fastIn = entry.FastInputPerMillion ?? entry.InputPerMillion;
        decimal fastOut = entry.FastOutputPerMillion ?? entry.OutputPerMillion;
        FastInOutDisplay = TaskCostCalculator.FormatInOutPair(fastIn, fastOut);

        ContextDisplay = TaskCostCalculator.FormatContextWindow(entry.ContextTokens);

        EstimatedTaskCostRaw = TaskCostCalculator.CalculateCost(
            entry,
            taskInputTokens,
            taskCachedTokens,
            taskOutputTokens,
            useFastMode: isFastMode);

        EstimatedTaskCostDisplay = TaskCostCalculator.FormatCost(EstimatedTaskCostRaw);
        Notes = entry.Notes;
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
    public decimal EstimatedTaskCostRaw { get; }
    public string EstimatedTaskCostDisplay { get; }
    public string Notes { get; }
    public bool IsCheapestInView { get; }
    public bool IsFastModeActive { get; }
    public bool IsHidden { get; }
    public string HideActionLabel => IsHidden ? "UNHIDE" : "HIDE";
    public string HideActionTooltip => IsHidden
        ? "Unhide this model so it appears in the HUD"
        : "Hide this model from the HUD";
    public double RowOpacity => IsHidden ? 0.52 : 1.0;
}
