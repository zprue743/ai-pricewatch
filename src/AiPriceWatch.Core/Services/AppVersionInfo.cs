using System.Reflection;

namespace AiPriceWatch.Core.Services;

public static class AppVersionInfo
{
    public static string GetDisplayVersion(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return FormatDisplayVersion(informationalVersion, assembly.GetName().Version);
    }

    public static string FormatDisplayVersion(string? informationalVersion, Version? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            int plusIndex = informationalVersion.IndexOf('+');
            string baseVersion = (plusIndex >= 0
                ? informationalVersion[..plusIndex]
                : informationalVersion).Trim();

            if (baseVersion.Length > 0)
            {
                return baseVersion.StartsWith('v') || baseVersion.StartsWith('V')
                    ? $"v{baseVersion[1..]}"
                    : $"v{baseVersion}";
            }
        }

        if (assemblyVersion is not null)
        {
            return $"v{assemblyVersion.ToString(3)}";
        }

        return "v0.0.0";
    }
}
