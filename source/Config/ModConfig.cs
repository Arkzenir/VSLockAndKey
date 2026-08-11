using System.Collections.Generic;

namespace VSLockAndKey;

public class ModConfig
{
    public bool AdminBypassKeyRequirement = false;

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

    /// <summary>
    /// Keyring capacity, keyed by the keyring's "material" variant state. A material
    /// missing from this map (e.g. a state added by a future update) falls back to
    /// DefaultKeyringSlots. Shrinking a value below what a live keyring's contents
    /// already fill doesn't delete anything - the extra slots just stop being
    /// exposed until raised again.
    /// </summary>
    public Dictionary<string, int> KeyringSlotsByMaterial = DefaultKeyringSlotsByMaterial();

    public int DefaultKeyringSlots = 4;

    /// <summary>
    /// Shared by the field initializer above and VSLockAndKeyModSystem.StartPre's
    /// guard against a config file that has KeyringSlotsByMaterial present but
    /// explicitly emptied out - a single source for "what the defaults actually
    /// are" instead of the same dictionary literal living in two places.
    /// </summary>
    public static Dictionary<string, int> DefaultKeyringSlotsByMaterial() => new()
    {
        ["rope"] = 2,
        ["tinbronze"] = 3,
        ["bismuthbronze"] = 3,
        ["blackbronze"] = 3,
        ["iron"] = 4,
        ["meteoriciron"] = 5,
        ["steel"] = 6
    };
}
