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
        bool isCheapestInView)
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

        InputPriceDisplay = TaskCostCalculator.FormatPricePerMillion(entry.InputPerMillion);
        CachedPriceDisplay = TaskCostCalculator.FormatPricePerMillion(entry.CachedInputPerMillion);
        OutputPriceDisplay = TaskCostCalculator.FormatPricePerMillion(entry.OutputPerMillion);
        ContextDisplay = TaskCostCalculator.FormatContextWindow(entry.ContextTokens);

        EstimatedTaskCostRaw = TaskCostCalculator.CalculateCost(
            entry,
            taskInputTokens,
            taskCachedTokens,
            taskOutputTokens);

        EstimatedTaskCostDisplay = TaskCostCalculator.FormatCost(EstimatedTaskCostRaw);
        Notes = entry.Notes;
        IsCheapestInView = isCheapestInView;
    }

    public string Id { get; }
    public string Name { get; }
    public string Provider { get; }
    public TaskTier Tier { get; }
    public string TierCode { get; }
    public string TierBadge { get; }
    public string InputPriceDisplay { get; }
    public string CachedPriceDisplay { get; }
    public string OutputPriceDisplay { get; }
    public string ContextDisplay { get; }
    public decimal EstimatedTaskCostRaw { get; }
    public string EstimatedTaskCostDisplay { get; }
    public string Notes { get; }
    public bool IsCheapestInView { get; }
}
