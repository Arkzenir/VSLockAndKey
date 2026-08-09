using System.Collections.Generic;

namespace VSLockAndKey;

public class ModConfig
{
    public bool AdminBypassKeyRequirement = true;

    public List<string> ExemptPlayerUids = new();

    public List<string> ExemptGroupNames = new();

    public bool LimitUnauthorisedUse = true;

    public int KeyDurability = 50;

    public bool GroupFilingRequiresOwnerOrOp = true;

    public string AdminBypassPrivilege = "commandplayer";
}
