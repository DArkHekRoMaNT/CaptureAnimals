using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CaptureAnimals
{
    public static class EntityUtil
    {
        public static void ToAttributes(this Entity entity, ItemStack stack, string key)
        {
            using var ms = new MemoryStream();
            {
                var writer = new BinaryWriter(ms);
                ICoreAPI api = entity.Api;

                writer.Write(api.World.ClassRegistry.GetEntityClassName(entity.GetType()));
                entity.ToBytes(writer, false);

                string value = Ascii85.Encode(ms.ToArray());
                stack.Attributes.SetString(key, value);
            }
        }

        public static Entity EntityFromAttributes(ITreeAttribute tree, string key, IWorldAccessor world)
        {
            string value = tree.GetString(key, null);
            if (value is null)
            {
                return null;
            }

            using var ms = new MemoryStream(Ascii85.Decode(value));
            {
                var reader = new BinaryReader(ms);

                string className = reader.ReadString();
                Entity entity = world.ClassRegistry.CreateEntity(className);

                entity.FromBytes(reader, false);
                return entity;
            }
        }
    }
}

