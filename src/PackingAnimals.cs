using Vintagestory.API.Common;
using System;

namespace PackingAnimals
{
    public class PackingAnimals : ModSystem
    {
        public static string MOD_ID = "packinganimals";

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            string[] items = {
                "ItemPack"
            };

            foreach (string e in items)
            {
                api.RegisterItemClass(e, Type.GetType(e));
            }
        }
    }
}