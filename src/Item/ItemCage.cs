using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

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
            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation(Constants.MOD_ID, "thrown" + variant));
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);
            ((EntityThrownCage)entity).FiredBy = byEntity;
            ((EntityThrownCage)entity).ProjectileStack = stack;
            ((EntityThrownCage)entity).Damage = damage;

            float acc = (1 - byEntity.Attributes.GetFloat("aimingAccuracy", 0));
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
                dsc.AppendLine(Lang.Get(Constants.MOD_ID + ":heldhelp-cage-full") + animal);
            }
            else if (inSlot.Itemstack.Item.LastCodePart() == "empty" &&
                inSlot.Itemstack.Attributes.HasAttribute("bait-code") &&
                inSlot.Itemstack.Attributes.HasAttribute("bait-type") &&
                inSlot.Itemstack.Attributes.HasAttribute("bait-chance") &&
                inSlot.Itemstack.Attributes.HasAttribute("bait-minhealth") &&
                inSlot.Itemstack.Attributes.HasAttribute("bait-animals"))
            {
                string type = inSlot.Itemstack.Attributes.GetString("bait-type");
                string bait = Util.GetLang(type, inSlot.Itemstack.Attributes.GetString("bait-code"));
                float chance = float.Parse(inSlot.Itemstack.Attributes.GetString("bait-chance") ?? "0");
                float minHealth = float.Parse(inSlot.Itemstack.Attributes.GetString("bait-minhealth") ?? "0");
                List<string> animals = Util.StrToListStr(inSlot.Itemstack.Attributes.GetString("bait-animals"));
                for (int i = 0; i < animals.Count; i++)
                {
                    animals[i] = Util.GetLang(type, animals[i], Util.GetLangType.Entity);
                }

                dsc.AppendLine(Lang.Get(Constants.MOD_ID + ":heldhelp-cage-empty-bait-name") + bait);
                dsc.Append(Lang.Get(Constants.MOD_ID + ":heldhelp-cage-empty-bait-chance") + chance * 100 + "%, ");
                dsc.AppendLine(Lang.Get(Constants.MOD_ID + ":heldhelp-cage-empty-bait-minhealth") + minHealth * 100 + "%");
                dsc.AppendLine(Lang.Get(Constants.MOD_ID + ":heldhelp-cage-empty-bait-animals") + Util.ListStrToStr(animals));
            }
            else if (inSlot.Itemstack.Item.LastCodePart() == "empty")
            {
                float chance = inSlot.Itemstack.Collectible.Attributes["defaultchance"]["chance"].AsFloat();
                float minHealth = inSlot.Itemstack.Collectible.Attributes["defaultchance"]["minhealth"].AsFloat();

                dsc.Append(Lang.Get(Constants.MOD_ID + ":heldhelp-cage-empty-bait-chance") + chance * 100 + "%, ");
                dsc.AppendLine(Lang.Get(Constants.MOD_ID + ":heldhelp-cage-empty-bait-minhealth") + minHealth * 100 + "%");
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            if (inSlot.Itemstack.Item.LastCodePart() == "empty")
            {
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = Constants.MOD_ID + ":heldhelp-cage-throw-empty",
                        MouseButton = EnumMouseButton.Right
                    }
                };
            }
            else if (inSlot.Itemstack.Item.LastCodePart() == "full")
            {
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = Constants.MOD_ID + ":heldhelp-cage-throw-full",
                        MouseButton = EnumMouseButton.Right
                    }
                };
            }
            else return base.GetHeldInteractionHelp(inSlot);
        }
    }
}