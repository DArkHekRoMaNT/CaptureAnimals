using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CaptureAnimals
{
    public static class Util
    {
        public static void SaveEntityInAttributes(Entity entity, ItemStack stack, string key)
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
        public static Entity GetEntityFromAttributes(ItemStack stack, string key, IWorldAccessor world)
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


        public static void SendMessage(string msg, ICoreAPI api, EntityAgent byEntity = null)
        {
            IPlayer byPlayer = api.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
            if(byPlayer == null)
            {
                api.World.Logger.Chat(msg);
            }
            else if (api.Side == EnumAppSide.Server)
            {
                IServerPlayer sp = byPlayer as IServerPlayer;
                sp.SendMessage(GlobalConstants.InfoLogChatGroup, msg, EnumChatType.Notification);
                api.World.Logger.Chat(msg);
            }
            else
            {
                IClientPlayer cp = byPlayer as IClientPlayer;
                cp.ShowChatNotification(msg);
                api.World.Logger.Chat(msg);
            }
        }
    }
}
