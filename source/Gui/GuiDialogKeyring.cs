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

        // AddItemSlotGrid always draws each slot at its fixed, unscaled pixel size
        // (GuiElementPassiveItemSlot.unscaledSlotSize) regardless of the bounds it's
        // given - it does not stretch to fill. So slotGridBounds must stay at its
        // natural size (matching ElementStdBounds.SlotGrid's own math exactly) or
        // the grid renders smaller than its container and leaves dead space. The
        // ~30% larger window instead comes from genuinely thicker border padding
        // around that correctly-sized grid.
        double pad = GuiElementItemSlotGrid.unscaledSlotPadding;
        ElementBounds slotGridBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, pad, pad, cols, rows);
        ElementBounds insetBounds = slotGridBounds.ForkBoundingParent(8, 8, 8, 8);
        ElementBounds dialogBounds = insetBounds
            .ForkBoundingParent(GuiStyle.ElementToDialogPadding * 1.3, GuiStyle.ElementToDialogPadding * 1.3 + 20, GuiStyle.ElementToDialogPadding * 1.3, GuiStyle.ElementToDialogPadding * 1.3)
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
