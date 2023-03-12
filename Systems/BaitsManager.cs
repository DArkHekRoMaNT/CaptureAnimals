using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace CaptureAnimals
{
    public class BaitsManager : ModSystem
    {
        public Dictionary<AssetLocation, List<CaptureEntity>> AllBaits { get; private set; } = new();

        private IServerNetworkChannel? _serverChannel;

        public override double ExecuteOrder() => 1;

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Network
                .RegisterChannel(Constants.BaitsManagerNetworkChannelName)
                .RegisterMessageType<AllBaitsSyncPacket>()
                .SetMessageHandler<AllBaitsSyncPacket>(OnReceiveAllBaitsSyncPacket);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            _serverChannel = api.Network
                .RegisterChannel(Constants.BaitsManagerNetworkChannelName)
                .RegisterMessageType<AllBaitsSyncPacket>();

            api.Event.PlayerJoin += SendAllBaits;

            IAsset baitsAsset = api.Assets.Get(Constants.ModId + ":config/baits.json");
            ResolveBaits(api, baitsAsset?.ToObject<Bait[]>());
        }

        private void SendAllBaits(IServerPlayer forPlayer)
        {
            _serverChannel!.SendPacket(new AllBaitsSyncPacket
            {
                AllBaits = AllBaits
            }, forPlayer);
        }

        private void OnReceiveAllBaitsSyncPacket(AllBaitsSyncPacket networkMessage)
        {
            AllBaits = networkMessage.AllBaits;
        }

        private void ResolveBaits(ICoreServerAPI api, Bait[]? baits)
        {
            if (baits != null)
            {
                foreach (var bait in baits)
                {
                    var entities = ResolveCaptureEntities(api, bait);
                    var codes = ResolveBaitCodes(api, bait);

                    foreach (var code in codes)
                    {
                        AllBaits.Add(code, entities);
                    }
                }
            }

            api.World.Logger.Event("{0} baits loaded [{1}]", AllBaits.Count, Mod.Info.ModID);
            api.World.Logger.StoryEvent(Lang.Get("Capturing animals..."));
        }

        private static List<CaptureEntity> ResolveCaptureEntities(ICoreAPI api, Bait bait)
        {
            var entities = new List<CaptureEntity>();
            foreach (var captureEntity in bait.Entities)
            {
                var captureEntityCode = new AssetLocation(captureEntity.Code);
                if (!captureEntityCode.IsWildCard)
                {
                    entities.Add(captureEntity);
                }
                else
                {
                    foreach (var entityType in api.World.EntityTypes)
                    {
                        if (WildcardUtil.Match(captureEntityCode, entityType.Code))
                        {
                            entities.Add(captureEntity.WithCode(entityType.Code));
                        }
                    }
                }
            }
            return entities;
        }

        private static List<AssetLocation> ResolveBaitCodes(ICoreAPI api, Bait bait)
        {
            var codes = new List<AssetLocation>();
            var key = new AssetLocation(bait.Code);
            if (key.IsWildCard)
            {
                if (bait.Type == "item")
                {
                    foreach (var item in api.World.Items)
                    {
                        if (item.WildCardMatch(key))
                        {
                            codes.Add(item.Code);
                        }
                    }
                }
                else
                {
                    foreach (var block in api.World.Blocks)
                    {
                        if (block.WildCardMatch(key))
                        {
                            codes.Add(block.Code);
                        }
                    }
                }
            }
            else
            {
                codes.Add(key);
            }
            return codes;
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        private struct AllBaitsSyncPacket
        {
            public Dictionary<AssetLocation, List<CaptureEntity>> AllBaits { get; set; }
        }


        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class CaptureEntity
        {
            public string Code { get; set; } = "";
            public float CaptureChance { get; set; } = 0;

            public CaptureEntity WithCode(AssetLocation code)
            {
                return new CaptureEntity()
                {
                    Code = code + "",
                    CaptureChance = CaptureChance
                };
            }
        }

        public class Bait
        {
            public string Type { get; set; } = "";
            public string Code { get; set; } = "";
            public CaptureEntity[] Entities { get; set; } = Array.Empty<CaptureEntity>();
        }
    }
}
