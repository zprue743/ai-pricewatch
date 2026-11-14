using System.Runtime.InteropServices;

namespace AiPriceWatch.App.Services;

public static class AppEnvironment
{
    private const int AppModelErrorNoPackage = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

    public static bool IsPackagedStoreApp()
    {
        try
        {
            int length = 0;
            int result = GetCurrentPackageFullName(ref length, null);
            return result != AppModelErrorNoPackage && length > 0;
        }
        catch
        {
            return false;
        }
    }
}
