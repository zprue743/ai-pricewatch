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

public sealed class HudViewModel : INotifyPropertyChanged
{
    private const double MinOpacity = 0.35;
    private const double MaxOpacity = 1.0;
    private static readonly TimeSpan SyncTimeout = TimeSpan.FromSeconds(15);

    private readonly PricingCatalogService _catalog;
    private readonly UserPreferencesStore _preferencesStore;
    private readonly HashSet<string> _hiddenModelIds;
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
    private IReadOnlyList<ModelRowViewModel> _visibleModels = Array.Empty<ModelRowViewModel>();

    public HudViewModel(PricingCatalogService catalog, UserPreferencesStore? preferencesStore = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
        _preferencesStore = preferencesStore ?? new UserPreferencesStore();
        _hiddenModelIds = _preferencesStore.LoadHiddenModels();

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

        _selectedPreset = Presets[1]; // Default to Code Feature (50K in / 8K out)
        _taskInputTokens = _selectedPreset.InputTokens;
        _taskCachedTokens = _selectedPreset.CachedInputTokens;
        _taskOutputTokens = _selectedPreset.OutputTokens;

        RefreshVisibleModels();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<string> Providers { get; }

    public IReadOnlyList<TaskPreset> Presets { get; }

    public int HiddenCount => _hiddenModelIds.Count;

    public bool HasHiddenModels => _hiddenModelIds.Count > 0;

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
            StatusText = "Syncing live prices from OpenRouter...";

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/models");
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("AiPriceWatch", "0.2"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using HttpResponseMessage response = await httpClient.SendAsync(request, timeoutCts.Token);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(timeoutCts.Token);

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

            _catalog.MergeEntries(result.Models);
            DataOrigin = CatalogDataOrigin.LiveSynced;
            SyncStatus = SyncStatus.Succeeded;
            LastSuccessfulSyncAt = DateTimeOffset.Now;
            LastSyncError = null;
            LastSyncMatchedCount = result.UpdatedCount;
            LastSyncIgnoredCount = result.IgnoredCount;
            RefreshVisibleModels();
            StatusText = $"Live sync OK • {result.UpdatedCount} matched, {result.IgnoredCount} ignored ({LastSuccessfulSyncAt.Value:HH:mm})";
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

    private string GetOriginLabel() => DataOrigin switch
    {
        CatalogDataOrigin.LiveSynced => "live data",
        CatalogDataOrigin.LastValid => $"last valid sync ({LastSuccessfulSyncAt?.ToString("HH:mm") ?? "prior"})",
        _ => "bundled data"
    };

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

            rows.Add(new ModelRowViewModel(
                entry,
                _taskInputTokens,
                _taskCachedTokens,
                _taskOutputTokens,
                isCheapest,
                _isFastMode,
                isHidden));
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
