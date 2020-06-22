using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace PackingAnimals
{
    public class ItemPack : Item
    {
        protected void SendMessage(string msg, EntityAgent byEntity)
        {
            IPlayer byPlayer = api.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);
            if (api.Side == EnumAppSide.Server)
            {
                IServerPlayer sp = byPlayer as IServerPlayer;
                sp.SendMessage(GlobalConstants.InfoLogChatGroup, msg, EnumChatType.Notification);
            }
            else
            {
                //IClientPlayer cp = byPlayer as IClientPlayer;
                //cp.ShowChatNotification(msg);
            }
        }

    }
}