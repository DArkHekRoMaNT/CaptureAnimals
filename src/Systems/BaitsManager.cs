using SharedUtils;
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

        IServerNetworkChannel serverChannel;
        ICoreAPI api;

        public override double ExecuteOrder() => 1;

        public override void StartPre(ICoreAPI api)
        {
            this.api = api;

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
            IAsset baitsAsset = api.Assets.Get(ConstantsCore.ModId + ":config/baits.json");
            ResolveBaits(api, baitsAsset?.ToObject<Bait[]>());

            api.Event.PlayerJoin += (IServerPlayer byPlayer) =>
            {
                serverChannel.SendPacket(AllBaits, byPlayer);
            };
        }

        private void OnReceiveAllBaits(Dictionary<AssetLocation, List<CaptureEntity>> networkMessage)
        {
            AllBaits = networkMessage;
        }

        private void ResolveBaits(ICoreServerAPI api, Bait[] baits)
        {
            if (baits != null)
            {
                foreach (var bait in baits)
                {
                    var entities = ResolveCaptureEntities(bait);
                    var codes = ResolveBaitCodes(bait);

                    foreach (var code in codes)
                    {
                        AllBaits.Add(code, entities);
                    }
                }
            }

            api.World.Logger.Event("{0} baits loaded [CaptureAnimals]", AllBaits.Count);
            api.World.Logger.StoryEvent(Lang.Get("Capturing animals..."));
        }

        private List<CaptureEntity> ResolveCaptureEntities(Bait bait)
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

        private List<AssetLocation> ResolveBaitCodes(Bait bait)
        {
            var codes = new List<AssetLocation>();

            var key = new AssetLocation(bait.Code);
            if (!key.IsWildCard)
            {
                codes.Add(key);
            }
            else
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

            return codes;
        }

    }
}