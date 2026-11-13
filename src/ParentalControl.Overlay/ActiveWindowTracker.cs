using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ParentalControl.Overlay;

public static class ActiveWindowTracker
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public static (string ProcessName, string SiteOrTitle) GetActiveAppAndSite()
    {
        try
        {
            var hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return (string.Empty, string.Empty);

            GetWindowThreadProcessId(hWnd, out var processId);
            if (processId == 0) return (string.Empty, string.Empty);

            using var proc = Process.GetProcessById((int)processId);
            var procName = proc.ProcessName;

            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            var title = sb.ToString().Trim();

            var site = ExtractSiteFromTitle(procName, title);
            return (procName, site);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private static string ExtractSiteFromTitle(string procName, string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        var isBrowser = procName.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
                        procName.Equals("msedge", StringComparison.OrdinalIgnoreCase) ||
                        procName.Equals("firefox", StringComparison.OrdinalIgnoreCase) ||
                        procName.Equals("brave", StringComparison.OrdinalIgnoreCase) ||
                        procName.Equals("opera", StringComparison.OrdinalIgnoreCase);

        if (isBrowser)
        {
            if (title.Contains("YouTube", StringComparison.OrdinalIgnoreCase)) return "YouTube";
            if (title.Contains("Roblox", StringComparison.OrdinalIgnoreCase)) return "Roblox";
            if (title.Contains("Google", StringComparison.OrdinalIgnoreCase)) return "Google Search";
            if (title.Contains("Wikipedia", StringComparison.OrdinalIgnoreCase)) return "Wikipedia";
            if (title.Contains("Discord", StringComparison.OrdinalIgnoreCase)) return "Discord Web";
            if (title.Contains("Facebook", StringComparison.OrdinalIgnoreCase)) return "Facebook";
            if (title.Contains("Reddit", StringComparison.OrdinalIgnoreCase)) return "Reddit";
            if (title.Contains("Twitter", StringComparison.OrdinalIgnoreCase) || title.Contains(" X ", StringComparison.OrdinalIgnoreCase)) return "X / Twitter";
            if (title.Contains("Twitch", StringComparison.OrdinalIgnoreCase)) return "Twitch";
            if (title.Contains("GitHub", StringComparison.OrdinalIgnoreCase)) return "GitHub";
            if (title.Contains("Netflix", StringComparison.OrdinalIgnoreCase)) return "Netflix";

            var parts = title.Split(new[] { " - ", " — ", " | " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                var candidate = parts[0].Trim();
                return candidate.Length > 24 ? candidate[..24] : candidate;
            }
        }

        return string.Empty;
    }
}
