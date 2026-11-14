using Velopack;
using Velopack.Sources;

namespace AiPriceWatch.App.Services;

public sealed class AppUpdateService
{
    private const string RepoUrl = "https://github.com/zprue743/ai-pricewatch";

    private readonly UpdateManager _updateManager = new(
        new GithubSource(RepoUrl, accessToken: null, prerelease: false));

    public bool IsInstalled => _updateManager.IsInstalled;

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (!_updateManager.IsInstalled)
        {
            return null;
        }

        return await _updateManager.CheckForUpdatesAsync();
    }

    public async Task DownloadAndRestartAsync(UpdateInfo update, Action<int>? onProgress = null)
    {
        await _updateManager.DownloadUpdatesAsync(update, onProgress);
        _updateManager.ApplyUpdatesAndRestart(update);
    }
}
