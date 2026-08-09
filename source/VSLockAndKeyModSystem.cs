using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

[assembly: ModInfo("VSLockAndKey", "vslockandkey")]

namespace VSLockAndKey;

public class VSLockAndKeyModSystem : ModSystem
{
    public const string NetworkChannelId = "vslockandkey";
    const string HarmonyId = "vslockandkey.islockedforinteract";
    const string ConfigFileName = "vslockandkey.json";

    public static ICoreAPI? Api { get; private set; }
    public static ModConfig? Config { get; private set; }

    Harmony? harmony;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        Api = api;

        api.RegisterItemClass("ItemKey", typeof(ItemKey));
        api.RegisterItemClass("ItemKeyFile", typeof(ItemKeyFile));
        api.RegisterItemClass("ItemKeyring", typeof(ItemKeyring));
        api.RegisterCollectibleBehaviorClass("Keyring", typeof(CollectibleBehaviorKeyring));

        api.Network.RegisterChannel(NetworkChannelId)
            .RegisterMessageType<BindKeyPacket>();

        api.Logger.Debug("[VSLockAndKey] Mod loaded.");
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        LoadConfig(api);

        harmony = new Harmony(HarmonyId);
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        api.Network.GetChannel(NetworkChannelId).SetMessageHandler<BindKeyPacket>(OnBindKeyPacket);
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll(HarmonyId);
        base.Dispose();
    }

    void LoadConfig(ICoreServerAPI api)
    {
        ModConfig config = api.LoadModConfig<ModConfig>(ConfigFileName);
        if (config == null)
        {
            config = new ModConfig();
            api.StoreModConfig(config, ConfigFileName);
            api.Logger.Notification($"[VSLockAndKey] No config found, wrote defaults to {ConfigFileName}.");
        }

        Config = config;
    }

    void OnBindKeyPacket(IServerPlayer fromPlayer, BindKeyPacket packet)
    {
        ItemSlot keySlot = fromPlayer.InventoryManager.ActiveHotbarSlot;
        ItemSlot fileSlot = fromPlayer.Entity.LeftHandItemSlot;

        if (keySlot?.Itemstack?.Collectible is not ItemKey) return;
        if (fileSlot?.Itemstack?.Collectible is not ItemKeyFile) return;

        string keyMetal = keySlot.Itemstack.Collectible.Variant["metal"];
        string fileMetal = fileSlot.Itemstack.Collectible.Variant["metal"];
        if (!MetalTier.IsAtLeast(fileMetal, keyMetal)) return;

        if (packet.IsGroup)
        {
            if (Config.GroupFilingRequiresOwnerOrOp)
            {
                PlayerGroupMembership membership = fromPlayer.GetGroup(packet.GroupId);
                if (membership == null || (membership.Level != EnumPlayerGroupMemberShip.Owner && membership.Level != EnumPlayerGroupMemberShip.Op))
                {
                    return;
                }
            }
            else if (fromPlayer.GetGroup(packet.GroupId) == null)
            {
                return;
            }

            keySlot.Itemstack.Attributes.SetInt(KeyAccessUtil.BoundGroupIdAttr, packet.GroupId);
            keySlot.Itemstack.Attributes.RemoveAttribute(KeyAccessUtil.BoundPlayerUidAttr);
        }
        else
        {
            if (packet.PlayerUid != fromPlayer.PlayerUID) return;

            keySlot.Itemstack.Attributes.SetString(KeyAccessUtil.BoundPlayerUidAttr, packet.PlayerUid);
            keySlot.Itemstack.Attributes.RemoveAttribute(KeyAccessUtil.BoundGroupIdAttr);
        }

        keySlot.Itemstack.Attributes.SetString(KeyAccessUtil.BoundNameAttr, packet.DisplayName);
        keySlot.Itemstack.Attributes.SetInt(KeyAccessUtil.DurabilityAttr, Config.KeyDurability);
        keySlot.MarkDirty();
    }
}
