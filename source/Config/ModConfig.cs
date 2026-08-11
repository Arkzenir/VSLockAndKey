using System.Collections.Generic;

namespace VSLockAndKey;

public class ModConfig
{
    public bool AdminBypassKeyRequirement = true;

    public List<string> ExemptPlayerUids = new();

    public List<string> ExemptGroupNames = new();

    public bool LimitUnauthorisedUse = true;

    public int KeyDurability = 3;

    public bool GroupFilingRequiresOwnerOrOp = true;

    public string AdminBypassPrivilege = "commandplayer";

    /// <summary>
    /// If false, keys never show their "Bound to: X" / "Not yet filed" status line
    /// in their tooltip - lets a server hide who/what a key is filed to at a glance.
    /// </summary>
    public bool ShowKeyBindingInfo = true;
}
