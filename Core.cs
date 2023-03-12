using Vintagestory.API.Common;

namespace CaptureAnimals
{
    public class Core : ModSystem
    {
        public override void StartPre(ICoreAPI api)
        {
            api.RegisterItemClass("ItemCage", typeof(ItemCage));
            api.RegisterEntity("EntityThrownCage", typeof(EntityThrownCage));
        }
    }
}