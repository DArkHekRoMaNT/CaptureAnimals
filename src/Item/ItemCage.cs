﻿using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace CaptureAnimals
{
    public class ItemCage : Item
    {
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            return null;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (slot.Itemstack.Item.LastCodePart() == "case")
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            byEntity.Attributes.SetInt("aiming", 1);
            byEntity.Attributes.SetInt("aimingCancel", 0);
            byEntity.StartAnimation("aim");

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Attributes.GetInt("aimingCancel") == 1) return false;

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
            if (byEntity.Attributes.GetInt("aimingCancel") == 1) return;

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
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("game:sounds/player/throw"), byEntity, byPlayer, false, 8);

            string variant = stack.Item.Code.Path;
            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation(CaptureAnimals.MOD_ID + ":thrown" + variant));
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);
            ((EntityThrownCage)entity).FiredBy = byEntity;
            ((EntityThrownCage)entity).ProjectileStack = stack;
            ((EntityThrownCage)entity).Damage = damage;

            float acc = (1 - byEntity.Attributes.GetFloat("aimingAccuracy", 0));
            double rndpitch = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1) * acc * 0.75;
            double rndyaw = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1) * acc * 0.75;

            Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.EyeHeight - 0.2, 0);
            Vec3d aheadPos = pos.AheadCopy(1, byEntity.ServerPos.Pitch + rndpitch, byEntity.ServerPos.Yaw + rndyaw);
            Vec3d velocity = (aheadPos - pos) * 0.5;

            entity.ServerPos.SetPos(
                byEntity.ServerPos.BehindCopy(0.21).XYZ.Add(0, byEntity.EyeHeight - 0.2, 0)
            );
            entity.ServerPos.Motion.Set(velocity);
            entity.Pos.SetFrom(entity.ServerPos);
            entity.World = byEntity.World;
            ((EntityThrownCage)entity).SetRotation();

            byEntity.World.SpawnEntity(entity);
            byEntity.StartAnimation("throw");
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (inSlot.Itemstack.Item.LastCodePart() == "full")
            {
                string animal = inSlot.Itemstack.Attributes.GetString("capturename");
                dsc.AppendLine(Lang.Get(CaptureAnimals.MOD_ID + ":heldhelp-cage-full") + animal);
            }

            if (inSlot.Itemstack.Item.LastCodePart() == "empty" && inSlot.Itemstack.Collectible.Attributes["bait"].Exists)
            {
                string bait = inSlot.Itemstack.Collectible.Attributes["bait"].AsString();
                dsc.AppendLine(Lang.Get(CaptureAnimals.MOD_ID + ":heldhelp-cage-empty-bait") + bait);
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            if (inSlot.Itemstack.Item.LastCodePart() == "empty")
            {
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = CaptureAnimals.MOD_ID + ":heldhelp-cage-throw-empty",
                        MouseButton = EnumMouseButton.Right
                    }
                };
            }
            else if (inSlot.Itemstack.Item.LastCodePart() == "full")
            {
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = CaptureAnimals.MOD_ID + ":heldhelp-cage-throw-full",
                        MouseButton = EnumMouseButton.Right
                    }
                };
            }
            else return base.GetHeldInteractionHelp(inSlot);
        }
    }
}