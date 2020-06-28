using System.IO;
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
        bool beforeCollided;
        bool stuck;

        long msLaunch;
        Vec3d motionBeforeCollide = new Vec3d();

        CollisionTester collTester = new CollisionTester();

        public Entity FiredBy;
        internal float Damage;
        public ItemStack ProjectileStack;

        public override bool IsInteractable
        {
            get { return false; }
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            msLaunch = World.ElapsedMilliseconds;

            if (ProjectileStack?.Collectible != null)
            {
                ProjectileStack.ResolveBlockOrItem(World);
            }
        }


        //TODO: Убрать уничтожение при попадании в стену. Проверить хитбокс, т.к. нельзя попасть в дырку 1х1
        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (OnGround || ShouldDespawn) return;

            EntityPos pos = LocalPos;

            stuck = Collided;
            if (stuck)
            {
                pos.Pitch = GameMath.PIHALF;
                pos.Roll = 0;
                pos.Yaw = GameMath.PIHALF;
            }
            else
            {
                pos.Pitch = (World.ElapsedMilliseconds / 300f) % GameMath.TWOPI;
                pos.Roll = 0;
                pos.Yaw = (World.ElapsedMilliseconds / 400f) % GameMath.TWOPI;
            }

            if (stuck)
            {
                if (!beforeCollided && World is IServerWorldAccessor)
                {
                    float strength = GameMath.Clamp((float)motionBeforeCollide.Length() * 4, 0, 1);

                    if (CollidedHorizontally)
                    {
                        pos.Motion.X = motionBeforeCollide.X * 0.8f;
                        pos.Motion.Z = motionBeforeCollide.Z * 0.8f;

                        if (strength > 0.08f && World.Rand.NextDouble() > 0.2f)
                        {
                            World.SpawnCubeParticles(LocalPos.XYZ.OffsetCopy(0, 0.2, 0), ProjectileStack, 0.2f, 20);
                            Die();
                        }
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

            if (World is IServerWorldAccessor && ProjectileStack.Item?.LastCodePart() == "empty")
            {
                Entity entity = World.GetNearestEntity(ServerPos.XYZ, 5f, 5f, (e) => {
                    if (e.EntityId == this.EntityId || (FiredBy != null && e.EntityId == FiredBy.EntityId && World.ElapsedMilliseconds - msLaunch < 500) || !e.IsInteractable)
                    {
                        return false;
                    }

                    double dist = e.CollisionBox.ToDouble().Translate(e.ServerPos.X, e.ServerPos.Y, e.ServerPos.Z).ShortestDistanceFrom(ServerPos.X, ServerPos.Y, ServerPos.Z);
                    return dist < 0.5f;
                });

                if (entity != null && (entity as EntityPlayer) == null && entity.Alive)
                {
                    bool didDamage = entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Entity, SourceEntity = FiredBy == null ? this : FiredBy, Type = EnumDamageType.BluntAttack }, Damage);
                    World.PlaySoundAt(new AssetLocation("game:sounds/thud"), this, null, false, 32);
                    World.SpawnCubeParticles(entity.LocalPos.XYZ.OffsetCopy(0, 0.2, 0), ProjectileStack, 0.2f, 20);

                    if (FiredBy is EntityPlayer && didDamage)
                    {
                        World.PlaySoundFor(new AssetLocation("game:sounds/player/projectilehit"), (FiredBy as EntityPlayer).Player, false, 24);
                    }

                    if (entity.GetBehavior("health") is EntityBehaviorHealth behavior)
                    {
                        float chanceMin = ProjectileStack.Collectible.Attributes["defaultchance"]["min"].AsFloat();
                        float maxChanceHealth = ProjectileStack.Collectible.Attributes["defaultchance"]["maxchancehealth"].AsFloat();

                        string name = entity.GetName();
                        string test = entity.Attributes.GetString("ttt");

                        Util.SendMessage("ttt: " + test, Api, FiredBy);

                        if (behavior.Health / behavior.MaxHealth <= 1 || behavior.Health <= 1)
                        {
                            AssetLocation location = ProjectileStack.Item?.CodeWithVariant("type", "full");
                            ItemStack full = new ItemStack(Api.World.GetItem(location));

                            Util.SaveEntityInAttributes(entity, full, "capture");
                            full.Attributes.SetString("capturename", entity.GetName());

                            Api.World.SpawnItemEntity(full, entity.Pos.XYZ);

                            entity.Die(EnumDespawnReason.Removed);
                            Die();
                        }
                        else
                        {
                            Util.SendMessage(Lang.Get(CaptureAnimals.MOD_ID + ":error-too-much-hp"), Api, FiredBy);
                            Api.World.SpawnItemEntity(ProjectileStack, entity.Pos.XYZ);
                            Die();
                        }
                    }

                    return;
                }
            }

            beforeCollided = false;
            motionBeforeCollide.Set(pos.Motion.X, pos.Motion.Y, pos.Motion.Z);

            SetRotation();
        }


        public virtual void SetRotation()
        {
            EntityPos pos = (World is IServerWorldAccessor) ? ServerPos : Pos;

            double speed = pos.Motion.Length();

            if (CollisionBox == null) return;
            CollisionBox.X1 = -0.5f;
            CollisionBox.X2 = 0.5f;
            CollisionBox.Z1 = -0.5f;
            CollisionBox.Z2 = 0.5f;
            CollisionBox.Y1 = 0f;
            CollisionBox.Y2 = 0.5f;
        }


        public override bool CanCollect(Entity byEntity)
        {
            return Alive && World.ElapsedMilliseconds - msLaunch > 1000 && ServerPos.Motion.Length() < 0.01;
        }

        public override ItemStack OnCollected(Entity byEntity)
        {
            ProjectileStack.ResolveBlockOrItem(World);
            return ProjectileStack;
        }

        public override void OnCollided()
        {
            if (ProjectileStack.Item?.LastCodePart() != "full") return;

            Entity entity = Util.GetEntityFromAttributes(ProjectileStack, "capture", Api.World);
            if(entity != null)
            {
                entity.Pos.SetPos(Pos);
                entity.ServerPos.SetPos(ServerPos);
                entity.PositionBeforeFalling.Set(Pos.XYZ);

                Api.World.SpawnEntity(entity);
                Die();
            }
            else
            {
                Util.SendMessage("Undefined entity in cage!", Api, FiredBy as EntityAgent);
                Die();
            }
        }

        public override void OnCollideWithLiquid()
        {
            if (motionBeforeCollide.Y <= 0)
            {
                LocalPos.Motion.Y = GameMath.Clamp(motionBeforeCollide.Y * -0.5f, -0.1f, 0.1f);
                PositionBeforeFalling.Y = Pos.Y + 1;
            }

            base.OnCollideWithLiquid();
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

