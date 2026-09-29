using System.Runtime.Versioning;
using Microsoft.Win32;

namespace SQLModelViewer.App.Services;

/// <summary>
/// Checks for the WebView2 Evergreen Runtime the way Microsoft documents for native apps that
/// don't want a hard dependency on the WebView2 SDK: look for its registered version under the
/// well-known EdgeUpdate client GUID, in each of the three locations the installer can use
/// (machine-wide 64-bit, machine-wide 32-bit-on-64-bit, or per-user). Avalonia.Controls.WebView's
/// Windows backend doesn't expose a managed detection API itself, so this is the pragmatic
/// alternative rather than pulling in the full WebView2 SDK just for a presence check.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WebView2AvailabilityChecker : IWebViewAvailabilityChecker
{
    private const string ClientId = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

    private static readonly string[] RegistryPaths =
    [
        $@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{ClientId}",
        $@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{ClientId}",
    ];

    public bool IsAvailable()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            foreach (var path in RegistryPaths)
            {
                if (HasVersion(Registry.LocalMachine, path) || HasVersion(Registry.CurrentUser, path))
                    return true;
            }
        }
        catch (Exception)
        {
            // Any registry access failure — treat as "not available" and fall back gracefully
            // rather than let a detection quirk crash the app.
            return false;
        }

        return false;
    }

    private static bool HasVersion(RegistryKey root, string path)
    {
        using var key = root.OpenSubKey(path);
        var version = key?.GetValue("pv") as string;
        return !string.IsNullOrWhiteSpace(version) && version != "0.0.0.0";
    }
}
