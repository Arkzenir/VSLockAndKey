using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VSLockAndKey.Gui;

/// <summary>
/// Composition mirrors vanilla's GuiDialogBlockEntityInventory (AddItemSlotGrid and
/// all the drag/drop/slot-transfer behavior that comes with it) - the only real
/// difference is the transport: block dialogs tunnel packets through
/// SendBlockEntityPacketWithOffset since they're tied to a BlockPos, but a held
/// item has no position, so this uses ICoreClientAPI.Network.SendPacketClient
/// directly - the same generic, engine-level channel InventoryBase.Open/Close and
/// every other non-block-tied inventory transfer already rides on.
/// </summary>
public class GuiDialogKeyring : GuiDialogGeneric
{
    readonly InventoryGeneric inventory;

    public GuiDialogKeyring(string dialogTitle, InventoryGeneric inventory, ICoreClientAPI capi) : base(dialogTitle, capi)
    {
        this.inventory = inventory;

        int cols = Math.Min(inventory.Count, 6);
        int rows = (int)Math.Ceiling(inventory.Count / (float)cols);

        double pad = GuiElementItemSlotGrid.unscaledSlotPadding;
        ElementBounds slotGridBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, pad, pad, cols, rows);
        ElementBounds insetBounds = slotGridBounds.ForkBoundingParent(6, 6, 6, 6);
        ElementBounds dialogBounds = insetBounds
            .ForkBoundingParent(GuiStyle.ElementToDialogPadding, GuiStyle.ElementToDialogPadding + 20, GuiStyle.ElementToDialogPadding, GuiStyle.ElementToDialogPadding)
            .WithAlignment(EnumDialogArea.CenterMiddle);

        SingleComposer = capi.Gui
            .CreateCompo("vslockandkey-keyring", dialogBounds)
            .AddShadedDialogBG(ElementBounds.Fill)
            .AddDialogTitleBar(dialogTitle, OnTitleBarClose)
            .AddInset(insetBounds)
            .AddItemSlotGrid(inventory, SendPacket, cols, slotGridBounds, "slotgrid")
            .Compose();
    }

    void SendPacket(object packet)
    {
        capi.Network.SendPacketClient(packet);
    }

    void OnTitleBarClose() => TryClose();

    public override string ToggleKeyCombinationCode => null;

    public override void OnGuiClosed()
    {
        base.OnGuiClosed();
        capi.World.Player.InventoryManager.CloseInventoryAndSync(inventory);
    }
}
