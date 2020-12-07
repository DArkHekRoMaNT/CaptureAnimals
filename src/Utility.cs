using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

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
        public static void SendMessage(string msg, ICoreAPI api, IPlayer player, int chatGroup = -1)
        {
            if (chatGroup == -1) chatGroup = GlobalConstants.InfoLogChatGroup;
            if (player == null)
            {
                api.World.Logger.Chat(msg);
            }
            else if (api.Side == EnumAppSide.Server)
            {
                IServerPlayer sp = player as IServerPlayer;
                sp.SendMessage(chatGroup, msg, EnumChatType.Notification);
                api.World.Logger.Chat(msg);
            }
            else
            {
                IClientPlayer cp = player as IClientPlayer;
                cp.ShowChatNotification(msg);
                api.World.Logger.Chat(msg);
            }
        }
        public static void SendMessage(string msg, ICoreAPI api, Entity playerEntity = null, int chatGroup = -1)
        {
            IPlayer player = api.World.PlayerByUid((playerEntity as EntityPlayer)?.PlayerUID);
            SendMessage(msg, api, player, chatGroup);
        }
        public static void SendMessage(string msg, Entity playerEntity, int chatGroup = -1)
        {
            SendMessage(msg, playerEntity.Api, playerEntity, chatGroup);
        }
        public static void SendMessageAll(string msg, ICoreAPI api, int chatGroup = -1)
        {
            IPlayer[] players = api.World.AllPlayers;
            foreach (IPlayer player in players)
            {
                SendMessage(msg, api, player, chatGroup);
            }
        }

        public const string separator = ", ";
        public static string ListStrToStr(List<string> list)
        {
            return String.Join(separator, list.ToArray());
        }
        public static List<string> StrToListStr(string str)
        {
            return str.Split(new string[] { separator }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        public static List<string> CodeMapping(string type, string code, IWorldAccessor world)
        {
            if (!code.Contains("*")) return new List<string> { code };

            List<string> codes = new List<string>();
            AssetLocation asset = new AssetLocation(code);
            int wildcardStartLen = asset.Path.IndexOf('*');
            int wildcardEndLen = asset.Path.Length - wildcardStartLen - 1;

            if (type == "block")
            {
                foreach (var block in world.Blocks)
                {
                    if (block == null || block.IsMissing || block.Code == null) continue;
                    if (WildcardUtil.Match(asset, block.Code))
                    {
                        string codeend = block.Code.Path.Substring(wildcardStartLen);
                        string codepart = codeend.Substring(0, codeend.Length - wildcardEndLen);
                        codes.Add(code.Replace("*", codepart));
                    }
                }
            }
            else
            {
                foreach (var item in world.Items)
                {
                    if (item == null || item.IsMissing || item.Code == null) continue;
                    if (WildcardUtil.Match(asset, item.Code))
                    {
                        string codeend = item.Code.Path.Substring(wildcardStartLen);
                        string codepart = codeend.Substring(0, codeend.Length - wildcardEndLen);
                        codes.Add(code.Replace("*", codepart));
                    }
                }
            }

            return codes;
        }

        public static string FirstCodeMapping(string type, string code, IWorldAccessor world)
        {
            if (!code.Contains("*")) return code;

            AssetLocation asset = new AssetLocation(code);
            int wildcardStartLen = asset.Path.IndexOf('*');
            int wildcardEndLen = asset.Path.Length - wildcardStartLen - 1;

            if (type == "block")
            {
                foreach (var block in world.Blocks)
                {
                    if (block == null || block.IsMissing || block.Code == null) continue;
                    if (WildcardUtil.Match(asset, block.Code))
                    {
                        string codeend = block.Code.Path.Substring(wildcardStartLen);
                        string codepart = codeend.Substring(0, codeend.Length - wildcardEndLen);
                        return code.Replace("*", codepart);
                    }
                }
            }
            else
            {
                foreach (var item in world.Items)
                {
                    if (item == null || item.IsMissing || item.Code == null) continue;
                    if (WildcardUtil.Match(asset, item.Code))
                    {
                        string codeend = item.Code.Path.Substring(wildcardStartLen);
                        string codepart = codeend.Substring(0, codeend.Length - wildcardEndLen);
                        return code.Replace("*", codepart);
                    }
                }
            }

            return null;
        }

        public enum GetLangType
        {
            ItemOrBlock = 0,
            Entity = 1
        }
        public static string GetLang(string type, string codeAndPath, GetLangType getLangType = GetLangType.ItemOrBlock)
        {
            int center = codeAndPath.IndexOf(AssetLocation.LocationSeparator);
            string domain = codeAndPath.Substring(0, center);
            string path = codeAndPath.Substring(center + 1, codeAndPath.Length - center - 1);

            switch (getLangType)
            {
                case GetLangType.ItemOrBlock:
                    return Lang.GetMatching(domain + AssetLocation.LocationSeparator + type + "-" + path);
                case GetLangType.Entity:
                    return Lang.GetMatching(domain + AssetLocation.LocationSeparator + "item-creature-" + path);
                default:
                    return null;
            }
        }
        public static Vec3d HumanCoord(Vec3d trueCoord, ICoreAPI api)
        {
            if (trueCoord == null) return null;

            double x = trueCoord.X - api.World.DefaultSpawnPosition.XYZ.Z;
            double y = trueCoord.Y;
            double z = trueCoord.Z - api.World.DefaultSpawnPosition.XYZ.Z;
            return new Vec3d(x, y, z);
        }
    }
}
