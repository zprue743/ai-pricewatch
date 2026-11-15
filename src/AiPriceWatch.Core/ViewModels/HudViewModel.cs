using System.ComponentModel;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using AiPriceWatch.Core.Models;
using AiPriceWatch.Core.Services;

namespace AiPriceWatch.Core.ViewModels;

public enum CatalogDataOrigin
{
    Bundled = 0,
    LastValid = 1,
    LiveSynced = 2
}

public enum SyncStatus
{
    Idle = 0,
    Syncing = 1,
    Succeeded = 2,
    Failed = 3
}

public sealed record OpenRouterCandidateViewModel(
    string FullId,
    string Name,
    string Provider,
    string InOutDisplay,
    string ContextDisplay,
    bool IsNew,
    bool IsAdded,
    bool CanAdd)
{
    public string ActionLabel => IsAdded ? "REMOVE" : "ADD";
}

public sealed class HudViewModel : INotifyPropertyChanged
{
    private const double MinOpacity = 0.35;
    private const double MaxOpacity = 1.0;
    private static readonly TimeSpan SyncTimeout = TimeSpan.FromSeconds(15);

    private readonly PricingCatalogService _catalog;
    private readonly UserPreferencesStore _preferencesStore;
    private readonly SyncSnapshotStore _snapshotStore;
    private readonly HashSet<string> _bundledModelKeys;
    private readonly HashSet<string> _hiddenModelIds;
    private readonly HashSet<string> _addedModelIds = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, PriceDiff> _activePriceDiffs = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, PriceSnapshot> _lastPriceSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _knownOpenRouterIds = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _newOpenRouterIds = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<OpenRouterModelCandidate> _availableOpenRouterModels = Array.Empty<OpenRouterModelCandidate>();
    private bool _isAddModelsOpen;
    private string _addModelsSearchText = string.Empty;
    private bool _onlyNewCandidates;
    private DateTimeOffset? _snapshotLastSyncedAt;
    private string _selectedProvider = "All";
    private TaskTier? _selectedTier;
    private string _searchText = string.Empty;
    private PricingSortField _selectedSort = PricingSortField.ScoreDesc;
    private TaskPreset _selectedPreset;
    private int _taskInputTokens;
    private int _taskCachedTokens;
    private int _taskOutputTokens;
    private bool _isFastMode;
    private bool _showHiddenModels;
    private double _backgroundOpacity = 0.86;
    private bool _isAlwaysOnTop = true;
    private string _statusText = "Bundled catalog ready";
    private CatalogDataOrigin _dataOrigin = CatalogDataOrigin.Bundled;
    private SyncStatus _syncStatus = SyncStatus.Idle;
    private DateTimeOffset? _lastSuccessfulSyncAt;
    private string? _lastSyncError;
    private int _lastSyncMatchedCount;
    private int _lastSyncIgnoredCount;
    private bool _hasDismissedOnboarding;
    private bool _hasShownTrayMinimizeNotice;
    private bool _startWithWindows;
    private IReadOnlyList<ModelRowViewModel> _visibleModels = Array.Empty<ModelRowViewModel>();

    public HudViewModel(
        PricingCatalogService catalog,
        UserPreferencesStore? preferencesStore = null,
        SyncSnapshotStore? snapshotStore = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
        _preferencesStore = preferencesStore ?? new UserPreferencesStore();
        _snapshotStore = snapshotStore ?? new SyncSnapshotStore();
        UserPreferences prefs = _preferencesStore.Load();
        _hiddenModelIds = new HashSet<string>(prefs.HiddenModelIds, StringComparer.OrdinalIgnoreCase);

        _bundledModelKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ModelPricingEntry entry in _catalog.AllModels)
        {
            _bundledModelKeys.Add(entry.Id);
            if (!string.IsNullOrWhiteSpace(entry.OpenRouterId))
            {
                _bundledModelKeys.Add(entry.OpenRouterId);
            }
        }

        LoadSnapshotIntoCatalog();

        var providerList = new List<string> { "All" };
        providerList.AddRange(_catalog.GetProviders());
        Providers = providerList;

        Presets =
        [
            new TaskPreset("Quick Prompt", 4_000, 0, 1_000, "4K in / 1K out"),
            new TaskPreset("Code Feature", 50_000, 0, 8_000, "50K in / 8K out"),
            new TaskPreset("Cached Agent Loop", 20_000, 100_000, 4_000, "20K in / 100K cached / 4K out"),
            new TaskPreset("Repo Audit", 250_000, 0, 16_000, "250K in / 16K out")
        ];

