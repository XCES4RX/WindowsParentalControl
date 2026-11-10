namespace ParentalControl.Core.Models;

/// <summary>
/// Per-user Windows policy restrictions enforced for a child account.
/// </summary>
public sealed class UserPolicyConfig
{
    public int UserId { get; set; }
    public bool LockTaskbar { get; set; }
    public bool BlockSettings { get; set; }
    public bool BlockTaskManager { get; set; }
    public bool BlockCommandPrompt { get; set; }
}
