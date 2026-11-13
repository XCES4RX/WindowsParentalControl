using System.Diagnostics;
using ParentalControl.Core.Data;

namespace ParentalControl.Core.Security;

public static class DnsProtectionManager
{
    private const string SettingKey = "FamilySafeDnsEnabled";

    public static bool IsEnabled()
    {
        var val = SettingsRepository.GetCustomSetting(SettingKey);
        return string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static bool SetFamilyDns(bool enable)
    {
        try
        {
            var psScript = enable
                ? "Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.Virtual -ne $true } | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses ('1.1.1.3', '1.0.0.3') -ErrorAction SilentlyContinue }"
                : "Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.Virtual -ne $true } | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ResetServerAddresses -ErrorAction SilentlyContinue }";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{psScript}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(7000);

            SettingsRepository.SetCustomSetting(SettingKey, enable ? "true" : "false");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
