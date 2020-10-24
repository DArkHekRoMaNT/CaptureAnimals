using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace CaptureAnimals
{
    public class BlockEntityCage : BlockEntity
    {
        BlockCage ownBlock;
        readonly float findRange = 10f;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            ownBlock = Block as BlockCage;

            if(api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnEvery10Sec, 10000);
            }
        }
        private void OnEvery10Sec(float dt)
        {
            Entity entity = Api.World.GetNearestEntity(Pos.ToVec3d(), findRange, findRange, (e) =>
            {
                if (!e.IsInteractable)
                {
                    return false;
                }
                return true;
            });
            Util.SendMessageAll("BECage ticked in: " + Util.HumanCoord(Pos.ToVec3d(), Api) + "\nFind entity at " + Util.HumanCoord(entity?.Pos.XYZ, Api) + "\nName: " + entity?.GetName(), Api, GlobalConstants.AllChatGroups);
        }
    }
}
