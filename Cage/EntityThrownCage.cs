using CommonLib.Config;
using CommonLib.Utils;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CaptureAnimals
{
    public class EntityThrownCage : Entity
    {
        private bool _beforeCollided;
        private long _launchMs = 0;
        private Vec3d _motionBeforeCollide = Vec3d.Zero;
        private BaitsManager _baitsManager = null!;
        private Config _config = null!;

        public Entity? FiredBy { get; set; }
        public ItemStack ProjectileStack { get; set; } = new ItemStack();

        public override bool IsInteractable => false;

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            var configs = api.ModLoader.GetModSystem<ConfigManager>();
            _config = configs.GetConfig<Config>();

            _baitsManager = api.ModLoader.GetModSystem<BaitsManager>();
            _launchMs = World.ElapsedMilliseconds;

            if (ProjectileStack.Collectible != null)
            {
                ProjectileStack.ResolveBlockOrItem(World);
            }

            if (api.Side == EnumAppSide.Client)
            {
                AnimManager.StartAnimation(new AnimationMetaData
                {
                    Code = "soul",
                    Animation = "soul",
                    EaseInSpeed = float.MaxValue,
                    EaseOutSpeed = float.MaxValue,
                    AnimationSpeed = 0.5f
                });

                AnimManager.StartAnimation(new AnimationMetaData
                {
                    Code = "ring1",
                    Animation = "ring1",
                    EaseInSpeed = float.MaxValue,
                    EaseOutSpeed = float.MaxValue,
                    AnimationSpeed = 0.5f
                });

                AnimManager.StartAnimation(new AnimationMetaData
                {
                    Code = "ring2",
                    Animation = "ring2",
                    EaseInSpeed = float.MaxValue,
                    EaseOutSpeed = float.MaxValue,
                    AnimationSpeed = 0.5f
                });

                AnimManager.StartAnimation(new AnimationMetaData
                {
                    Code = "ring3",
                    Animation = "ring3",
                    EaseInSpeed = float.MaxValue,
                    EaseOutSpeed = float.MaxValue,
                    AnimationSpeed = 0.5f
                });
            }
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (ShouldDespawn || (ProjectileStack.Collectible is null))
            {
                return;
            }

            if (OnGround && CanCollect(null))
            {
                Api.World.SpawnItemEntity(ProjectileStack, ServerPos.XYZ);
                Die();
                return;
            }

            CollideCheck();

            ItemCage? itemCage = ProjectileStack.Collectible as ItemCage;
            if (World is IServerWorldAccessor && itemCage?.IsEmpty is true)
            {
                Entity? entity = World.GetNearestEntity(ServerPos.XYZ, 5f, 5f, MatchNearestAnimal);
                if (entity != null && entity.Alive != false && entity is not EntityPlayer)
                {
                    World.PlaySoundAt(new AssetLocation("game:sounds/thud"), this, null, false, 32);
                    World.SpawnCubeParticles(entity.SidedPos.XYZ.OffsetCopy(0, 0.2, 0), ProjectileStack, 0.2f, 20);

                    if (entity.GetBehavior("health") is EntityBehaviorHealth healthBehavior && CanCapture(entity))
                    {
                        if (TryCapture(entity, healthBehavior))
                        {
                            CaptureSuccess(entity);
                        }
                        else
                        {
                            CaptureMiss();
                        }
                    }
                }
            }

            _beforeCollided = false;
            _motionBeforeCollide = SidedPos.Motion.Clone();

            bool CanCapture(Entity entity)
            {
                return _config
                    .GetAvailableEntities(entity.Api)
                    .Contains(entity.Code.ToString());
            }
        }

        private bool TryCapture(Entity entity, EntityBehaviorHealth healthBehavior)
        {
            float captureChance = ProjectileStack.Collectible.Attributes["defaultcapturechance"].AsFloat();

            ItemStack? baitStack = ProjectileStack.Attributes.GetItemstack("bait");
            baitStack?.ResolveBlockOrItem(entity.World);

            if (baitStack != null && _baitsManager.AllBaits.TryGetValue(baitStack.Collectible.Code, out var captureEntities))
            {
                foreach (var captureEntity in captureEntities)
                {
                    if (captureEntity.Code == entity.Code.ToString())
                    {
                        captureChance = captureEntity.CaptureChance;
                        break;
                    }
                }
            }

            // -1% hp = (+1% * efficiency) capture chance
            float efficiency = ProjectileStack.Collectible.Attributes["efficiency"].AsFloat();
            captureChance += (1 - healthBehavior.Health / healthBehavior.MaxHealth) * efficiency;

            return Api.World.Rand.NextDouble() < captureChance;
        }

        private void CaptureSuccess(Entity entity)
        {
            AssetLocation location = ProjectileStack.Collectible.CodeWithVariant("type", "full");
            var full = new ItemStack(Api.World.GetItem(location));

            entity.ToAttributes(full, "capture");
            full.Attributes.SetString("capturename", entity.GetName());

            Api.World.SpawnItemEntity(full, entity.Pos.XYZ);

            FiredBy?.SendMessage(Lang.Get($"{Constants.ModId}:cage-captured"));
            entity.Die(EnumDespawnReason.Removed);
            Die();
        }

        private void CaptureMiss()
        {
            float breakChance = ProjectileStack.Collectible.Attributes["breakchance"].AsFloat();
            if (breakChance >= Api.World.Rand.NextDouble())
            {
                FiredBy?.SendMessage(Lang.Get($"{Constants.ModId}:cage-broken"));
            }
            else
            {
                FiredBy?.SendMessage(Lang.Get($"{Constants.ModId}:cage-mistake"));
                AssetLocation caseCode = ProjectileStack.Collectible.CodeWithVariant("type", "case");
                var caseStack = new ItemStack(Api.World.GetItem(caseCode));
                Api.World.SpawnItemEntity(caseStack, ServerPos.XYZ);
            }
            Die();
        }

        private bool MatchNearestAnimal(Entity entity)
        {
            bool itself = entity.EntityId == EntityId;
            bool isNotInteractable = !entity.IsInteractable;
            bool isFiredByThisPlayerRightNow =
                entity.EntityId == FiredBy?.EntityId &&
                World.ElapsedMilliseconds - _launchMs < 500;

            if (itself || isNotInteractable || isFiredByThisPlayerRightNow)
            {
                return false;
            }

            double dist = entity.CollisionBox.ToDouble()
                .Translate(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z)
                .ShortestDistanceFrom(ServerPos.X, ServerPos.Y, ServerPos.Z);

            return dist < 0.5f;
        }

        private void CollideCheck()
        {
            if (Collided)
            {
                if (!_beforeCollided && World is IServerWorldAccessor)
                {
                    float strength = GameMath.Clamp((float)_motionBeforeCollide.Length() * 4, 0, 1);

                    if (CollidedHorizontally)
                    {
                        SidedPos.Motion.X = _motionBeforeCollide.X * 0.8f;
                        SidedPos.Motion.Z = _motionBeforeCollide.Z * 0.8f;
                    }

                    if (CollidedVertically && _motionBeforeCollide.Y <= 0)
                    {
                        SidedPos.Motion.Y = GameMath.Clamp(_motionBeforeCollide.Y * -0.4f, -0.1f, 0.1f);
                    }

                    World.PlaySoundAt(new AssetLocation("game:sounds/thud"), this, null, false, 32, strength);
                    WatchedAttributes.MarkAllDirty();
                }

                _beforeCollided = true;
                return;
            }
        }

        public override bool CanCollect(Entity? byEntity)
        {
            return Alive && World.ElapsedMilliseconds - _launchMs > 1000 && ServerPos.Motion.Length() < 0.01;
        }

        public override ItemStack OnCollected(Entity byEntity)
        {
            ProjectileStack.ResolveBlockOrItem(World);
            return ProjectileStack;
        }

        public override void OnCollided()
        {
            if (ProjectileStack.Collectible is ItemCage cage && !cage.IsFull)
            {
                return;
            }

            Entity? entity = EntityUtil.EntityFromAttributes(ProjectileStack.Attributes, "capture", Api.World);
            if (entity != null)
            {
                entity.Pos.SetPos(Pos);
                entity.ServerPos.SetPos(ServerPos);
                entity.PositionBeforeFalling.Set(Pos.XYZ);
                Api.World.SpawnEntity(entity);
                Die();
            }
            else
            {
                FiredBy?.SendMessage("Undefined entity in cage!");
                Die();
            }
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            base.ToBytes(writer, forClient);

            writer.Write(_beforeCollided);
            ProjectileStack.ToBytes(writer);
        }

        public override void FromBytes(BinaryReader reader, bool fromServer)
        {
            base.FromBytes(reader, fromServer);

            _beforeCollided = reader.ReadBoolean();
            ProjectileStack = World == null ? new ItemStack(reader) : new ItemStack(reader, World);
        }
    }
}
