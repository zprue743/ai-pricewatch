using System.Diagnostics;
using Microsoft.Win32;

namespace AiPriceWatch.App.Services;

public static class StartupService
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppValueName = "AiPriceWatch";

    public static bool IsPackaged => AppEnvironment.IsPackagedStoreApp();

    public static bool ApplyStartWithWindows(bool enabled, out string? statusNote)
    {
        statusNote = null;
        if (IsPackaged)
        {
            statusNote = "For Microsoft Store installs, manage startup in Windows Settings > Apps > Startup.";
            return false;
        }

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                statusNote = "Unable to access Windows startup registry key.";
                return false;
            }

            if (enabled)
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    statusNote = "Unable to resolve application executable path.";
                    return false;
                }

                key.SetValue(AppValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception ex)
        {
            statusNote = $"Could not update startup setting: {ex.Message}";
            return false;
        }
    }

    public static void OpenWindowsStartupSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true });
        }
        catch
        {
            // Ignore if settings URI cannot be launched
        }
    }
}
