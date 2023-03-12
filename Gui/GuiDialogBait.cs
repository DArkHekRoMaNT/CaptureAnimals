using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace CaptureAnimals
{
    public class GuiDialogBait : GuiDialogGeneric
    {
        private readonly InventoryCage _inventory;

        public GuiDialogBait(InventoryCage inventory, ICoreClientAPI capi)
            : base(Lang.Get(Constants.ModId + ":bait-dialog-title"), capi)
        {
            _inventory = inventory;
            _inventory.SlotModified += n => UpdateText();

            InitDialog();
        }

        private void InitDialog()
        {
            ElementBounds slotBounds = ElementStdBounds.Slot(0, GuiStyle.TitleBarHeight);

            ElementBounds textBounds = ElementBounds.Fixed(slotBounds.fixedWidth + 13, (int)GuiStyle.TitleBarHeight + 3, 300, 300);
            ElementBounds clippingBounds = textBounds.ForkBoundingParent();
            ElementBounds insetBounds = textBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);

            ElementBounds scrollbarBounds = insetBounds
                .CopyOffsetedSibling(insetBounds.fixedWidth + 3.0)
                .WithFixedWidth(GuiElementScrollbar.DefaultScrollbarWidth)
                .WithFixedPadding(GuiElementScrollbar.DeafultScrollbarPadding);

            ElementBounds bgBounds = ElementStdBounds.DialogBackground();

            // Temporally fix scrollbar without bg
            bgBounds.BothSizing = ElementSizing.Fixed;
            bgBounds.fixedWidth = 388;
            bgBounds.fixedHeight = 340;

            SingleComposer = capi.Gui
                .CreateCompo(Constants.ModId + "-cage-bait-dlg", ElementStdBounds.AutosizedMainDialog)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(DialogTitle, () => TryClose())
                .BeginChildElements(bgBounds)
                    .AddItemSlotGrid(_inventory, SendInvPacket, 1, slotBounds, "slot")
                    .BeginClip(clippingBounds)
                        .AddInset(insetBounds, 3)
                        .AddDynamicText("", CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Left), textBounds, "text")
                    .EndClip()
                    .AddVerticalScrollbar(OnNewScrollbarvalue, scrollbarBounds, "scrollbar")
                .EndChildElements()
                .Compose()
            ;

            SingleComposer.GetDynamicText("text").AutoHeight();
            UpdateText();

            SingleComposer.GetScrollbar("scrollbar").SetHeights(
                (float)insetBounds.fixedHeight, (float)textBounds.fixedHeight
            );
        }

        private void OnNewScrollbarvalue(float value)
        {
            GuiElementDynamicText textElem = SingleComposer.GetDynamicText("text");

            textElem.Bounds.fixedY = 3 - value;
            textElem.Bounds.CalcWorldBounds();
        }

        private void UpdateText()
        {
            string text;

            var baitCode = _inventory[0]?.Itemstack?.Collectible?.Code;
            var baitsManager = capi.ModLoader.GetModSystem<BaitsManager>();
            if (baitCode != null && baitsManager.AllBaits.TryGetValue(baitCode, out var captureEntities))
            {
                var info = new Dictionary<string, int>();
                foreach (var captureEntity in captureEntities)
                {
                    var loc = new AssetLocation(captureEntity.Code);
                    info.Add(
                        Lang.Get(loc.Domain + ":item-creature-" + loc.Path),
                        (int)(captureEntity.CaptureChance * 100f)
                    );
                }

                var orderedInfo = info.OrderBy((e) => e.Key).OrderBy((e) => e.Value);
                StringBuilder str = new StringBuilder();
                foreach (var el in orderedInfo)
                {
                    str.AppendLine(el.Value + "%: " + el.Key);
                }

                text = str + "";
            }
            else
            {
                text = Lang.Get(Constants.ModId + ":bait-dialog-placeholder");
            }

            var textElem = SingleComposer.GetDynamicText("text");
            textElem.SetNewText(text);

            SingleComposer
                .GetScrollbar("scrollbar")
                .SetNewTotalHeight((float)textElem.Bounds.fixedHeight);
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            capi.Network.SendPacketClient(_inventory.Close(capi.World.Player));
            SingleComposer.GetSlotGrid("slot").OnGuiClosed(capi);
        }

        private void SendInvPacket(object packet)
        {
            capi.Network.SendPacketClient(packet);
        }
    }
}
