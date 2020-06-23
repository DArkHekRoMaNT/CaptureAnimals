using System;
using Vintagestory.API.Common;

namespace CaptureAnimals
{
    public class CaptureAnimals : ModSystem
    {
        public static string MOD_ID = "captureanimals";
        public static string MOD_SPACE = "CaptureAnimals";

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            string[] items = {
                "ItemCage"
            };
            string[] entities = {
                "EntityThrownCage"
            };

            foreach (string e in items)
            {
                api.RegisterItemClass(e, Type.GetType(MOD_SPACE + "." + e));
            }
            foreach (string e in entities)
            {
                api.RegisterEntity(e, Type.GetType(MOD_SPACE + "." + e));
            }
        }
    }
}