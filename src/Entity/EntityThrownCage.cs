using System;
using System.IO;
using SharedUtils;
using SharedUtils.Extensions;
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
        public Core Core { get; private set; }

        bool beforeCollided;

        long launchMs;
        Vec3d motionBeforeCollide = new Vec3d();

        public Entity FiredBy;
        public float Damage;
        public ItemStack ProjectileStack;

        readonly Random random = new Random();

        public override bool IsInteractable => false;


        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            launchMs = World.ElapsedMilliseconds;

            if (ProjectileStack?.Collectible != null)
            {
                ProjectileStack.ResolveBlockOrItem(World);
            }

            Core = api.ModLoader.GetModSystem<Core>();

            AnimManager.StartAnimation("idle");
            AnimManager.StartAnimation("rotating");
            AnimManager.StartAnimation("circle-small");
            AnimManager.StartAnimation("circle-middle");
            AnimManager.StartAnimation("circle-large");
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


            EntityPos pos = SidedPos;

            pos.Pitch = World.ElapsedMilliseconds / 250f % GameMath.TWOPI;
            pos.Roll = World.ElapsedMilliseconds / 250f % GameMath.TWOPI;
            pos.Yaw = World.ElapsedMilliseconds / 250f % GameMath.TWOPI;

            if (Collided)
            {
                if (!beforeCollided && World is IServerWorldAccessor)
                {
                    float strength = GameMath.Clamp((float)motionBeforeCollide.Length() * 4, 0, 1);

                    if (CollidedHorizontally)
                    {
                        pos.Motion.X = motionBeforeCollide.X * 0.8f;
                        pos.Motion.Z = motionBeforeCollide.Z * 0.8f;
                    }

                    if (CollidedVertically && motionBeforeCollide.Y <= 0)
                    {
                        pos.Motion.Y = GameMath.Clamp(motionBeforeCollide.Y * -0.4f, -0.1f, 0.1f);
                    }

                    World.PlaySoundAt(new AssetLocation("game:sounds/thud"), this, null, false, 32, strength);
                    WatchedAttributes.MarkAllDirty();
                }

                beforeCollided = true;
                return;
            }

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
                        if (baitStack != null && Core.AllBaits.TryGetValue(baitStack.Collectible.Code, out var captureEntities))
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

                        if (captureChance < random.NextDouble())
                        {
                            float breakChance = ProjectileStack.Collectible.Attributes["breakchance"].AsFloat();
                            if (breakChance >= random.NextDouble())
                            {
                                FiredBy.SendMessage(Lang.Get(ConstantsCore.ModId + ":cage-broken"));
                            }
                            else
                            {
                                FiredBy.SendMessage(Lang.Get(ConstantsCore.ModId + ":cage-mistake"));

                                //Api.World.SpawnItemEntity(ProjectileStack, ServerPos.XYZ);
                                AssetLocation caseCode = ProjectileStack.Collectible.CodeWithVariant("type", "case");
                                ItemStack caseStack = new ItemStack(Api.World.GetItem(caseCode));
                                Api.World.SpawnItemEntity(caseStack, ServerPos.XYZ);
                            }
                        }
                        else
                        {
                            AssetLocation location = ProjectileStack.Item?.CodeWithVariant("type", "full");
                            ItemStack full = new ItemStack(Api.World.GetItem(location));

                            SaveEntityInAttributes(entity, full, "capture");
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
            motionBeforeCollide.Set(pos.Motion.X, pos.Motion.Y, pos.Motion.Z);
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

            Entity entity = GetEntityFromAttributes(ProjectileStack, "capture", Api.World);
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


        public void SaveEntityInAttributes(Entity entity, ItemStack stack, string key)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);
                ICoreAPI api = entity.Api;

                writer.Write(api.World.ClassRegistry.GetEntityClassName(entity.GetType()));
                entity.ToBytes(writer, false);

                string value = Ascii85.Encode(ms.ToArray());
                stack.Attributes.SetString(key, value);
            }
        }
        public Entity GetEntityFromAttributes(ItemStack stack, string key, IWorldAccessor world)
        {
            string value = null;
            if (stack.Attributes.HasAttribute(key))
            {
                value = stack.Attributes.GetString(key);
            }
            if (value == null) return null;

            using (MemoryStream ms = new MemoryStream(Ascii85.Decode(value)))
            {
                BinaryReader reader = new BinaryReader(ms);

                string className = reader.ReadString();
                Entity entity = world.ClassRegistry.CreateEntity(className);

                entity.FromBytes(reader, false);
                return entity;
            }
        }
    }
}

