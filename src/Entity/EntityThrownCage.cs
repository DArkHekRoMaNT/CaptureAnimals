using SharedUtils;
using SharedUtils.Extensions;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;

namespace CaptureAnimals
{
    public class EntityThrownCage : Entity
    {
        bool beforeCollided;
        long launchMs = 0;
        Vec3d motionBeforeCollide = Vec3d.Zero;
        BaitsManager baitsManager;

        public Entity FiredBy { get; set; }
        public float Damage { get; set; }
        public ItemStack ProjectileStack { get; set; }

        public override bool IsInteractable => false;


        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            baitsManager = Api.ModLoader.GetModSystem<BaitsManager>();

            launchMs = World.ElapsedMilliseconds;

            if (ProjectileStack?.Collectible != null)
            {
                ProjectileStack.ResolveBlockOrItem(World);
            }

            if (api.Side == EnumAppSide.Client)
            {
                AnimManager.StartAnimation(new AnimationMetaData()
                {
                    Code = "soul",
                    Animation = "soul",
                    EaseInSpeed = float.MaxValue,
                    EaseOutSpeed = float.MaxValue,
                    AnimationSpeed = 0.5f
                });

                AnimManager.StartAnimation(new AnimationMetaData()
                {
                    Code = "ring1",
                    Animation = "ring1",
                    EaseInSpeed = float.MaxValue,
                    EaseOutSpeed = float.MaxValue,
                    AnimationSpeed = 0.5f
                });

                AnimManager.StartAnimation(new AnimationMetaData()
                {
                    Code = "ring2",
                    Animation = "ring2",
                    EaseInSpeed = float.MaxValue,
                    EaseOutSpeed = float.MaxValue,
                    AnimationSpeed = 0.5f
                });

                AnimManager.StartAnimation(new AnimationMetaData()
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

            if (ShouldDespawn) return;

            if (OnGround && CanCollect(null))
            {
                Api.World.SpawnItemEntity(ProjectileStack, ServerPos.XYZ);
                Die();
                return;
            }

            CollideCheck();

            if (World is IServerWorldAccessor && ((ItemCage)ProjectileStack.Item)?.IsEmpty == true)
            {
                Entity entity = World.GetNearestEntity(ServerPos.XYZ, 5f, 5f, (e) =>
                {
                    if (e.EntityId == this.EntityId || (FiredBy != null && e.EntityId == FiredBy.EntityId && World.ElapsedMilliseconds - launchMs < 500) || !e.IsInteractable)
                    {
                        return false;
                    }

                    double dist = e.CollisionBox.ToDouble().Translate(e.ServerPos.X, e.ServerPos.Y, e.ServerPos.Z).ShortestDistanceFrom(ServerPos.X, ServerPos.Y, ServerPos.Z);
                    return dist < 0.5f;
                });

                if (entity != null && (entity as EntityPlayer) == null && entity.Alive)
                {
                    bool didDamage = entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Entity, SourceEntity = FiredBy ?? (this), Type = EnumDamageType.BluntAttack }, Damage);
                    World.PlaySoundAt(new AssetLocation("game:sounds/thud"), this, null, false, 32);
                    World.SpawnCubeParticles(entity.SidedPos.XYZ.OffsetCopy(0, 0.2, 0), ProjectileStack, 0.2f, 20);

                    if (FiredBy is EntityPlayer && didDamage)
                    {
                        World.PlaySoundFor(new AssetLocation("game:sounds/player/projectilehit"), (FiredBy as EntityPlayer).Player, false, 24);
                    }

                    if (entity.GetBehavior("health") is EntityBehaviorHealth behavior)
                    {
                        float captureChance = ProjectileStack.Collectible.Attributes["defaultcapturechance"].AsFloat();

                        ItemStack baitStack = ProjectileStack.Attributes.GetItemstack("bait");
                        baitStack?.ResolveBlockOrItem(entity.World);

                        if (baitStack != null && baitsManager.AllBaits.TryGetValue(baitStack.Collectible.Code, out var captureEntities))
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

                        float efficiency = ProjectileStack.Collectible.Attributes["efficiency"].AsFloat();
                        captureChance += (1 - behavior.Health / behavior.MaxHealth) * efficiency; // -1% hp = (+1% * efficiency) capture chance

                        if (captureChance < Api.World.Rand.NextDouble())
                        {
                            float breakChance = ProjectileStack.Collectible.Attributes["breakchance"].AsFloat();
                            if (breakChance >= Api.World.Rand.NextDouble())
                            {
                                FiredBy.SendMessage(Lang.Get(ConstantsCore.ModId + ":cage-broken"));
                            }
                            else
                            {
                                FiredBy.SendMessage(Lang.Get(ConstantsCore.ModId + ":cage-mistake"));

                                //Api.World.SpawnItemEntity(ProjectileStack, ServerPos.XYZ);
                                AssetLocation caseCode = ProjectileStack.Collectible.CodeWithVariant("type", "case");
                                var caseStack = new ItemStack(Api.World.GetItem(caseCode));
                                Api.World.SpawnItemEntity(caseStack, ServerPos.XYZ);
                            }
                        }
                        else
                        {
                            AssetLocation location = ProjectileStack.Item?.CodeWithVariant("type", "full");
                            var full = new ItemStack(Api.World.GetItem(location));

                            entity.ToAttributes(full, "capture");
                            full.Attributes.SetString("capturename", entity.GetName());

                            Api.World.SpawnItemEntity(full, entity.Pos.XYZ);

                            FiredBy.SendMessage(Lang.Get(ConstantsCore.ModId + ":cage-captured"));
                            entity.Die(EnumDespawnReason.Removed);
                        }

                        Die();
                    }
                }
            }

