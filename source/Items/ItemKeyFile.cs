using Vintagestory.API.Common;

namespace VSLockAndKey;

/// <summary>
/// Passive tool item. All of its logic lives in ItemKey.OnHeldInteractStart, which
/// checks the offhand slot for one of these and its metal tier before opening the
/// filing dialog - a file has nothing to do when it's the active-hand item.
/// </summary>
public class ItemKeyFile : Item
{
}
