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

        // Widths/heights are the original layout's numbers x1.3 (a deliberate size
        // increase). Vertical placement, by contrast, is derived rather than
        // guessed: AddShadedDialogBG(withTitleBar: true) only reserves title-bar
        // space in the drawn background texture, not in bgBounds' own coordinate
        // frame, so child elements still start at bgBounds' y=0 unless explicitly
        // pushed down - hence starting the label at GuiStyle.TitleBarHeight (the
        // actual reserved title bar height) instead of an eyeballed pixel offset,
        // then chaining every element below the previous one with GuiStyle's own
        // standard element gap (HalfPadding) rather than more guessed numbers.
        ElementBounds label = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight, 390, 33);
        ElementBounds dropdown = label.BelowCopy(0, GuiStyle.HalfPadding, 0, 0).WithFixedSize(390, 39);
        ElementBounds confirmButton = dropdown.BelowCopy(0, GuiStyle.HalfPadding * 2, 0, 0).WithFixedSize(156, 39);
        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle);

        SingleComposer = capi.Gui
            .CreateCompo("vslockandkey-keyfile", dialogBounds)
            .AddShadedDialogBG(bgBounds, true, 5.0, 0.75f)
            .AddDialogTitleBar(Lang.Get("vslockandkey:keyfile-title"), OnTitleBarClose)
            .BeginChildElements(bgBounds)
                .AddStaticText(Lang.Get("vslockandkey:keyfile-label"), CairoFont.WhiteDetailText(), label)
                .AddDropDown(values, names, 0, OnSelectionChanged, dropdown, "targetDropdown")
                .AddSmallButton(Lang.Get("vslockandkey:common-confirm"), OnConfirmClicked, confirmButton)
            .EndChildElements()
            .Compose();
    }

    public static void OpenFor(EntityAgent byEntity, ItemSlot keySlot)
    {
        if (byEntity.World.Api is not ICoreClientAPI capi) return;
        IPlayer player = (byEntity as EntityPlayer)?.Player;
        if (player == null) return;

        List<string> values = new() { "player:" + player.PlayerUID };
        List<string> names = new() { player.PlayerName };

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
