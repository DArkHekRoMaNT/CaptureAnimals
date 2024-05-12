using System;
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
        public bool IsEmpty => LastCodePart(1) == "empty";
        public bool IsFull => LastCodePart(1) == "full";
        public bool IsCase => LastCodePart(1) == "case";

        private long _lastRenderMs = 0;
        private MeshData[] _meshes = Array.Empty<MeshData>();
        private MultiTextureMeshRef? _model;
        private BaitsManager _baitsManager = null!;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            _baitsManager = api.ModLoader.GetModSystem<BaitsManager>();
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            var cage = (ItemCage)slot.Itemstack.Item;

            if (cage.IsCase)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (byEntity.Controls.Sneak && cage.IsEmpty && byEntity is EntityPlayer entityPlayer)
            {
                var inventory = new InventoryCage(entityPlayer.Player, slot);
                entityPlayer.Player.InventoryManager.OpenInventory(inventory);
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
            {
                return false;
            }

            if (byEntity.World is IClientWorldAccessor)
            {
                var tf = new ModelTransform();
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
                byEntity.Attributes.GetInt("opengui") == 1)
            {
                return;
            }

            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.StopAnimation("aim");

            if (secondsUsed > 0.35f)
            {
                Throw();
            }

            void Throw()
            {
                ItemStack stack;
                EntityPlayer? entityPlayer = byEntity as EntityPlayer;
                if (entityPlayer?.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    stack = slot.TakeOut(1);
                }
                else
                {
                    stack = slot.TakeOut(0);
                    stack.StackSize = 1;
                }
                slot.MarkDirty();
                byEntity.World.PlaySoundAt(new AssetLocation("game:sounds/player/throw"),
                    byEntity, entityPlayer?.Player, false, 8);

                var code = new AssetLocation(Constants.ModId, $"thrown{stack.Item.Code.Path}");
                EntityProperties type = byEntity.World.GetEntityType(code);
                var entity = (EntityThrownCage)byEntity.World.ClassRegistry.CreateEntity(type);
                entity.FiredBy = byEntity;
                entity.ProjectileStack = stack;

                float accuracy = 1 - byEntity.Attributes.GetFloat("aimingAccuracy", 0);
                double rndpitch = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1) * accuracy * 0.75;
                double rndyaw = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1) * accuracy * 0.75;

                Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.LocalEyePos.Y - 0.2, 0);
                Vec3d aheadPos = pos.AheadCopy(1, byEntity.ServerPos.Pitch + rndpitch, byEntity.ServerPos.Yaw + rndyaw);
                Vec3d velocity = (aheadPos - pos) * 0.5;

                Vec3d startPos = byEntity.ServerPos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y - 0.2, 0);
                entity.ServerPos.SetPos(startPos);
                entity.ServerPos.Motion.Set(velocity);
                entity.Pos.SetFrom(entity.ServerPos);
                entity.World = byEntity.World;

                byEntity.World.SpawnEntity(entity);
                byEntity.StartAnimation("throw");
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            ItemCage cage = (ItemCage)inSlot.Itemstack.Item;

            if (cage.IsEmpty)
            {
                return
                [
                    new WorldInteraction
                    {
                        ActionLangCode = $"{Constants.ModId}:heldhelp-cage-throw-empty",
                        MouseButton = EnumMouseButton.Right
                    },
                    new WorldInteraction
                    {
                        ActionLangCode = $"{Constants.ModId}:heldhelp-cage-open",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak"
                    }
                ];
            }
            else if (cage.IsFull)
            {
                return new WorldInteraction[]
                {
                    new WorldInteraction
                    {
                        ActionLangCode = $"{Constants.ModId}:heldhelp-cage-throw-full",
                        MouseButton = EnumMouseButton.Right
                    }
                };
            }
            else
            {
                return base.GetHeldInteractionHelp(inSlot);
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (IsEmpty)
            {
                dsc.AppendLine(Lang.Get(
                    $"{Constants.ModId}:heldinfo-cage-empty-breakchance",
                    (int)(Attributes["breakchance"].AsFloat() * 100f)
                ));
                dsc.AppendLine(Lang.Get(
                    $"{Constants.ModId}:heldinfo-cage-empty-efficiency",
                    (int)(Attributes["efficiency"].AsFloat() * 100f)
                ));

                ItemStack bait = inSlot.Itemstack.Attributes.GetItemstack("bait");
                if (bait != null)
                {
                    bait.ResolveBlockOrItem(api.World);
                    dsc.AppendLine(Lang.Get(
                        $"{Constants.ModId}:heldinfo-cage-empty-bait",
                        bait.GetName()
                    ));
                }
            }
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string cageName = base.GetHeldItemName(itemStack);

            if (IsFull)
            {
                string contentName = itemStack.Attributes.GetString("capturename", Lang.Get($"{Constants.ModId}:nothing"));
                return Lang.Get("{0} with {1}", cageName, contentName);
            }

            return cageName;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            if (!IsCase)
            {
                if (_model == null)
                {
                    CreateModel(capi);
                }

                // Hook to fix renderinfo.dt is zero if not in hand
                // Do real dt for all equivalent stacks
                float dt = api.World.ElapsedMilliseconds - _lastRenderMs;
                _lastRenderMs = api.World.ElapsedMilliseconds;

                UpdateModel(capi, dt / 1000);

                renderinfo.ModelRef = _model;
            }

            void CreateModel(ICoreClientAPI capi)
            {
                var shapes = new Shape[]
                {
                    capi.Assets.Get<Shape>(new AssetLocation(Constants.ModId, "shapes/item/cage/soul.json")),
                    capi.Assets.Get<Shape>(new AssetLocation(Constants.ModId, "shapes/item/cage/ring1.json")),
                    capi.Assets.Get<Shape>(new AssetLocation(Constants.ModId, "shapes/item/cage/ring2.json")),
                    capi.Assets.Get<Shape>(new AssetLocation(Constants.ModId, "shapes/item/cage/ring3.json"))
                };

                _meshes = new MeshData[shapes.Length];
                for (int i = 0; i < _meshes.Length; i++)
                {
                    capi.Tesselator.TesselateShape(this, shapes[i], out _meshes[i]);
                }

                var origin = new Vec3f(0.5f, 0.5f, 0.5f);
                _meshes[0].Rotate(origin, RandomRotation(), RandomRotation(), RandomRotation());
                _meshes[1].Rotate(origin, 0, 0, RandomRotation());
                _meshes[2].Rotate(origin, 0, RandomRotation(), ((float)Math.PI) / 2);
                _meshes[3].Rotate(origin, RandomRotation(), 0, 0);

                UploadModel(capi);
            }

            void UpdateModel(ICoreClientAPI capi, float dt)
            {
                var origin = new Vec3f(0.5f, 0.5f, 0.5f);
                _meshes[0].Rotate(origin, 4 * dt, 4 * dt, 4 * dt);
                _meshes[1].Rotate(origin, 0 * dt, 0 * dt, 3 * dt);
                _meshes[2].Rotate(origin, 0 * dt, 2f * dt, 0 * dt);
                _meshes[3].Rotate(origin, 1 * dt, 0 * dt, 0 * dt);

                UploadModel(capi);
            }

            void UploadModel(ICoreClientAPI capi)
            {
                int vertices = 0, indices = 0;
                foreach (var mesh in _meshes)
                {
                    vertices += mesh.VerticesCount;
                    indices += mesh.IndicesCount;
                }

                var fullMesh = new MeshData(vertices, indices);
                foreach (var mesh in _meshes)
                {
                    fullMesh.AddMeshData(mesh);
                }

                if (_model == null)
                {
                    _model = capi.Render.UploadMultiTextureMesh(fullMesh);
                }
                else
                {
                    capi.Render.UpdateMesh(_model.meshrefs[0], fullMesh);
                }
            }

            float RandomRotation()
            {
                return 0 * (float)(api.World.Rand.NextDouble() * Math.PI * 2);
            }
        }
    }
}
