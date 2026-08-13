using Vintagestory.API.Common;

namespace VSLockAndKey;

/// <summary>
/// Passive tool item. All of its logic lives in ItemKey.OnHeldInteractStart, which
/// checks the offhand slot for one of these before opening the filing dialog - any
/// file works on any key's metal, but weaker files carry far less durability, so
/// filing a lot of keys still favors owning a better one. A file has nothing to do
/// when it's the active-hand item.
/// </summary>
public class ItemKeyFile : Item
{
}
