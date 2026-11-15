using System.Text.Json;
using System.Text.Json.Serialization;
using AiPriceWatch.Core.Models;

namespace AiPriceWatch.Core.Services;

public sealed record PriceSnapshot(
    decimal InputPerMillion,
    decimal OutputPerMillion,
    decimal? CachedInputPerMillion);

public sealed record PriceDiff(
    decimal PreviousInputPerMillion,
    decimal PreviousOutputPerMillion,
    decimal NewInputPerMillion,
    decimal NewOutputPerMillion,
    decimal PercentChange,
    DateTimeOffset DetectedAt);

public sealed record SyncSnapshot
{
    public int SchemaVersion { get; init; } = 1;
    public DateTimeOffset? LastSyncedAt { get; init; }
    public HashSet<string> KnownOpenRouterIds { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, PriceSnapshot> LastPricesByModelId { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ModelPricingEntry> AddedModels { get; init; } = new();
    public Dictionary<string, PriceDiff> ActivePriceDiffs { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SyncSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    private readonly string _filePath;

    public SyncSnapshotStore(string? filePath = null)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            _filePath = filePath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "AiPriceWatch");
            _filePath = Path.Combine(dir, "sync-snapshot.json");
        }
    }

    public SyncSnapshot Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new SyncSnapshot();
            }

            string json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new SyncSnapshot();
            }

            SyncSnapshot? loaded = JsonSerializer.Deserialize<SyncSnapshot>(json, JsonOptions);
            return loaded is null ? new SyncSnapshot() : Normalize(loaded);
        }
        catch
        {
            return new SyncSnapshot();
        }
    }

    public void Save(SyncSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            SyncSnapshot normalized = Normalize(snapshot) with { SchemaVersion = 1 };
            string json = JsonSerializer.Serialize(normalized, JsonOptions);

            string tempPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch
        {
            // Ignore disk write failures so UI interaction never crashes.
        }
    }

    public void ClearDiffs()
    {
        SyncSnapshot current = Load();
        if (current.ActivePriceDiffs.Count == 0)
        {
            return;
        }

        Save(current with
        {
            ActivePriceDiffs = new Dictionary<string, PriceDiff>(StringComparer.OrdinalIgnoreCase)
        });
    }

    private static SyncSnapshot Normalize(SyncSnapshot snapshot)
    {
        var knownIds = snapshot.KnownOpenRouterIds is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(
                snapshot.KnownOpenRouterIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()),
                StringComparer.OrdinalIgnoreCase);

        var lastPrices = new Dictionary<string, PriceSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (snapshot.LastPricesByModelId is not null)
        {
            foreach ((string key, PriceSnapshot? value) in snapshot.LastPricesByModelId)
            {
                if (!string.IsNullOrWhiteSpace(key) && value is not null)
                {
                    lastPrices[key.Trim()] = value;
                }
            }
        }

        var addedModels = snapshot.AddedModels is null
            ? new List<ModelPricingEntry>()
            : snapshot.AddedModels
                .Where(m => m is not null && !string.IsNullOrWhiteSpace(m.Id) && !string.IsNullOrWhiteSpace(m.Name))
                .ToList();

        var activeDiffs = new Dictionary<string, PriceDiff>(StringComparer.OrdinalIgnoreCase);
        if (snapshot.ActivePriceDiffs is not null)
        {
            foreach ((string key, PriceDiff? value) in snapshot.ActivePriceDiffs)
            {
                if (!string.IsNullOrWhiteSpace(key) && value is not null)
                {
                    activeDiffs[key.Trim()] = value;
                }
            }
        }

        return snapshot with
        {
            KnownOpenRouterIds = knownIds,
            LastPricesByModelId = lastPrices,
            AddedModels = addedModels,
            ActivePriceDiffs = activeDiffs
        };
    }
}