        _selectedProvider = providerList.Contains(prefs.SelectedProvider, StringComparer.OrdinalIgnoreCase)
            ? providerList.First(p => string.Equals(p, prefs.SelectedProvider, StringComparison.OrdinalIgnoreCase))
            : "All";
        _selectedTier = prefs.SelectedTier;
        _selectedSort = prefs.SelectedSort;
        _isFastMode = prefs.IsFastMode;
        _backgroundOpacity = Math.Clamp(prefs.BackgroundOpacity, MinOpacity, MaxOpacity);
        _isAlwaysOnTop = prefs.IsAlwaysOnTop;
        _hasDismissedOnboarding = prefs.HasDismissedOnboarding;
        _hasShownTrayMinimizeNotice = prefs.HasShownTrayMinimizeNotice;
        _startWithWindows = prefs.StartWithWindows;

        _selectedPreset = Presets.FirstOrDefault(p =>
            string.Equals(p.Name, prefs.SelectedPresetName, StringComparison.OrdinalIgnoreCase))
            ?? Presets[1]; // Default to Code Feature (50K in / 8K out)

        _taskInputTokens = prefs.TaskInputTokens ?? _selectedPreset.InputTokens;
        _taskCachedTokens = prefs.TaskCachedTokens ?? _selectedPreset.CachedInputTokens;
        _taskOutputTokens = prefs.TaskOutputTokens ?? _selectedPreset.OutputTokens;

        RefreshVisibleModels();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<string> Providers { get; }

    public IReadOnlyList<TaskPreset> Presets { get; }

    public int HiddenCount => _hiddenModelIds.Count;

    public bool HasHiddenModels => _hiddenModelIds.Count > 0;

    public int NewOpenRouterCount => _newOpenRouterIds.Count;

    public bool HasNewOpenRouterModels => _newOpenRouterIds.Count > 0;

    public bool HasPriceDiffs => _activePriceDiffs.Count > 0;

    public bool IsAddModelsOpen
    {
        get => _isAddModelsOpen;
        set
        {
            if (_isAddModelsOpen != value)
            {
                _isAddModelsOpen = value;
                OnPropertyChanged();
            }
        }
    }

