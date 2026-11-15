using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using AiPriceWatch.Core.Models;

namespace AiPriceWatch.Core.Services;

public sealed record CursorBenchEntry(
    string BaseModelName,
    string EffortTier,
    decimal Score);

public sealed record CursorBenchSyncResult(
    IReadOnlyList<ModelPricingEntry> Models,
    bool IsValidPayload,
    int UpdatedCount,
    string BenchmarkVersion,
    string? ErrorMessage = null);

public static partial class CursorBenchParser
{
    public const string SourceUrl = "https://cursor.com/cursorbench";

    private static readonly string[] EffortSuffixes =
    [
        "Extra High",
        "Minimal",
        "Medium",
        "High",
        "Low",
        "Max",
        "None"
    ];

    private static readonly Dictionary<string, string[]> CatalogIdAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-fable-5-2"] = ["Fable 5.2"],
        ["claude-opus-5-5"] = ["Opus 5.5"],
        ["claude-sonnet-5-5"] = ["Sonnet 5.5"],
        ["claude-haiku-5-5"] = ["Haiku 5.5"],
        ["deepseek-flash"] = ["DeepSeek V4.1 Flash"]
    };

    [GeneratedRegex(@"<tr[^>]*>.*?<span[^>]*class=""[^""]*truncate[^""]*""[^>]*>([^<]+)</span>.*?</td>\s*<td[^>]*>\s*(\d+(?:\.\d+)?)(?:<!--\s*-->)?\s*%</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
    private static partial Regex TableRowRegex();

    [GeneratedRegex(@"aria-label=""([^""]+?):\s*(\d+(?:\.\d+)?)%,\s*\$[\d,.]+\s+avg cost per task""", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
    private static partial Regex AriaLabelRegex();

    [GeneratedRegex(@"CursorBench\s+(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionRegex();

    public static (string Version, IReadOnlyDictionary<string, CursorBenchEntry> BestByModel) ParseLeaderboardHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return ("4.0", new Dictionary<string, CursorBenchEntry>(StringComparer.OrdinalIgnoreCase));
        }

        string version = "4.0";
        var bestByModel = new Dictionary<string, CursorBenchEntry>(StringComparer.OrdinalIgnoreCase);

        void RecordMatch(string rawLabel, string rawScore)
        {
            string decodedLabel = WebUtility.HtmlDecode(rawLabel).Trim();
            if (string.IsNullOrWhiteSpace(decodedLabel))
            {
                return;
            }

            if (!decimal.TryParse(rawScore, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal score) ||
                score < 0m || score > 100m)
            {
                return;
            }

            (string baseName, string effort) = SplitModelAndEffort(decodedLabel);
            if (!bestByModel.TryGetValue(baseName, out CursorBenchEntry? existing) || score > existing.Score)
            {
                bestByModel[baseName] = new CursorBenchEntry(baseName, effort, score);
            }
        }

        try
        {
            Match versionMatch = VersionRegex().Match(html);
            if (versionMatch.Success)
            {
                version = versionMatch.Groups[1].Value;
            }

            foreach (Match match in TableRowRegex().Matches(html))
            {
                RecordMatch(match.Groups[1].Value, match.Groups[2].Value);
            }

            if (bestByModel.Count == 0)
            {
                foreach (Match match in AriaLabelRegex().Matches(html))
                {
                    RecordMatch(match.Groups[1].Value, match.Groups[2].Value);
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Treat regex timeouts on unexpected HTML layouts as an empty parse rather than crashing.
            bestByModel.Clear();
        }

        return (version, bestByModel);
    }

    public static CursorBenchSyncResult MergeLiveScores(
        IReadOnlyList<ModelPricingEntry> existingCatalog,
        string cursorBenchHtml,
        DateTimeOffset? verifiedAt = null)
    {
        ArgumentNullException.ThrowIfNull(existingCatalog);

        if (string.IsNullOrWhiteSpace(cursorBenchHtml))
        {
            return new CursorBenchSyncResult(
                existingCatalog,
                IsValidPayload: false,
                UpdatedCount: 0,
                BenchmarkVersion: "4.0",
                ErrorMessage: "CursorBench response was empty.");
        }

        (string version, IReadOnlyDictionary<string, CursorBenchEntry> bestByModel) = ParseLeaderboardHtml(cursorBenchHtml);
        if (bestByModel.Count == 0)
        {
            return new CursorBenchSyncResult(
                existingCatalog,
                IsValidPayload: false,
                UpdatedCount: 0,
                BenchmarkVersion: version,
                ErrorMessage: "No CursorBench scores found in response.");
        }

        DateTimeOffset timestamp = verifiedAt ?? DateTimeOffset.UtcNow;
        int updatedCount = 0;
        var merged = new List<ModelPricingEntry>(existingCatalog.Count);

        foreach (ModelPricingEntry entry in existingCatalog)
        {
            CursorBenchEntry? matched = FindBestMatch(entry, bestByModel);
            if (matched is null)
            {
                merged.Add(entry);
                continue;
            }

            updatedCount++;
            string versionLabel = string.IsNullOrWhiteSpace(matched.EffortTier)
                ? version
                : $"{version} ({matched.EffortTier})";

            merged.Add(entry with
            {
                CursorBenchScore = matched.Score,
                CursorBenchSource = new BenchmarkProvenance(
                    Publisher: "Cursor",
                    BenchmarkName: "CursorBench",
                    Version: versionLabel,
                    SourceUrl: SourceUrl,
                    ScoreDate: timestamp,
                    Status: DataVerificationStatus.Verified)
            });
        }

        if (updatedCount == 0)
        {
            return new CursorBenchSyncResult(
                existingCatalog,
                IsValidPayload: false,
                UpdatedCount: 0,
                BenchmarkVersion: version,
                ErrorMessage: "No matching CursorBench models found in response.");
        }

        return new CursorBenchSyncResult(
            merged,
            IsValidPayload: true,
            UpdatedCount: updatedCount,
            BenchmarkVersion: version);
    }

    private static (string BaseName, string Effort) SplitModelAndEffort(string label)
    {
        foreach (string suffix in EffortSuffixes)
        {
            if (label.EndsWith(" " + suffix, StringComparison.OrdinalIgnoreCase))
            {
                string baseName = label[..^(suffix.Length + 1)].Trim();
                return (baseName, suffix);
            }
        }

        return (label, string.Empty);
    }

    private static CursorBenchEntry? FindBestMatch(
        ModelPricingEntry entry,
        IReadOnlyDictionary<string, CursorBenchEntry> bestByModel)
    {
        if (bestByModel.TryGetValue(entry.Name, out CursorBenchEntry? direct))
        {
            return direct;
        }

        if (CatalogIdAliases.TryGetValue(entry.Id, out string[]? aliases))
        {
            foreach (string alias in aliases)
            {
                if (bestByModel.TryGetValue(alias, out CursorBenchEntry? aliasMatch))
                {
                    return aliasMatch;
                }

                // Handle dated suffixes on CursorBench like "DeepSeek V4.1 Flash 1017"
                CursorBenchEntry? prefixMatch = bestByModel.Values
                    .Where(v => v.BaseModelName.StartsWith(alias + " ", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(v => v.Score)
                    .FirstOrDefault();

                if (prefixMatch is not null)
                {
                    return prefixMatch;
                }
            }
        }

        // Fallback: dated suffix match against catalog Name (e.g., "Model X 1017")
        return bestByModel.Values
            .Where(v => v.BaseModelName.StartsWith(entry.Name + " ", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(v => v.Score)
            .FirstOrDefault();
    }
}
