namespace ParentalControl.Core.Models;

/// <summary>
/// Live session countdown and protection status sent from the service to the child companion/overlay.
/// </summary>
public sealed class OverlayStatus
{
    public bool ShouldShow { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsRestricted { get; set; }
    public int DailyMinutes { get; set; }
    public int MinutesUsed { get; set; }
    public string ScheduleStart { get; set; } = string.Empty;
    public string ScheduleEnd { get; set; } = string.Empty;
    public int EffectiveSecondsRemaining { get; set; }
    public int SessionMinutes { get; set; }
    public bool BlockTaskManager { get; set; }
    public bool BlockRegistry { get; set; }
    public bool BlockControlPanel { get; set; }
    public bool BlockCmd { get; set; }
    public int BlockedAppsCount { get; set; }
}
