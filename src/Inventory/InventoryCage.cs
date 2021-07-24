using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace CaptureAnimals
{
    public class InventoryCage : InventoryGeneric
    {
        readonly ItemSlot cageSlot;
        private GuiDialogBait invDialog;


        public InventoryCage(IPlayer player, ItemSlot cageSlot)
            : base(1, "inventoryCage", player.PlayerUID, player.Entity.Api)
        {
            this.cageSlot = cageSlot;
            slots[0].MaxSlotStackSize = 1;
            SyncFromCageStack();

            OnInventoryOpened += OnInvOpened;
            OnInventoryClosed += OnInvClosed;
            SlotModified += OnSlotModified;
        }

        private void OnInvOpened(IPlayer player)
        {
            if (player.Entity.Api is ICoreClientAPI capi)
            {
                invDialog = new GuiDialogBait(this, capi);
                invDialog.TryOpen();
            }
        }

        private void OnInvClosed(IPlayer player)
        {
            SyncToCageStack();
            invDialog?.Dispose();
            invDialog = null;
        }

        private void OnSlotModified(int n)
        {
            SyncToCageStack();
        }

        public void SyncToCageStack()
        {
            cageSlot.Itemstack.Attributes.SetItemstack("bait", slots[0].Itemstack);
            cageSlot.MarkDirty();
        }

        public void SyncFromCageStack()
        {
            slots[0].Itemstack = cageSlot.Itemstack.Attributes.GetItemstack("bait");
            slots[0].Itemstack?.ResolveBlockOrItem(Api.World);
        }
    }
}