    public string AddModelsSearchText
    {
        get => _addModelsSearchText;
        set
        {
            string next = value ?? string.Empty;
            if (!string.Equals(_addModelsSearchText, next, StringComparison.Ordinal))
            {
                _addModelsSearchText = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AvailableCandidates));
            }
        }
    }

    public bool OnlyNewCandidates
    {
        get => _onlyNewCandidates;
        set
        {
            if (_onlyNewCandidates != value)
            {
                _onlyNewCandidates = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AvailableCandidates));
            }
        }
    }

    public IReadOnlyList<OpenRouterCandidateViewModel> AvailableCandidates
    {
        get
        {
            if (_availableOpenRouterModels.Count == 0)
            {
                return Array.Empty<OpenRouterCandidateViewModel>();
            }

            string search = _addModelsSearchText.Trim();
            IEnumerable<OpenRouterModelCandidate> query = _availableOpenRouterModels;
            if (_onlyNewCandidates)
            {
                query = query.Where(c => _newOpenRouterIds.Contains(c.FullId));
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c =>
                    c.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    c.FullId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    c.Provider.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            return query
                .Take(100)
                .Select(c =>
                {
                    bool isBundled = _bundledModelKeys.Contains(c.FullId);
                    bool isAdded = _addedModelIds.Contains(c.FullId);
                    return new OpenRouterCandidateViewModel(
                        FullId: c.FullId,
                        Name: c.Name,
                        Provider: c.Provider,
                        InOutDisplay: TaskCostCalculator.FormatInOutPair(c.InputPerMillion, c.OutputPerMillion),
                        ContextDisplay: c.ContextTokens > 0 ? TaskCostCalculator.FormatContextWindow(c.ContextTokens) : "—",
                        IsNew: _newOpenRouterIds.Contains(c.FullId),
                        IsAdded: isAdded,
                        CanAdd: !isBundled);
                })
                .ToList();
        }
    }

    public string HiddenToggleLabel => _showHiddenModels
        ? $"HIDE ({_hiddenModelIds.Count})"
        : $"HIDDEN ({_hiddenModelIds.Count})";

    public bool ShowHiddenModels
    {
        get => _showHiddenModels;
        set
        {
            if (_showHiddenModels != value)
            {
                _showHiddenModels = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HiddenToggleLabel));
                RefreshVisibleModels();
            }
        }
    }

    public string SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? "All" : value.Trim();
            if (_selectedProvider != normalized)
            {
                _selectedProvider = normalized;
                OnPropertyChanged();
                SavePreferences();
                RefreshVisibleModels();
            }
        }
    }

    public TaskTier? SelectedTier
    {
        get => _selectedTier;
        set
        {
            if (_selectedTier != value)
            {
                _selectedTier = value;
                OnPropertyChanged();
                SavePreferences();
                RefreshVisibleModels();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            string normalized = value ?? string.Empty;
            if (_searchText != normalized)
            {
                _searchText = normalized;
                OnPropertyChanged();
                RefreshVisibleModels();
            }
        }
    }

    public PricingSortField SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (_selectedSort != value)
            {
                _selectedSort = value;
                OnPropertyChanged();
                SavePreferences();
                RefreshVisibleModels();
            }
        }
    }

    public TaskPreset SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (value is not null && _selectedPreset != value)
            {
                _selectedPreset = value;
                _taskInputTokens = value.InputTokens;
                _taskCachedTokens = value.CachedInputTokens;
                _taskOutputTokens = value.OutputTokens;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TaskInputTokens));
                OnPropertyChanged(nameof(TaskCachedTokens));
                OnPropertyChanged(nameof(TaskOutputTokens));
                SavePreferences();
                RefreshVisibleModels();
            }
        }
    }

    public int TaskInputTokens
    {
        get => _taskInputTokens;
        set
        {
            int clamped = Math.Max(0, value);
            if (_taskInputTokens != clamped)
            {
                _taskInputTokens = clamped;
                OnPropertyChanged();
                SavePreferences();
                RefreshVisibleModels();
            }
        }
    }

    public int TaskCachedTokens
    {
        get => _taskCachedTokens;
        set
        {
            int clamped = Math.Max(0, value);
            if (_taskCachedTokens != clamped)
            {
                _taskCachedTokens = clamped;
                OnPropertyChanged();
                SavePreferences();
                RefreshVisibleModels();
            }
        }
    }

    public int TaskOutputTokens
    {
        get => _taskOutputTokens;
        set
        {
            int clamped = Math.Max(0, value);
            if (_taskOutputTokens != clamped)
            {
                _taskOutputTokens = clamped;
                OnPropertyChanged();
                SavePreferences();
                RefreshVisibleModels();
            }
        }
    }

    public bool IsFastMode
    {
        get => _isFastMode;
        set
        {
            if (_isFastMode != value)
            {
                _isFastMode = value;
                OnPropertyChanged();
                SavePreferences();
                RefreshVisibleModels();
            }
        }
    }

    public double BackgroundOpacity
    {
        get => _backgroundOpacity;
        set
        {
            double clamped = Math.Clamp(value, MinOpacity, MaxOpacity);
            if (Math.Abs(_backgroundOpacity - clamped) > 0.0001)
            {
                _backgroundOpacity = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OpacityPercentageDisplay));
                SavePreferences();
            }
        }
    }

    public string OpacityPercentageDisplay => $"{(int)Math.Round(_backgroundOpacity * 100)}%";

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set
        {
            if (_isAlwaysOnTop != value)
            {
                _isAlwaysOnTop = value;
                OnPropertyChanged();
                SavePreferences();
            }
        }
    }

    public bool HasDismissedOnboarding
    {
        get => _hasDismissedOnboarding;
        set
        {
            if (_hasDismissedOnboarding != value)
            {
                _hasDismissedOnboarding = value;
                OnPropertyChanged();
                SavePreferences();
            }
        }
    }

    public bool HasShownTrayMinimizeNotice
    {
        get => _hasShownTrayMinimizeNotice;
        set
        {
            if (_hasShownTrayMinimizeNotice != value)
            {
                _hasShownTrayMinimizeNotice = value;
                OnPropertyChanged();
                SavePreferences();
            }
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (_startWithWindows != value)
            {
                _startWithWindows = value;
                OnPropertyChanged();
                SavePreferences();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    public CatalogDataOrigin DataOrigin
    {
        get => _dataOrigin;
        private set
        {
            if (_dataOrigin != value)
            {
                _dataOrigin = value;
                OnPropertyChanged();
            }
        }
    }

    public SyncStatus SyncStatus
    {
        get => _syncStatus;
        private set
        {
            if (_syncStatus != value)
            {
                _syncStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSyncing));
                OnPropertyChanged(nameof(CanRetrySync));
            }
        }
    }

    public bool IsSyncing => _syncStatus == SyncStatus.Syncing;

    public bool CanRetrySync => _syncStatus == SyncStatus.Failed;

    public DateTimeOffset? LastSuccessfulSyncAt
    {
        get => _lastSuccessfulSyncAt;
        private set
        {
            if (_lastSuccessfulSyncAt != value)
            {
                _lastSuccessfulSyncAt = value;
                OnPropertyChanged();
            }
        }
    }

    public string? LastSyncError
    {
        get => _lastSyncError;
        private set
        {
            if (_lastSyncError != value)
            {
                _lastSyncError = value;
                OnPropertyChanged();
            }
        }
    }

    public int LastSyncMatchedCount
    {
        get => _lastSyncMatchedCount;
        private set
        {
            if (_lastSyncMatchedCount != value)
            {
                _lastSyncMatchedCount = value;
                OnPropertyChanged();
            }
        }
    }

    public int LastSyncIgnoredCount
    {
        get => _lastSyncIgnoredCount;
        private set
        {
            if (_lastSyncIgnoredCount != value)
            {
                _lastSyncIgnoredCount = value;
                OnPropertyChanged();
            }
        }
    }

    public IReadOnlyList<ModelRowViewModel> VisibleModels
    {
        get => _visibleModels;
        private set
        {
            _visibleModels = value;
            OnPropertyChanged();
        }
    }

    public void ToggleModelHidden(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return;
        }

        if (!_hiddenModelIds.Remove(modelId))
        {
            _hiddenModelIds.Add(modelId);
        }

        _preferencesStore.SaveHiddenModels(_hiddenModelIds);
        NotifyHiddenChanged();
        RefreshVisibleModels();
    }

    public void UnhideAllModels()
    {
        if (_hiddenModelIds.Count == 0)
        {
            return;
        }

        _hiddenModelIds.Clear();
        _showHiddenModels = false;
        _preferencesStore.SaveHiddenModels(_hiddenModelIds);
        OnPropertyChanged(nameof(ShowHiddenModels));
        NotifyHiddenChanged();
        RefreshVisibleModels();
    }

    public async Task SyncLivePricesAsync(HttpClient httpClient, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        if (_syncStatus == SyncStatus.Syncing)
        {
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(SyncTimeout);

        try
        {
            SyncStatus = SyncStatus.Syncing;
            LastSyncError = null;
            StatusText = "Syncing live prices & CursorBench scores...";

            Task<string> openRouterTask = FetchTextAsync(
                httpClient,
                "https://openrouter.ai/api/v1/models",
                "application/json",
                timeoutCts.Token);

            Task<string?> cursorBenchTask = TryFetchTextAsync(
                httpClient,
                CursorBenchParser.SourceUrl,
                "text/html",
                timeoutCts.Token);

            IReadOnlyList<ModelPricingEntry> previousCatalogSnapshot = _catalog.AllModels.ToList();
            string json = await openRouterTask;

            OpenRouterSyncResult result = OpenRouterPriceParser.MergeLivePricesDetailed(_catalog.AllModels, json);
            if (!result.IsValidPayload)
            {
                if (DataOrigin == CatalogDataOrigin.LiveSynced)
                {
                    DataOrigin = CatalogDataOrigin.LastValid;
                }

                SyncStatus = SyncStatus.Failed;
                LastSyncError = result.ErrorMessage ?? "Invalid OpenRouter response.";
                LastSyncMatchedCount = 0;
                LastSyncIgnoredCount = result.IgnoredCount;
                StatusText = $"Live sync failed: {LastSyncError} • keeping {GetOriginLabel()}";
                return;
            }

            IReadOnlyList<ModelPricingEntry> mergedModels = result.Models;
            string? cursorBenchHtml = await cursorBenchTask;
            CursorBenchSyncResult? cbResult = null;
            if (cursorBenchHtml is not null)
            {
                try
                {
                    cbResult = CursorBenchParser.MergeLiveScores(mergedModels, cursorBenchHtml);
                }
                catch
                {
                    cbResult = null;
                }
            }

            if (cbResult is { IsValidPayload: true })
            {
                mergedModels = cbResult.Models;
            }

            DateTimeOffset syncedNow = DateTimeOffset.Now;
            CatalogDiffResult diffResult = CatalogDiffCalculator.ComputeDiffs(
                previousCatalogSnapshot,
                mergedModels,
                result.AvailableModels,
                _lastPriceSnapshots,
                _knownOpenRouterIds,
                syncedNow);

            _catalog.MergeEntries(mergedModels);
            _availableOpenRouterModels = result.AvailableModels;
            _knownOpenRouterIds = diffResult.UpdatedKnownOpenRouterIds;
            _newOpenRouterIds = diffResult.NewOpenRouterIds;
            _lastPriceSnapshots = diffResult.UpdatedPriceSnapshots;
            _activePriceDiffs = diffResult.PriceDiffs;
            _snapshotLastSyncedAt = syncedNow;
            SaveSnapshot();

            DataOrigin = CatalogDataOrigin.LiveSynced;
            SyncStatus = SyncStatus.Succeeded;
            LastSuccessfulSyncAt = syncedNow;
            LastSyncError = null;
            LastSyncMatchedCount = result.UpdatedCount;
            LastSyncIgnoredCount = result.IgnoredCount;
            NotifyDiffAndCandidateChanges();
            RefreshVisibleModels();

            string cbSuffix = cbResult is { IsValidPayload: true }
                ? $", {cbResult.UpdatedCount} CursorBench"
                : " • CursorBench unchanged";
            string diffSuffix = _activePriceDiffs.Count > 0 ? $" • {_activePriceDiffs.Count} changed" : string.Empty;
            string newSuffix = _newOpenRouterIds.Count > 0 ? $" • {_newOpenRouterIds.Count} new" : string.Empty;
            StatusText = $"Live sync OK • {result.UpdatedCount} prices{cbSuffix}{diffSuffix}{newSuffix} ({LastSuccessfulSyncAt.Value:HH:mm})";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            SyncStatus = SyncStatus.Failed;
            LastSyncError = "Sync cancelled.";
            StatusText = $"Live sync cancelled • keeping {GetOriginLabel()}";
        }
        catch (OperationCanceledException)
        {
            if (DataOrigin == CatalogDataOrigin.LiveSynced)
            {
                DataOrigin = CatalogDataOrigin.LastValid;
            }

            SyncStatus = SyncStatus.Failed;
            LastSyncError = "Request timed out.";
            StatusText = $"Live sync failed (timeout) • keeping {GetOriginLabel()}";
        }
        catch (Exception ex)
        {
            if (DataOrigin == CatalogDataOrigin.LiveSynced)
            {
                DataOrigin = CatalogDataOrigin.LastValid;
            }

            SyncStatus = SyncStatus.Failed;
            LastSyncError = ex.Message;
            StatusText = $"Live sync failed ({ex.GetType().Name}) • keeping {GetOriginLabel()}";
        }
    }

    private static async Task<string> FetchTextAsync(
        HttpClient httpClient,
        string url,
        string acceptMediaType,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("AiPriceWatch", "0.2"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptMediaType));

        using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static async Task<string?> TryFetchTextAsync(
        HttpClient httpClient,
        string url,
        string acceptMediaType,
        CancellationToken ct)
    {
        try
        {
            return await FetchTextAsync(httpClient, url, acceptMediaType, ct);
        }
        catch
        {
            return null;
        }
    }

    private string GetOriginLabel() => DataOrigin switch
    {
        CatalogDataOrigin.LiveSynced => "live data",
        CatalogDataOrigin.LastValid => $"last valid sync ({LastSuccessfulSyncAt?.ToString("HH:mm") ?? "prior"})",
        _ => "bundled data"
    };

    public bool AddOpenRouterModel(string fullId)
    {
        if (string.IsNullOrWhiteSpace(fullId))
        {
            return false;
        }

        string trimmedId = fullId.Trim();
        if (_bundledModelKeys.Contains(trimmedId) || _addedModelIds.Contains(trimmedId))
        {
            return false;
        }

        OpenRouterModelCandidate? candidate = _availableOpenRouterModels
            .FirstOrDefault(c => string.Equals(c.FullId, trimmedId, StringComparison.OrdinalIgnoreCase));
        if (candidate is null)
        {
            return false;
        }

        var provenance = new FieldProvenance(
            SourceName: "OpenRouter API",
            SourceUrl: "https://openrouter.ai/api/v1/models",
            SourceType: PriceSourceType.OpenRouter,
            LastVerifiedAt: _snapshotLastSyncedAt ?? DateTimeOffset.UtcNow,
            Status: DataVerificationStatus.Verified);

        var entry = new ModelPricingEntry(
            Id: candidate.FullId,
            Name: candidate.Name,
            Provider: candidate.Provider,
            Tier: TaskTier.Balanced,
            InputPerMillion: candidate.InputPerMillion,
            CachedInputPerMillion: candidate.CachedInputPerMillion,
            OutputPerMillion: candidate.OutputPerMillion,
            ContextTokens: candidate.ContextTokens,
            Notes: "Added from OpenRouter live feed.",
            FastInputPerMillion: null,
            FastOutputPerMillion: null,
            IntelligenceScore: null,
            OpenRouterId: candidate.FullId,
            PricingSource: provenance,
            CachedPricingSource: candidate.CachedInputPerMillion.HasValue ? provenance : null);

        var updated = _catalog.AllModels.ToList();
        updated.Add(entry);
        _catalog.MergeEntries(updated);
        _addedModelIds.Add(entry.Id);
        _lastPriceSnapshots[entry.Id] = new PriceSnapshot(
            entry.InputPerMillion,
            entry.OutputPerMillion,
            entry.CachedInputPerMillion);

        SaveSnapshot();
        NotifyDiffAndCandidateChanges();
        RefreshVisibleModels();
        return true;
    }

    public bool RemoveAddedModel(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        string trimmedId = modelId.Trim();
        if (!_addedModelIds.Remove(trimmedId))
        {
            return false;
        }

        var remaining = _catalog.AllModels
            .Where(m => !string.Equals(m.Id, trimmedId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _catalog.MergeEntries(remaining);
        _activePriceDiffs.Remove(trimmedId);
        _lastPriceSnapshots.Remove(trimmedId);

        SaveSnapshot();
        NotifyDiffAndCandidateChanges();
        RefreshVisibleModels();
        return true;
    }

    public void ClearPriceDiffs()
    {
        if (_activePriceDiffs.Count == 0)
        {
            return;
        }

        _activePriceDiffs.Clear();
        _snapshotStore.ClearDiffs();
        OnPropertyChanged(nameof(HasPriceDiffs));
        RefreshVisibleModels();
    }

    private void LoadSnapshotIntoCatalog()
    {
        SyncSnapshot snapshot = _snapshotStore.Load();
        _snapshotLastSyncedAt = snapshot.LastSyncedAt;
        _knownOpenRouterIds = snapshot.KnownOpenRouterIds;
        _lastPriceSnapshots = snapshot.LastPricesByModelId;
        _activePriceDiffs = snapshot.ActivePriceDiffs;

        if (snapshot.AddedModels.Count > 0)
        {
            var merged = _catalog.AllModels.ToList();
            foreach (ModelPricingEntry added in snapshot.AddedModels)
            {
                if (_bundledModelKeys.Contains(added.Id) ||
                    (!string.IsNullOrWhiteSpace(added.OpenRouterId) && _bundledModelKeys.Contains(added.OpenRouterId)))
                {
                    continue;
                }

                if (_addedModelIds.Add(added.Id))
                {
                    merged.Add(added);
                }
            }

            _catalog.MergeEntries(merged);
        }
    }

    private void SaveSnapshot()
    {
        List<ModelPricingEntry> addedModels = _catalog.AllModels
            .Where(m => _addedModelIds.Contains(m.Id))
            .ToList();

        _snapshotStore.Save(new SyncSnapshot
        {
            SchemaVersion = 1,
            LastSyncedAt = _snapshotLastSyncedAt,
            KnownOpenRouterIds = _knownOpenRouterIds,
            LastPricesByModelId = _lastPriceSnapshots,
            AddedModels = addedModels,
            ActivePriceDiffs = _activePriceDiffs
        });
    }

    private void NotifyDiffAndCandidateChanges()
    {
        OnPropertyChanged(nameof(NewOpenRouterCount));
        OnPropertyChanged(nameof(HasNewOpenRouterModels));
        OnPropertyChanged(nameof(HasPriceDiffs));
        OnPropertyChanged(nameof(AvailableCandidates));
    }

    private void SavePreferences()
    {
        _preferencesStore.Save(new UserPreferences
        {
            SchemaVersion = 2,
            HiddenModelIds = _hiddenModelIds
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            BackgroundOpacity = _backgroundOpacity,
            IsAlwaysOnTop = _isAlwaysOnTop,
            SelectedProvider = _selectedProvider,
            SelectedTier = _selectedTier,
            SelectedSort = _selectedSort,
            IsFastMode = _isFastMode,
            SelectedPresetName = _selectedPreset.Name,
            TaskInputTokens = _taskInputTokens,
            TaskCachedTokens = _taskCachedTokens,
            TaskOutputTokens = _taskOutputTokens,
            HasDismissedOnboarding = _hasDismissedOnboarding,
            HasShownTrayMinimizeNotice = _hasShownTrayMinimizeNotice,
            StartWithWindows = _startWithWindows
        });
    }

    private void NotifyHiddenChanged()
    {
        OnPropertyChanged(nameof(HiddenCount));
        OnPropertyChanged(nameof(HasHiddenModels));
        OnPropertyChanged(nameof(HiddenToggleLabel));
    }

    private void RefreshVisibleModels()
    {
        IReadOnlyList<ModelPricingEntry> entries = _catalog.Query(
            provider: _selectedProvider,
            tier: _selectedTier,
            searchText: _searchText,
            sort: _selectedSort,
            taskInputTokens: _taskInputTokens,
            taskCachedTokens: _taskCachedTokens,
            taskOutputTokens: _taskOutputTokens,
            useFastMode: _isFastMode);

        List<ModelPricingEntry> filtered = _showHiddenModels
            ? entries.ToList()
            : entries.Where(e => !_hiddenModelIds.Contains(e.Id)).ToList();

        if (filtered.Count == 0)
        {
            VisibleModels = Array.Empty<ModelRowViewModel>();
            StatusText = _hiddenModelIds.Count > 0 && !_showHiddenModels
                ? $"0 visible ({_hiddenModelIds.Count} hidden)"
                : "0 models match current filters";
            return;
        }

        IReadOnlyList<ModelPricingEntry> nonHiddenInView = filtered
            .Where(e => !_hiddenModelIds.Contains(e.Id))
            .ToList();

        List<decimal> comparableCosts = nonHiddenInView
            .Select(e => TaskCostCalculator.CalculateCostOrNull(e, _taskInputTokens, _taskCachedTokens, _taskOutputTokens, _isFastMode))
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .ToList();

        decimal? minTaskCost = comparableCosts.Count > 0 ? comparableCosts.Min() : null;

        bool cheapestAssigned = false;
        var rows = new List<ModelRowViewModel>(filtered.Count);
        foreach (ModelPricingEntry entry in filtered)
        {
            bool isHidden = _hiddenModelIds.Contains(entry.Id);
            decimal? cost = TaskCostCalculator.CalculateCostOrNull(entry, _taskInputTokens, _taskCachedTokens, _taskOutputTokens, _isFastMode);
            bool isCheapest = !isHidden && !cheapestAssigned && minTaskCost.HasValue && cost.HasValue && cost.Value == minTaskCost.Value;
            if (isCheapest)
            {
                cheapestAssigned = true;
            }

            _activePriceDiffs.TryGetValue(entry.Id, out PriceDiff? diff);
            bool isUserAdded = _addedModelIds.Contains(entry.Id);

            rows.Add(new ModelRowViewModel(
                entry,
                _taskInputTokens,
                _taskCachedTokens,
                _taskOutputTokens,
                isCheapest,
                _isFastMode,
                isHidden,
                diff,
                isUserAdded));
        }

        VisibleModels = rows;
        string modeLabel = _isFastMode ? "FAST MODE" : "STD MODE";
        string hiddenSuffix = _hiddenModelIds.Count > 0 ? $" • {_hiddenModelIds.Count} hidden" : string.Empty;
        string syncSuffix = _syncStatus == SyncStatus.Failed ? " • Sync failed (offline/stale)" : string.Empty;
        StatusText = $"{rows.Count} models • {_selectedProvider} • {modeLabel}{hiddenSuffix}{syncSuffix}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
