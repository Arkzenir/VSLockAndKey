using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace VSLockAndKey.Gui;

/// <summary>
/// Filing dialog: dropdown of the player's own name plus every group they belong to,
/// and a Confirm button. The server (BindKeyPacket handler in VSLockAndKeyModSystem)
/// re-validates file tier and group-filing rules before actually binding the key -
/// this dialog only builds the selection UI.
/// </summary>
public class GuiDialogKeyFile : GuiDialogGeneric
{
    readonly string[] values;
    readonly string[] names;
    string? selectedValue;

    GuiDialogKeyFile(ICoreClientAPI capi, string[] values, string[] names) : base(Lang.Get("vslockandkey:keyfile-title"), capi)
    {
        this.values = values;
        this.names = names;
        selectedValue = values.Length > 0 ? values[0] : null;

        ElementBounds label = ElementBounds.Fixed(0, 0, 300, 25);
        ElementBounds dropdown = ElementBounds.Fixed(0, 25, 300, 30);
        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterFixed)
            .WithFixedAlignmentOffset(0, 0);

        SingleComposer = capi.Gui
            .CreateCompo("vslockandkey-keyfile", dialogBounds)
            .AddShadedDialogBG(bgBounds, true, 5.0, 0.75f)
            .AddDialogTitleBar(Lang.Get("vslockandkey:keyfile-title"), OnTitleBarClose)
            .BeginChildElements(bgBounds)
                .AddStaticText(Lang.Get("vslockandkey:keyfile-label"), CairoFont.WhiteDetailText(), label)
                .AddDropDown(values, names, 0, OnSelectionChanged, dropdown, "targetDropdown")
                .AddSmallButton(Lang.Get("vslockandkey:common-confirm"), OnConfirmClicked, dropdown.BelowCopy(0, 15, 0, 0).WithFixedSize(120, 30))
            .EndChildElements()
            .Compose();
    }

    public static void OpenFor(EntityAgent byEntity, ItemSlot keySlot)
    {
        if (byEntity.World.Api is not ICoreClientAPI capi) return;
        IPlayer player = (byEntity as EntityPlayer)?.Player;
        if (player == null) return;

        List<string> values = new() { "player:" + player.PlayerUID };
        List<string> names = new() { Lang.Get("vslockandkey:keyfile-self", player.PlayerName) };

        bool requireOwnerOrOp = VSLockAndKeyModSystem.Config?.GroupFilingRequiresOwnerOrOp ?? true;

        foreach (PlayerGroupMembership membership in player.Groups)
        {
            if (requireOwnerOrOp && membership.Level != EnumPlayerGroupMemberShip.Owner && membership.Level != EnumPlayerGroupMemberShip.Op)
            {
                continue;
            }

            values.Add("group:" + membership.GroupUid);
            names.Add(membership.GroupName);
        }

        new GuiDialogKeyFile(capi, values.ToArray(), names.ToArray()).TryOpen();
    }

    public override string ToggleKeyCombinationCode => null;

    void OnTitleBarClose() => TryClose();

    void OnSelectionChanged(string code, bool selected)
    {
        selectedValue = code;
    }

    bool OnConfirmClicked()
    {
        if (selectedValue == null)
        {
            TryClose();
            return true;
        }

        string[] parts = selectedValue.Split(':');
        bool isGroup = parts[0] == "group";
        int index = System.Array.IndexOf(values, selectedValue);

        BindKeyPacket packet = new()
        {
            IsGroup = isGroup,
            PlayerUid = isGroup ? null : parts[1],
            GroupId = isGroup ? int.Parse(parts[1]) : 0,
            DisplayName = index >= 0 ? names[index] : ""
        };

        capi.Network.GetChannel(VSLockAndKeyModSystem.NetworkChannelId).SendPacket(packet);

        TryClose();
        return true;
    }
}
