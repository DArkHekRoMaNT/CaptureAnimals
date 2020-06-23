using System;
using Vintagestory.API.Common;

namespace PackingAnimals
{
    public class PackingAnimals : ModSystem
    {
        public static string MOD_ID = "packinganimals";
        public static string MOD_SPACE = "PackingAnimals";

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            string[] items = {
                "ItemPack"
            };

            foreach (string e in items)
            {
                api.RegisterItemClass(e, Type.GetType(MOD_SPACE + "." + e));
            }
        }
    }
}