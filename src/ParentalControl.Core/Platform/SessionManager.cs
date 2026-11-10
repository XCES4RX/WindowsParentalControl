using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ParentalControl.Core.Platform;

public static class SessionManager
{
    public static bool ForceLogoff(int sessionId)
    {
        return NativeMethods.WTSLogoffSession(
            NativeMethods.WTS_CURRENT_SERVER_HANDLE,
            sessionId,
            false);
    }

    public static bool DisconnectSession(int sessionId)
    {
        return NativeMethods.WTSDisconnectSession(
            NativeMethods.WTS_CURRENT_SERVER_HANDLE,
            sessionId,
            false);
    }

    public static bool SendSessionMessage(int sessionId, string title, string message, int timeoutSeconds = 15)
    {
        // MB_OK (0x0) | MB_ICONWARNING (0x30) | MB_TOPMOST (0x40000)
        const uint style = 0x00000030 | 0x00040000;
        return NativeMethods.WTSSendMessageW(
            NativeMethods.WTS_CURRENT_SERVER_HANDLE,
            sessionId,
            title,
            title.Length * sizeof(char),
            message,
            message.Length * sizeof(char),
            style,
            timeoutSeconds,
            out _,
            bWait: false);
    }

    public static string? GetSessionUsername(int sessionId)
    {
        if (!NativeMethods.WTSQuerySessionInformationW(
                NativeMethods.WTS_CURRENT_SERVER_HANDLE,
                sessionId,
                NativeMethods.WTS_INFO_CLASS.WTSUserName,
                out var buffer,
                out _))
        {
            return null;
        }

        try
        {
            var username = Marshal.PtrToStringUni(buffer);
            return string.IsNullOrEmpty(username) ? null : username;
        }
        finally
        {
            NativeMethods.WTSFreeMemory(buffer);
        }
    }

    public static string? GetSessionDomain(int sessionId)
    {
        if (!NativeMethods.WTSQuerySessionInformationW(
                NativeMethods.WTS_CURRENT_SERVER_HANDLE,
                sessionId,
                NativeMethods.WTS_INFO_CLASS.WTSDomainName,
                out var buffer,
                out _))
        {
            return null;
        }

        try
        {
            var domain = Marshal.PtrToStringUni(buffer);
            return string.IsNullOrEmpty(domain) ? null : domain;
        }
        finally
        {
            NativeMethods.WTSFreeMemory(buffer);
        }
    }

    public static string? GetSessionUserSid(int sessionId)
    {
        var username = GetSessionUsername(sessionId);
        if (username is null) return null;

        var domain = GetSessionDomain(sessionId);
        var fullName = string.IsNullOrEmpty(domain) ? username : $"{domain}\\{username}";

        try
        {
            var account = new NTAccount(fullName);
            var sid = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
            return sid.Value;
        }
        catch (IdentityNotMappedException)
        {
            return null;
        }
    }

    public static List<(int SessionId, string Username, string? Sid)> GetActiveSessions()
    {
        var sessions = new List<(int, string, string?)>();

        if (!NativeMethods.WTSEnumerateSessionsW(
                NativeMethods.WTS_CURRENT_SERVER_HANDLE,
                0,
                1,
                out var pSessionInfo,
                out var count))
        {
            return sessions;
        }

        try
        {
            var structSize = Marshal.SizeOf<NativeMethods.WTS_SESSION_INFO>();
            for (int i = 0; i < count; i++)
            {
                var current = Marshal.PtrToStructure<NativeMethods.WTS_SESSION_INFO>(
                    pSessionInfo + i * structSize);

                if (current.State != NativeMethods.WTS_CONNECTSTATE_CLASS.WTSActive)
                    continue;

                var username = GetSessionUsername(current.SessionID);
                if (string.IsNullOrEmpty(username))
                    continue;

                var sid = GetSessionUserSid(current.SessionID);
                sessions.Add((current.SessionID, username, sid));
            }
        }
        finally
        {
            NativeMethods.WTSFreeMemory(pSessionInfo);
        }

        return sessions;
    }

    public static int? GetActiveConsoleSessionId()
    {
        var id = NativeMethods.WTSGetActiveConsoleSessionId();
        return id == 0xFFFFFFFF ? null : (int)id;
    }

    public static bool IsProcessRunningInSession(string processNameWithoutExt, int sessionId)
    {
        foreach (var proc in Process.GetProcessesByName(processNameWithoutExt))
        {
            using (proc)
            {
                if (proc.SessionId == sessionId)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Launches an executable inside an interactive user session from the LocalSystem service.
    /// </summary>
    public static bool LaunchInSession(
        int sessionId,
        string exePath,
        string arguments = "",
        bool hidden = false,
        int? waitTimeoutMs = null)
    {
        if (!File.Exists(exePath)) return false;

        if (!NativeMethods.WTSQueryUserToken(sessionId, out var userToken))
            return false;

        var primaryToken = IntPtr.Zero;
        var envBlock = IntPtr.Zero;
        var procInfo = new NativeMethods.PROCESS_INFORMATION();

        try
        {
            if (!NativeMethods.DuplicateTokenEx(
                    userToken,
                    NativeMethods.MAXIMUM_ALLOWED,
                    IntPtr.Zero,
                    NativeMethods.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                    NativeMethods.TOKEN_TYPE.TokenPrimary,
                    out primaryToken))
            {
                return false;
            }

            NativeMethods.CreateEnvironmentBlock(out envBlock, primaryToken, false);

            var commandLine = string.IsNullOrWhiteSpace(arguments)
                ? $"\"{exePath}\""
                : $"\"{exePath}\" {arguments}";

            var startupInfo = new NativeMethods.STARTUPINFO
            {
                cb = Marshal.SizeOf<NativeMethods.STARTUPINFO>(),
                lpDesktop = @"winsta0\default",
                dwFlags = NativeMethods.STARTF_USESHOWWINDOW,
                wShowWindow = hidden ? NativeMethods.SW_HIDE : NativeMethods.SW_SHOW,
            };

            var flags = NativeMethods.CREATE_UNICODE_ENVIRONMENT;
            if (hidden) flags |= NativeMethods.CREATE_NO_WINDOW;

            var created = NativeMethods.CreateProcessAsUserW(
                primaryToken,
                null,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                flags,
                envBlock,
                Path.GetDirectoryName(exePath),
                ref startupInfo,
                out procInfo);

            if (!created) return false;

            if (waitTimeoutMs.HasValue && procInfo.hProcess != IntPtr.Zero)
            {
                NativeMethods.WaitForSingleObject(procInfo.hProcess, (uint)waitTimeoutMs.Value);
            }

            return true;
        }
        finally
        {
            if (procInfo.hThread != IntPtr.Zero) NativeMethods.CloseHandle(procInfo.hThread);
            if (procInfo.hProcess != IntPtr.Zero) NativeMethods.CloseHandle(procInfo.hProcess);
            if (envBlock != IntPtr.Zero) NativeMethods.DestroyEnvironmentBlock(envBlock);
            if (primaryToken != IntPtr.Zero) NativeMethods.CloseHandle(primaryToken);
            NativeMethods.CloseHandle(userToken);
        }
    }
}
