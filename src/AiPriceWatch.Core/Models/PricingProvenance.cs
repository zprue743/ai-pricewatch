namespace AiPriceWatch.Core.Models;

public enum PriceSourceType
{
    Unknown = 0,
    DirectProvider = 1,
    OpenRouter = 2,
    Estimate = 3
}

public enum DataVerificationStatus
{
    Unknown = 0,
    Verified = 1,
    Estimated = 2,
    Unavailable = 3
}

public sealed record FieldProvenance(
    string? SourceName = null,
    string? SourceUrl = null,
    PriceSourceType SourceType = PriceSourceType.Unknown,
    DateTimeOffset? LastVerifiedAt = null,
    DataVerificationStatus Status = DataVerificationStatus.Unknown,
    string Currency = "USD",
    string Unit = "per 1M tokens")
{
    public static FieldProvenance Unknown { get; } = new();

    public static FieldProvenance Unavailable { get; } = new(Status: DataVerificationStatus.Unavailable);

    public string FormatSummary()
    {
        if (Status == DataVerificationStatus.Unavailable)
        {
            return "Unavailable (N/A)";
        }

        string sourceLabel = !string.IsNullOrWhiteSpace(SourceName)
            ? SourceName
            : SourceType switch
            {
                PriceSourceType.DirectProvider => "Direct Provider",
                PriceSourceType.OpenRouter => "OpenRouter",
                PriceSourceType.Estimate => "Estimate",
                _ => "Unknown source"
            };

        string statusLabel = Status switch
        {
            DataVerificationStatus.Verified => "Verified",
            DataVerificationStatus.Estimated => "Estimated",
            DataVerificationStatus.Unavailable => "Unavailable",
            _ => "Unverified"
        };

        string dateLabel = LastVerifiedAt.HasValue
            ? $" • {LastVerifiedAt.Value:yyyy-MM-dd}"
            : " • date unknown";

        return $"{sourceLabel} ({statusLabel}{dateLabel})";
    }
}

public sealed record BenchmarkProvenance(
    string? Publisher = null,
    string? BenchmarkName = null,
    string? Version = null,
    string? SourceUrl = null,
    DateTimeOffset? ScoreDate = null,
    DataVerificationStatus Status = DataVerificationStatus.Unknown)
{
    public static BenchmarkProvenance Unknown { get; } = new();

    public static BenchmarkProvenance Unavailable { get; } = new(Status: DataVerificationStatus.Unavailable);

    public string FormatSummary()
    {
        if (Status == DataVerificationStatus.Unavailable)
        {
            return "Benchmark score unavailable (N/A)";
        }

        string publisher = !string.IsNullOrWhiteSpace(Publisher) ? Publisher : "Unknown publisher";
        string benchmark = !string.IsNullOrWhiteSpace(BenchmarkName) ? BenchmarkName : "Benchmark";
        string version = !string.IsNullOrWhiteSpace(Version) ? $" {Version}" : string.Empty;
        string date = ScoreDate.HasValue ? $" • {ScoreDate.Value:yyyy-MM-dd}" : " • date unknown";
        string status = Status == DataVerificationStatus.Verified ? "Verified" : "Unverified";

        return $"{publisher} — {benchmark}{version} ({status}{date})";
    }
}
