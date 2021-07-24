using System;
using System.Collections.Generic;
using ProtoBuf;
using SharedUtils;
using SharedUtils.Extensions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace CaptureAnimals
{
    public class Core : ModSystem
    {
        public Dictionary<AssetLocation, List<CaptureEntity>> AllBaits { get; private set; } = new Dictionary<AssetLocation, List<CaptureEntity>>();

        IServerNetworkChannel serverChannel;

        public override double ExecuteOrder() => 1;
        public override void StartPre(ICoreAPI api)
        {
            api.RegisterItemClass("ItemCage", typeof(ItemCage));
            api.RegisterEntity("EntityThrownCage", typeof(EntityThrownCage));

            if (api.Side == EnumAppSide.Client)
            {
                (api as ICoreClientAPI).Network
                    .RegisterChannel(ConstantsCore.ModId)
                    .RegisterMessageType<Dictionary<AssetLocation, List<CaptureEntity>>>()
                    .SetMessageHandler<Dictionary<AssetLocation, List<CaptureEntity>>>(OnReceiveAllBaits);
            }
            else
            {
                serverChannel = (api as ICoreServerAPI).Network
                    .RegisterChannel(ConstantsCore.ModId)
                    .RegisterMessageType<Dictionary<AssetLocation, List<CaptureEntity>>>();
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            CollectAndResolveBaits(api);
            api.Event.PlayerJoin += (IServerPlayer byPlayer) =>
            {
                serverChannel.SendPacket(AllBaits, byPlayer);
            };
        }

        private void OnReceiveAllBaits(Dictionary<AssetLocation, List<CaptureEntity>> networkMessage)
        {
            AllBaits = networkMessage;
        }

        private void CollectAndResolveBaits(ICoreServerAPI api)
        {
            IAsset baitsAsset = api.Assets.Get(ConstantsCore.ModId + ":config/baits.json");
            var baits = baitsAsset?.ToObject<Bait[]>();

            if (baits != null)
            {
                foreach (var bait in baits)
                {
                    var resolvedCaptureEntities = new List<CaptureEntity>();

                    foreach (var captureEntity in bait.Entities)
                    {
                        var captureEntityCode = new AssetLocation(captureEntity.Code);
                        if (!captureEntityCode.IsWildCard)
                        {
                            resolvedCaptureEntities.Add(captureEntity);
                        }
                        else
                        {
                            foreach (var entityType in api.World.EntityTypes)
                            {
                                if (WildcardUtil.Match(captureEntityCode, entityType.Code))
                                {
                                    resolvedCaptureEntities.Add(captureEntity.WithCode(entityType.Code));
                                }
                            }
                        }
                    }


                    var resolvedBaitCodes = new List<AssetLocation>();
                    var baitCode = new AssetLocation(bait.Code);

                    if (!baitCode.IsWildCard)
                    {
                        resolvedBaitCodes.Add(baitCode);
                    }
                    else
                    {
                        if (bait.Type == "item")
                        {
                            foreach (var item in api.World.Items)
                            {
                                if (item.WildCardMatch(baitCode))
                                {
                                    resolvedBaitCodes.Add(item.Code);
                                }
                            }
                        }
                        else
                        {
                            foreach (var block in api.World.Blocks)
                            {
                                if (block.WildCardMatch(baitCode))
                                {
                                    resolvedBaitCodes.Add(block.Code);
                                }
                            }
                        }
                    }

                    foreach (var resolvedBaitCode in resolvedBaitCodes)
                    {
                        AllBaits.Add(resolvedBaitCode, resolvedCaptureEntities);
                    }
                }
            }

            api.World.Logger.Event("{0} baits loaded [CaptureAnimals]", AllBaits.Count);
            api.World.Logger.StoryEvent(Lang.Get("Capturing animals..."));
        }
    }

    public class Bait
    {
        public string Type { get; set; }
        public string Code { get; set; }
        public CaptureEntity[] Entities { get; set; }
    }

    [ProtoContract]
    public class CaptureEntity
    {
        [ProtoMember(1)]
        public string Code { get; set; }

        [ProtoMember(2)]
        public float CaptureChance { get; set; }

        public CaptureEntity WithCode(AssetLocation code)
        {
            return new CaptureEntity()
            {
                Code = code + "",
                CaptureChance = CaptureChance
            };
        }
    }
}