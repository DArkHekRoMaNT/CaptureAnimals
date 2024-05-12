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
        private readonly BaitsManager _baitsManager;

        public GuiDialogBait(InventoryCage inventory, ICoreClientAPI capi)
            : base(Lang.Get($"{Constants.ModId}:bait-dialog-title"), capi)
        {
            _inventory = inventory;
            _inventory.SlotModified += n => UpdateText();

            _baitsManager = capi.ModLoader.GetModSystem<BaitsManager>();

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
                .CreateCompo($"{Constants.ModId}-cage-bait-dlg", ElementStdBounds.AutosizedMainDialog)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(DialogTitle, () => TryClose())
                .BeginChildElements(bgBounds)
                    .AddItemSlotGrid(_inventory, SendInvPacket, 1, slotBounds, "slot")
                    .BeginClip(clippingBounds)
                        .AddInset(insetBounds, 3)
                        .AddDynamicText(string.Empty, CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Left), textBounds, "text")
                    .EndClip()
                    .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
                .EndChildElements()
                .Compose()
            ;

            SingleComposer.GetDynamicText("text").AutoHeight();
            UpdateText();

            SingleComposer.GetScrollbar("scrollbar").SetHeights(
                (float)insetBounds.fixedHeight, (float)textBounds.fixedHeight
            );
        }

        private void OnNewScrollbarValue(float value)
        {
            GuiElementDynamicText textElem = SingleComposer.GetDynamicText("text");

            textElem.Bounds.fixedY = 3 - value;
            textElem.Bounds.CalcWorldBounds();
        }

        private void UpdateText()
        {
            string text;

            var baitCode = _inventory[0]?.Itemstack?.Collectible?.Code;
            if (baitCode != null && _baitsManager.AllBaits.TryGetValue(baitCode, out var captureEntities))
            {
                var info = new Dictionary<string, int>();
                foreach (var captureEntity in captureEntities)
                {
                    var loc = new AssetLocation(captureEntity.Code);
                    info.Add(
                        $"{loc.Domain}:item-creature-{loc.Path}",
                        (int)(captureEntity.CaptureChance * 100f)
                    );
                }

                var orderedInfo = info.OrderBy((e) => e.Key).OrderBy((e) => e.Value);
                var sb = new StringBuilder();
                foreach (var (code, chance) in orderedInfo)
                {
                    sb.AppendLine($"{chance}%: {Lang.Get(code)}");
                }

                text = $"{sb}";
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine(Lang.Get($"{Constants.ModId}:heldinfo-cage-empty-baitlist"));
                foreach (AssetLocation key in _baitsManager.AllBaits.Keys)
                {
                    if (capi.World.GetBlock(key) != null)
                    {
                        string langkey = key.Clone().WithLocationPrefixOnce(new("block-")).ToString();
                        sb.AppendLine($"\t{Lang.Get(langkey)}");
                    }
                    else if (capi.World.GetItem(key) != null)
                    {
                        string langkey = key.Clone().WithLocationPrefixOnce(new("item-")).ToString();
                        sb.AppendLine($"\t{Lang.Get(langkey)}");
                    }
                }
                text = sb.ToString();
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
