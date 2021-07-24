using System;
using System.Text;
using SharedUtils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CaptureAnimals
{
    public class ItemCage : Item
    {
        public bool IsEmpty => LastCodePart(1) == "empty";
        public bool IsFull => LastCodePart(1) == "full";
        public bool IsCase => LastCodePart(1) == "case";

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (((ItemCage)slot.Itemstack.Item).IsCase)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (byEntity.Controls.Sneak && ((ItemCage)slot.Itemstack.Item).IsEmpty)
            {
                IPlayer player = (byEntity as EntityPlayer).Player;
                InventoryCage inventory = new InventoryCage(player, slot);

                player.InventoryManager.OpenInventory(inventory);

                byEntity.Attributes.SetInt("opengui", 1);
                handling = EnumHandHandling.Handled;
                return;
            }

            byEntity.Attributes.SetInt("opengui", 0);
            byEntity.Attributes.SetInt("aiming", 1);
            byEntity.Attributes.SetInt("aimingCancel", 0);
            byEntity.StartAnimation("aim");

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Attributes.GetInt("aimingCancel") == 1 ||
                byEntity.Attributes.GetInt("opengui") == 1)
                return false;

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float offset = GameMath.Clamp(secondsUsed * 3, 0, 1.5f);

                tf.Translation.Set(offset / 4f, offset / 2f, 0);
                tf.Rotation.Set(0, 0, GameMath.Min(90, secondsUsed * 360 / 1.5f));

                byEntity.Controls.UsingHeldItemTransformBefore = tf;
            }

            return true;
        }


        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.StopAnimation("aim");

            if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
            {
                byEntity.Attributes.SetInt("aimingCancel", 1);
            }

            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Attributes.GetInt("aimingCancel") == 1 ||
                byEntity.Attributes.GetInt("opengui") == 1) return;

            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.StopAnimation("aim");

            if (secondsUsed < 0.35f) return;

            float damage = 0;

            ItemStack stack;
            if ((byEntity as EntityPlayer)?.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                stack = slot.TakeOut(1);
            }
            else
            {
                stack = slot.TakeOut(0);
                stack.StackSize = 1;
            }
            slot.MarkDirty();

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer player) byPlayer = byEntity.World.PlayerByUid(player.PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("game:sounds/player/throw"), byEntity, byPlayer, false, 8);

            string variant = stack.Item.Code.Path;
            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation(ConstantsCore.ModId, "thrown" + variant));
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);
            ((EntityThrownCage)entity).FiredBy = byEntity;
            ((EntityThrownCage)entity).ProjectileStack = stack;
            ((EntityThrownCage)entity).Damage = damage;

            float acc = 1 - byEntity.Attributes.GetFloat("aimingAccuracy", 0);
            double rndpitch = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1) * acc * 0.75;
            double rndyaw = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1) * acc * 0.75;

            Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.LocalEyePos.Y - 0.2, 0);
            Vec3d aheadPos = pos.AheadCopy(1, byEntity.ServerPos.Pitch + rndpitch, byEntity.ServerPos.Yaw + rndyaw);
            Vec3d velocity = (aheadPos - pos) * 0.5;

            entity.ServerPos.SetPos(
                byEntity.ServerPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y - 0.2, 0)
            );
            entity.ServerPos.Motion.Set(velocity);
            entity.Pos.SetFrom(entity.ServerPos);
            entity.World = byEntity.World;

            byEntity.World.SpawnEntity(entity);
            byEntity.StartAnimation("throw");
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            if (((ItemCage)inSlot.Itemstack.Item).IsEmpty)
            {
                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = ConstantsCore.ModId + ":heldhelp-cage-throw-empty",
                        MouseButton = EnumMouseButton.Right
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = ConstantsCore.ModId + ":heldhelp-cage-open",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak"
                    }
                };
            }
            else if (((ItemCage)inSlot.Itemstack.Item).IsFull)
            {
                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = ConstantsCore.ModId + ":heldhelp-cage-throw-full",
                        MouseButton = EnumMouseButton.Right
                    }
                };
            }
            else return base.GetHeldInteractionHelp(inSlot);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (IsEmpty)
            {
                dsc.AppendLine(Lang.Get(
                    ConstantsCore.ModId + ":heldinfo-cage-empty-breakchance",
                    (int)(Attributes["breakchance"].AsFloat() * 100f)
                ));
                dsc.AppendLine(Lang.Get(
                    ConstantsCore.ModId + ":heldinfo-cage-empty-efficiency",
                    (int)(Attributes["efficiency"].AsFloat() * 100f)
                ));

                ItemStack bait = inSlot.Itemstack.Attributes.GetItemstack("bait");
                if (bait != null)
                {
                    bait.ResolveBlockOrItem(api.World);
                    dsc.AppendLine(Lang.Get(
                        ConstantsCore.ModId + ":heldinfo-cage-empty-bait",
                        bait.GetName()
                    ));
                }
            }
            else if (IsFull)
            {
                dsc.AppendLine(Lang.Get(
                    ConstantsCore.ModId + ":heldinfo-cage-full-entityname",
                    inSlot.Itemstack.Attributes.GetString(
                        "capturename",
                        Lang.Get(ConstantsCore.ModId + ":nothing")
                    )
                ));
            }
        }

        public override void InGuiIdle(IWorldAccessor world, ItemStack stack)
        {
            base.InGuiIdle(world, stack);

            GuiTransform.Rotation.Y = world.ElapsedMilliseconds / 10f % 360f;
            GuiTransform.Rotation.Z = world.ElapsedMilliseconds / 10f % 360f;
            GuiTransform.Rotation.X = world.ElapsedMilliseconds / 10f % 360f;
        }
        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            base.OnHeldIdle(slot, byEntity);
            IWorldAccessor world = byEntity.World;

            FpHandTransform.Rotation.Y = world.ElapsedMilliseconds / 10f % 360f;
            FpHandTransform.Rotation.Z = world.ElapsedMilliseconds / 10f % 360f;
            FpHandTransform.Rotation.X = world.ElapsedMilliseconds / 10f % 360f;

            TpHandTransform.Rotation.Y = world.ElapsedMilliseconds / 10f % 360f;
            TpHandTransform.Rotation.Z = world.ElapsedMilliseconds / 10f % 360f;
            TpHandTransform.Rotation.X = world.ElapsedMilliseconds / 10f % 360f;
        }
        public override void OnGroundIdle(EntityItem entityItem)
        {
            base.OnGroundIdle(entityItem);
            IWorldAccessor world = entityItem.World;

            GroundTransform.Rotation.Y = world.ElapsedMilliseconds / 10f % 360f;
            GroundTransform.Rotation.Z = world.ElapsedMilliseconds / 10f % 360f;
            GroundTransform.Rotation.X = world.ElapsedMilliseconds / 10f % 360f;
        }
    }
}