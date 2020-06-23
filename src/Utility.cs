using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace CaptureAnimals
{
    public static class Util
    {
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
