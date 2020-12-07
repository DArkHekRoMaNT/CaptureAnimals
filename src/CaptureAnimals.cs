using Vintagestory.API.Common;

namespace CaptureAnimals
{
    public class CaptureAnimals : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterItemClass("ItemCage", typeof(ItemCage));
            api.RegisterEntity("EntityThrownCage", typeof(EntityThrownCage));
        }
    }
}