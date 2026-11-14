using System.Text.Json;

namespace AiPriceWatch.Core.Services;

public sealed class UserPreferencesStore
{
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

    public HashSet<string> LoadHiddenModels()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            string json = File.ReadAllText(_filePath);
            UserPreferencesDto? dto = JsonSerializer.Deserialize<UserPreferencesDto>(json);
            return new HashSet<string>(
                dto?.HiddenModelIds ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void SaveHiddenModels(IEnumerable<string> hiddenModelIds)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var dto = new UserPreferencesDto
            {
                HiddenModelIds = hiddenModelIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };

            string json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Ignore disk write failures so UI interaction never crashes
        }
    }

    private sealed class UserPreferencesDto
    {
        public string[] HiddenModelIds { get; set; } = Array.Empty<string>();
    }
}
