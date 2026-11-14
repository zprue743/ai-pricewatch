namespace AiPriceWatch.Core.ViewModels;

public sealed record TaskPreset(
    string Name,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    string Description);
