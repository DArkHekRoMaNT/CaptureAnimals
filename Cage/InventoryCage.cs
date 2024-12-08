using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace CaptureAnimals
{
    public class InventoryCage : InventoryGeneric
    {
        public override bool RemoveOnClose => true;

        private readonly ItemSlot _cageSlot;
        private readonly int _cageStackId;
        private readonly int _cageSlotId;
        private GuiDialogBait? _invDialog;

        public InventoryCage(IPlayer player, ItemSlot cageSlot)
            : base(1, "inventoryCage", player.PlayerUID, player.Entity.Api)
        {
            _cageSlot = cageSlot;
            slots[0].MaxSlotStackSize = 1;
            SyncFromCageStack();

            OnInventoryOpened += OnInvOpened;
            OnInventoryClosed += OnInvClosed;
            SlotModified += slotId => SyncToCageStack();

            _cageStackId = Random.Shared.Next();
            _cageSlot.Itemstack.TempAttributes.SetInt("cageStackId", _cageStackId);
            _cageSlotId = _cageSlot.Inventory.GetSlotId(_cageSlot);
            _cageSlot.Inventory.SlotModified += OnCageSlotModified;
        }

        private void OnCageSlotModified(int slotId)
        {
            if (_cageSlotId != slotId)
            {
                return;
            }

            if (_cageSlot.Itemstack == null || _cageSlot.Itemstack.TempAttributes.GetInt("cageStackId") != _cageStackId)
            {
                _invDialog?.TryClose();
            }
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
            _cageSlot.Inventory.SlotModified -= OnCageSlotModified;
        }

        public void SyncToCageStack()
        {
            if (_cageSlot.Itemstack != null)
            {
                _cageSlot.Itemstack.Attributes.SetItemstack("bait", slots[0].Itemstack);
                _cageSlot.MarkDirty();
            }
        }

        public void SyncFromCageStack()
        {
            slots[0].Itemstack = _cageSlot.Itemstack.Attributes.GetItemstack("bait");
            slots[0].Itemstack?.ResolveBlockOrItem(Api.World);
        }
    }
}
