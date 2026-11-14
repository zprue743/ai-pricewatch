using System.Text.Json;
using System.Text.Json.Serialization;
using AiPriceWatch.Core.Models;

namespace AiPriceWatch.Core.Services;

public sealed record UserPreferences
{
    public int SchemaVersion { get; init; } = 2;
    public string[] HiddenModelIds { get; init; } = Array.Empty<string>();
    public double BackgroundOpacity { get; init; } = 0.86;
    public bool IsAlwaysOnTop { get; init; } = true;
    public string SelectedProvider { get; init; } = "All";
    public TaskTier? SelectedTier { get; init; } = null;
    public PricingSortField SelectedSort { get; init; } = PricingSortField.ScoreDesc;
    public bool IsFastMode { get; init; } = false;
    public string? SelectedPresetName { get; init; } = "Code Feature";
    public int? TaskInputTokens { get; init; } = null;
    public int? TaskCachedTokens { get; init; } = null;
    public int? TaskOutputTokens { get; init; } = null;
    public bool HasDismissedOnboarding { get; init; } = false;
    public bool HasShownTrayMinimizeNotice { get; init; } = false;
    public bool StartWithWindows { get; init; } = false;
}

public sealed class UserPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    private readonly string _filePath;

    public UserPreferencesStore(string? filePath = null)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            _filePath = filePath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "AiPriceWatch");
            _filePath = Path.Combine(dir, "user-preferences.json");
        }
    }

    public UserPreferences Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new UserPreferences();
            }

            string json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new UserPreferences();
            }

            UserPreferences? loaded = JsonSerializer.Deserialize<UserPreferences>(json, JsonOptions);
            if (loaded is null)
            {
                return new UserPreferences();
            }

            return Normalize(loaded);
        }
        catch
        {
            return new UserPreferences();
        }
    }

    public void Save(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            UserPreferences normalized = Normalize(preferences) with { SchemaVersion = 2 };
            string json = JsonSerializer.Serialize(normalized, JsonOptions);

            string tempPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch
        {
            // Ignore disk write failures so UI interaction never crashes
        }
    }

    public HashSet<string> LoadHiddenModels()
    {
        return new HashSet<string>(
            Load().HiddenModelIds,
            StringComparer.OrdinalIgnoreCase);
    }

    public void SaveHiddenModels(IEnumerable<string> hiddenModelIds)
    {
        UserPreferences current = Load();
        Save(current with
        {
            HiddenModelIds = hiddenModelIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        });
    }

    private static UserPreferences Normalize(UserPreferences prefs)
    {
        double opacity = double.IsNaN(prefs.BackgroundOpacity) || double.IsInfinity(prefs.BackgroundOpacity)
            ? 0.86
            : Math.Clamp(prefs.BackgroundOpacity, 0.35, 1.0);

        PricingSortField sort = Enum.IsDefined(prefs.SelectedSort)
            ? prefs.SelectedSort
            : PricingSortField.ScoreDesc;

        TaskTier? tier = prefs.SelectedTier.HasValue && Enum.IsDefined(prefs.SelectedTier.Value)
            ? prefs.SelectedTier
            : null;

        string[] hiddenIds = (prefs.HiddenModelIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return prefs with
        {
            HiddenModelIds = hiddenIds,
            BackgroundOpacity = opacity,
            SelectedProvider = string.IsNullOrWhiteSpace(prefs.SelectedProvider) ? "All" : prefs.SelectedProvider.Trim(),
            SelectedTier = tier,
            SelectedSort = sort,
            TaskInputTokens = prefs.TaskInputTokens.HasValue ? Math.Max(0, prefs.TaskInputTokens.Value) : null,
            TaskCachedTokens = prefs.TaskCachedTokens.HasValue ? Math.Max(0, prefs.TaskCachedTokens.Value) : null,
            TaskOutputTokens = prefs.TaskOutputTokens.HasValue ? Math.Max(0, prefs.TaskOutputTokens.Value) : null
        };
    }
}
