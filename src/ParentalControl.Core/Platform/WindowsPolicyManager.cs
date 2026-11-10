using Microsoft.Win32;
using ParentalControl.Core.Data;
using ParentalControl.Core.Models;

namespace ParentalControl.Core.Platform;

/// <summary>
/// Applies and removes per-user Windows policy restrictions under HKEY_USERS\{SID}.
/// </summary>
public static class WindowsPolicyManager
{
    private const string ExplorerPoliciesPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
    private const string SystemPoliciesPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string WindowsSystemPoliciesPath = @"Software\Policies\Microsoft\Windows\System";

    /// <summary>
    /// Loads the stored policy configuration for <paramref name="userId"/> and applies it to <paramref name="userSid"/>.
    /// </summary>
    public static bool ApplyUserPolicies(string userSid, int userId)
    {
        var policy = UserPolicyRepository.GetByUserId(userId);
        return ApplyPolicies(userSid, policy);
    }

    /// <summary>
    /// Applies the specified policy configuration to the user's loaded registry hive.
    /// Returns true if the hive was mounted and updated, or false if the user is not currently signed in.
    /// </summary>
    public static bool ApplyPolicies(string userSid, UserPolicyConfig? config)
    {
        if (string.IsNullOrWhiteSpace(userSid)) return false;

        using var usersRoot = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
        using var userHive = usersRoot.OpenSubKey(userSid, writable: true);
        if (userHive is null) return false;

        var effective = config ?? new UserPolicyConfig();
        var entries = new (string SubKey, string ValueName, bool Enabled)[]
        {
            (ExplorerPoliciesPath, "LockTaskbar", effective.LockTaskbar),
            (ExplorerPoliciesPath, "NoSetTaskbar", effective.LockTaskbar),
            (ExplorerPoliciesPath, "NoTrayContextMenu", effective.LockTaskbar),
            (ExplorerPoliciesPath, "NoControlPanel", effective.BlockSettings),
            (SystemPoliciesPath, "DisableTaskMgr", effective.BlockTaskManager),
            (WindowsSystemPoliciesPath, "DisableCMD", effective.BlockCommandPrompt),
        };

        var changed = false;
        foreach (var (subKeyPath, valueName, enabled) in entries)
        {
            if (SetOrDeleteDwordPolicy(userHive, subKeyPath, valueName, enabled))
            {
                changed = true;
            }
        }

        if (changed)
        {
            BroadcastPolicyChange();
        }

        return true;
    }

    private static bool SetOrDeleteDwordPolicy(RegistryKey userHive, string subKeyPath, string valueName, bool enabled)
    {
        if (enabled)
        {
            using var subKey = userHive.CreateSubKey(subKeyPath, writable: true);
            var existing = subKey.GetValue(valueName);
            if (existing is int currentValue && currentValue == 1)
                return false;

            subKey.SetValue(valueName, 1, RegistryValueKind.DWord);
            return true;
        }

        using var openKey = userHive.OpenSubKey(subKeyPath, writable: true);
        if (openKey is null || openKey.GetValue(valueName) is null)
            return false;

        openKey.DeleteValue(valueName, throwOnMissingValue: false);
        return true;
    }

    private static void BroadcastPolicyChange()
    {
        NativeMethods.SendMessageTimeoutW(
            NativeMethods.HWND_BROADCAST,
            NativeMethods.WM_SETTINGCHANGE,
            UIntPtr.Zero,
            "Policy",
            NativeMethods.SMTO_ABORTIFHUNG,
            1000,
            out _);
    }
}
