using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace AiPriceWatch.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _updateMenuItem;
    private bool _disposed;

    public TrayIconService(
        Action onToggleVisibility,
        Action onOpenSettings,
        Action onCheckForUpdates,
        Action onExit,
        bool updatesSupported)
    {
        ArgumentNullException.ThrowIfNull(onToggleVisibility);
        ArgumentNullException.ThrowIfNull(onOpenSettings);
        ArgumentNullException.ThrowIfNull(onCheckForUpdates);
        ArgumentNullException.ThrowIfNull(onExit);

        Icon icon = LoadAppIcon();
        var menu = new ContextMenuStrip();

        var showHideItem = new ToolStripMenuItem("Show / Hide HUD");
        showHideItem.Click += (_, _) => onToggleVisibility();

        var settingsItem = new ToolStripMenuItem("Settings & Help");
        settingsItem.Click += (_, _) => onOpenSettings();

        _updateMenuItem = new ToolStripMenuItem(updatesSupported
            ? "Check for Updates"
            : "Updates managed by Microsoft Store")
        {
            Enabled = updatesSupported
        };
        _updateMenuItem.Click += (_, _) => onCheckForUpdates();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => onExit();

        menu.Items.Add(showHideItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(_updateMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "AI PriceWatch (Ctrl+Shift+Space)",
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                onToggleVisibility();
            }
        };
    }

    public void ShowOneTimeMinimizeNotice()
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.ShowBalloonTip(
            3500,
            "AI PriceWatch is still running",
            "Closing the window hides it to the system tray. Press Ctrl+Shift+Space or click the tray icon to reopen.",
            ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            var resourceInfo = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/app-icon.ico", UriKind.Absolute));
            if (resourceInfo?.Stream is Stream stream)
            {
                using (stream)
                {
                    return new Icon(stream);
                }
            }
        }
        catch
        {
            // Fall back to default system application icon
        }

        return SystemIcons.Application;
    }
}
