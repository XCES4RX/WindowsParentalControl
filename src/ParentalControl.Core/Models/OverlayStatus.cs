namespace ParentalControl.Core.Models;

/// <summary>
/// Live session countdown status sent from the service to the interactive overlay process.
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
}