            beforeCollided = false;
            motionBeforeCollide = SidedPos.Motion.Clone();
        }

        private void CollideCheck()
        {
            if (Collided)
            {
                if (!beforeCollided && World is IServerWorldAccessor)
                {
                    float strength = GameMath.Clamp((float)motionBeforeCollide.Length() * 4, 0, 1);

                    if (CollidedHorizontally)
                    {
                        SidedPos.Motion.X = motionBeforeCollide.X * 0.8f;
                        SidedPos.Motion.Z = motionBeforeCollide.Z * 0.8f;
                    }

                    if (CollidedVertically && motionBeforeCollide.Y <= 0)
                    {
                        SidedPos.Motion.Y = GameMath.Clamp(motionBeforeCollide.Y * -0.4f, -0.1f, 0.1f);
                    }

                    World.PlaySoundAt(new AssetLocation("game:sounds/thud"), this, null, false, 32, strength);
                    WatchedAttributes.MarkAllDirty();
                }

                beforeCollided = true;
                return;
            }
        }

        public override bool CanCollect(Entity byEntity)
        {
            return Alive && World.ElapsedMilliseconds - launchMs > 1000 && ServerPos.Motion.Length() < 0.01;
        }

        public override ItemStack OnCollected(Entity byEntity)
        {
            ProjectileStack.ResolveBlockOrItem(World);
            return ProjectileStack;
        }

        public override void OnCollided()
        {
            if (((ItemCage)ProjectileStack.Item)?.IsFull != true) return;

            Entity entity = EntityUtil.EntityFromAttributes(ProjectileStack.Attributes, "capture", Api.World);
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
                FiredBy.SendMessage("Undefined entity in cage!");
                Die();
            }
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            base.ToBytes(writer, forClient);

            writer.Write(beforeCollided);
            ProjectileStack.ToBytes(writer);
        }

        public override void FromBytes(BinaryReader reader, bool fromServer)
        {
            base.FromBytes(reader, fromServer);

            beforeCollided = reader.ReadBoolean();
            ProjectileStack = World == null ? new ItemStack(reader) : new ItemStack(reader, World);
        }
    }
}

