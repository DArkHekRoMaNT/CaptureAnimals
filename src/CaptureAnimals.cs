using System;
using Vintagestory.API.Common;

[assembly: ModInfo("CaptureAnimals")]

namespace CaptureAnimals
{
    public class CaptureAnimals : ModSystem
    {
        public static string MOD_ID = "captureanimals";
        public static string MOD_SPACE = "CaptureAnimals";

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterItemClass("ItemCage", Type.GetType(MOD_SPACE + ".ItemCage"));
            api.RegisterBlockClass("BlockCage", Type.GetType(MOD_SPACE + ".BlockCage"));
            api.RegisterEntity("EntityThrownCage", Type.GetType(MOD_SPACE + ".EntityThrownCage"));
            api.RegisterBlockEntityClass("BlockEntityCage", Type.GetType(MOD_SPACE + ".BlockEntityCage"));
        }
    }
}