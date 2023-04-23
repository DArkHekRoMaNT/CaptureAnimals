using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace CaptureAnimals
{
    public class InventoryCage : InventoryGeneric
    {
        private readonly ItemSlot _cageSlot;
        private GuiDialogBait? _invDialog;


        public InventoryCage(IPlayer player, ItemSlot cageSlot)
            : base(1, "inventoryCage", player.PlayerUID, player.Entity.Api)
        {
            _cageSlot = cageSlot;
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
                _invDialog = new GuiDialogBait(this, capi);
                _invDialog.TryOpen();
            }
        }

        private void OnInvClosed(IPlayer player)
        {
            SyncToCageStack();
            _invDialog?.Dispose();
            _invDialog = null;
        }

        private void OnSlotModified(int n)
        {
            SyncToCageStack();
        }

        public void SyncToCageStack()
        {
            _cageSlot.Itemstack.Attributes.SetItemstack("bait", slots[0].Itemstack);
            _cageSlot.MarkDirty();
        }

        public void SyncFromCageStack()
        {
            slots[0].Itemstack = _cageSlot.Itemstack.Attributes.GetItemstack("bait");
            slots[0].Itemstack?.ResolveBlockOrItem(Api.World);
        }
    }
}
