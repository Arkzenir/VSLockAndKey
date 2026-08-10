using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VSLockAndKey;

/// <summary>
/// Adds a key requirement on top of vanilla's own owner/group lock check, without
/// touching how locking/reinforcing itself works. See PLANNING.md section 2 for why
/// this method (the one choke point every lockable block routes through) is the
/// patch target instead of inserting a competing block behavior.
/// </summary>
[HarmonyPatch(typeof(ModSystemBlockReinforcement), nameof(ModSystemBlockReinforcement.IsLockedForInteract))]
public static class IsLockedForInteractPatch
{
    public static bool Prefix(ModSystemBlockReinforcement __instance, BlockPos pos, IPlayer forPlayer, ref bool __result)
    {
        var api = VSLockAndKeyModSystem.Api;
        var config = VSLockAndKeyModSystem.Config;
        if (api == null || config == null) return true;

        BlockReinforcement bre = __instance.GetReinforcment(pos);
        if (bre == null || !bre.Locked)
        {
            // Not locked at all - let vanilla decide (it will return false/unlocked).
            return true;
        }

        if (KeyAccessUtil.IsOwnerExempt(bre, config, api))
        {
            return true;
        }

        if (config.AdminBypassKeyRequirement && forPlayer.HasPrivilege(config.AdminBypassPrivilege))
        {
            return true;
        }

        ItemSlot? keySlot = KeyAccessUtil.FindMatchingKeySlot(forPlayer, bre);
        if (keySlot == null)
        {
            // No matching key: locked for interact regardless of what vanilla would say.
            __result = true;
            return false;
        }

        // Harmony patches a physical method, not a "side" - in singleplayer the client's
        // predictive call and the server's authoritative call go through this same
        // patched method in the same process, so without this check a single interact
        // would damage the key twice. On dedicated multiplayer this patch only ever
        // runs server-side to begin with (see VSLockAndKeyModSystem.StartServerSide),
        // so the check is a no-op there, but it's required for singleplayer correctness.
        if (config.LimitUnauthorisedUse && !KeyAccessUtil.IsAuthorized(bre, forPlayer)
            && forPlayer.Entity.World.Side == EnumAppSide.Server)
        {
            KeyAccessUtil.DamageKey(forPlayer.Entity, keySlot, config);
        }

        // Matching key present: grant access, even to a stranger who is neither the
        // owner nor a group member.
        __result = false;
        return false;
    }
}
