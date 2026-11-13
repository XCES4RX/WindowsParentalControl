using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using ParentalControl.Core;
using ParentalControl.Core.Data;
using ParentalControl.Core.Localization;

namespace ParentalControl.Service;

public sealed class AutoUpdateWorker : BackgroundService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    private readonly TelegramNotifier _notifier;
    private readonly Serilog.ILogger _logger;
    private static volatile bool _isUpdating;

    public AutoUpdateWorker(TelegramNotifier notifier, Serilog.ILogger logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("AutoUpdateWorker started (Current version: {Version})", AppVersionInfo.VersionString);

        // Initial delay after service start
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var settings = SettingsRepository.Get();
                if (settings.AutoUpdateEnabled && !_isUpdating)
                {
                    await CheckAndApplyUpdateInternalAsync(manual: false, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Background auto-update check failed");
            }

            try
            {
                // Check once every 1 hour
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task<(bool UpdateAvailable, string VersionTag, string Message)> CheckOnlyAsync(CancellationToken ct = default)
    {
        try
        {
            var release = await FetchLatestReleaseAsync(ct);
            if (release is null)
            {
                return (false, string.Empty, "No release found on GitHub.");
            }

            if (IsNewerVersion(release.TagName, AppVersionInfo.CurrentVersion, out var remoteVersion))
            {
                return (true, release.TagName, $"Newer release found: {release.TagName} (current: v{AppVersionInfo.VersionString})");
            }

            return (false, release.TagName, $"Current version {AppVersionInfo.VersionString} is up to date.");
        }
        catch (Exception ex)
        {
            return (false, string.Empty, ex.Message);
        }
    }

    public async Task<string> CheckAndApplyUpdateAsync(bool manual, CancellationToken ct = default)
    {
        if (_isUpdating)
        {
            return "An update is already in progress.";
        }

        return await CheckAndApplyUpdateInternalAsync(manual, ct);
    }

    private async Task<string> CheckAndApplyUpdateInternalAsync(bool manual, CancellationToken ct)
    {
        try
        {
            var release = await FetchLatestReleaseAsync(ct);
            if (release is null)
            {
                _logger.Information("No releases found on GitHub repo {Repo}", AppVersionInfo.GitHubRepo);
                return "No releases found on GitHub.";
            }

            if (!IsNewerVersion(release.TagName, AppVersionInfo.CurrentVersion, out _))
            {
                _logger.Information("Current version {Current} is latest (remote: {Remote})",
                    AppVersionInfo.VersionString, release.TagName);
                return $"Up to date ({AppVersionInfo.VersionString}).";
            }

            if (string.IsNullOrWhiteSpace(release.DownloadUrl))
            {
                _logger.Warning("Release {Tag} has no .exe installer asset", release.TagName);
                return $"Release {release.TagName} has no installer executable.";
            }

            _logger.Information("New update discovered: {Tag}. Starting silent download...", release.TagName);
            _isUpdating = true;

            var lang = SettingsRepository.GetTelegramLanguage();
            _notifier.NotifyUpdateAvailable(release.TagName, lang);

            var updateDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ParentalControl",
                "updates");
            Directory.CreateDirectory(updateDir);

            var safeTagName = release.TagName.Replace("/", "_").Replace("\\", "_");
            var destPath = Path.Combine(updateDir, $"ParentalControlSetup_{safeTagName}.exe");

            _logger.Information("Downloading update from {Url} to {Dest}", release.DownloadUrl, destPath);
            using (var response = await HttpClient.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                await using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs, ct);
            }

            _logger.Information("Download complete ({Bytes} bytes). Spawning silent installer...",
                new FileInfo(destPath).Length);

            // Execute Inno Setup silently
            // /VERYSILENT: no wizard or progress dialogs
            // /SUPPRESSMSGBOXES: suppresses message boxes
            // /NORESTART: avoids reboot
            // /SP-: skips the "This will install..." prompt
            var psi = new ProcessStartInfo
            {
                FileName = destPath,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(psi);
            _logger.Information("Silent installer process launched successfully: {Path}", destPath);

            return $"Update {release.TagName} downloaded and launched.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to check or apply update");
            return $"Update error: {ex.Message}";
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private static async Task<GitHubRelease?> FetchLatestReleaseAsync(CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{AppVersionInfo.GitHubRepo}/releases/latest";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ParentalControlAutoUpdater", AppVersionInfo.VersionString));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

        using var response = await HttpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
        var htmlUrl = root.TryGetProperty("html_url", out var htmlProp) ? htmlProp.GetString() ?? "" : "";

        string? downloadUrl = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (asset.TryGetProperty("browser_download_url", out var dl))
                    {
                        downloadUrl = dl.GetString();
                        break;
                    }
                }
            }
        }

        return new GitHubRelease(tagName, downloadUrl, htmlUrl);
    }

    private static bool IsNewerVersion(string rawTag, Version current, out Version? parsedVersion)
    {
        parsedVersion = null;
        if (string.IsNullOrWhiteSpace(rawTag)) return false;

        var cleaned = rawTag.Trim().TrimStart('v', 'V');
        if (Version.TryParse(cleaned, out var remoteVer))
        {
            parsedVersion = remoteVer;
            return remoteVer > current;
        }

        return false;
    }

    private sealed record GitHubRelease(string TagName, string? DownloadUrl, string HtmlUrl);
}
