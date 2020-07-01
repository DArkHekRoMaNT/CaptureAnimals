using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace CaptureAnimals
{
    public class CaptureAnimalsRecipes : ModSystem
    {
        ICoreServerAPI api;
        Dictionary<AssetLocation, GridRecipe> gridRecipes = new Dictionary<AssetLocation, GridRecipe>();

        public override double ExecuteOrder()
        {
            return 1;
        }

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            api.Event.SaveGameLoaded += OnSaveGameLoaded;
        }

        public void OnSaveGameLoaded()
        {
            AddRecipes();
            LoadGridRecipes();
        }

        struct Bait
        {
            public string type;
            public string code;
            public float chance;
            public float minHealth;
        };

        public void AddRecipes()
        {
            List<EntityProperties> types = api.World.EntityTypes;
            Dictionary<Bait, List<string>> baitForAnimals = new Dictionary<Bait, List<string>>(); // Key = bait, Value = animals
            foreach (var type in types)
            {
                if (type.Class == "EntityAgent" && type.Attributes != null)
                {
                    if (type.Attributes[CaptureAnimals.MOD_ID]["type"].Exists &&
                        type.Attributes[CaptureAnimals.MOD_ID]["code"].Exists &&
                        type.Attributes[CaptureAnimals.MOD_ID]["chance"].Exists &&
                        type.Attributes[CaptureAnimals.MOD_ID]["minhealth"].Exists)
                    {
                        Bait bait = new Bait
                        {
                            type = type.Attributes[CaptureAnimals.MOD_ID]["type"].AsString(),
                            code = type.Attributes[CaptureAnimals.MOD_ID]["code"].AsString(),
                            chance = type.Attributes[CaptureAnimals.MOD_ID]["chance"].AsFloat(),
                            minHealth = type.Attributes[CaptureAnimals.MOD_ID]["minhealth"].AsFloat(),
                        };

                        string animal = type.Code.Domain + type.Code.Path;

                        if (baitForAnimals.ContainsKey(bait))
                        {
                            baitForAnimals[bait].Add(animal);
                        }
                        else
                        {
                            baitForAnimals.Add(bait, new List<string> { animal });
                        }
                    }
                }
            }

            string cageVariant = Util.FirstCodeMapping("item", CaptureAnimals.MOD_ID + ":cage-*-empty", api.World);
            AssetLocation cageLoc = new AssetLocation(cageVariant);
            foreach (var val in baitForAnimals) {
                List<string> codes = new List<string>();
                codes = Util.CodeMapping(val.Key.type, val.Key.code, api.World);

                foreach (var code in codes)
                {
                    string postfix = val.Key.type + "-" + code.Replace(':', '-');
                    AssetLocation recipeName = new AssetLocation(CaptureAnimals.MOD_ID + ":cage-with-" + postfix);

                    ItemStack output = new ItemStack(api.World.GetItem(cageLoc));
                    output.Attributes.SetString("animals", Util.ListStrToStr(val.Value));
                    output.Attributes.SetString("bait-type", val.Key.type);
                    output.Attributes.SetString("bait-code", code);
                    output.Attributes.SetFloat("bait-chance", val.Key.chance);
                    output.Attributes.SetFloat("bait-minhealth", val.Key.minHealth);

                    GridRecipe recipe = new GridRecipe
                    {
                        IngredientPattern = "CB",
                        Ingredients = new Dictionary<string, CraftingRecipeIngredient>(),
                        Width = 2,
                        Height = 1,
                        Shapeless = true,
                        Output = new CraftingRecipeIngredient
                        {
                            Type = EnumItemClass.Item,
                            Code = new AssetLocation(CaptureAnimals.MOD_ID + ":cage-{type}-empty"),
                            Attributes = new JsonObject(JToken.Parse(output.Attributes.ToJsonToken()))
                        }
                    };
                    recipe.Ingredients.Add("C", new CraftingRecipeIngredient
                    {
                        Type = EnumItemClass.Item,
                        Code = new AssetLocation(CaptureAnimals.MOD_ID + ":cage-*-empty"),
                        Name = "type"
                    });
                    recipe.Ingredients.Add("B", new CraftingRecipeIngredient
                    {
                        Type = EnumItemClass.Item,
                        Code = new AssetLocation(val.Key.code)
                    });

                    gridRecipes.Add(recipeName, recipe);
                }
            }
        }

        public void LoadGridRecipes()
        {
            int recipeQuantity = 0;

            foreach (var val in gridRecipes)
            {
                LoadRecipe(val.Key, val.Value, ref recipeQuantity);
            }

            api.World.Logger.Event("{0} cage-with-bait recipes created by CaptureAnimals", recipeQuantity);
            api.World.Logger.StoryEvent(Lang.Get("Animal cages..."));
        }

        public void LoadRecipe(AssetLocation loc, GridRecipe recipe, ref int recipeQuantity)
        {
            if (!recipe.Enabled) return;
            if (recipe.Name == null) recipe.Name = loc;

            Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(api.World);

            if (nameToCodeMapping.Count > 0)
            {
                List<GridRecipe> subRecipes = new List<GridRecipe>();

                int qCombs = 0;
                bool first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    if (first) qCombs = val2.Value.Length;
                    else qCombs *= val2.Value.Length;
                    first = false;
                }

                first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    string variantCode = val2.Key;
                    string[] variants = val2.Value;

                    for (int i = 0; i < qCombs; i++)
                    {
                        GridRecipe rec;

                        if (first) subRecipes.Add(rec = recipe.Clone());
                        else rec = subRecipes[i];

                        foreach (CraftingRecipeIngredient ingred in rec.Ingredients.Values)
                        {
                            if (ingred.Name == variantCode)
                            {
                                ingred.Code.Path = ingred.Code.Path.Replace("*", variants[i % variants.Length]);
                            }
                        }

                        rec.Output.FillPlaceHolder(variantCode, variants[i % variants.Length]);
                    }

                    first = false;
                }

                foreach (GridRecipe subRecipe in subRecipes)
                {
                    if (!subRecipe.ResolveIngredients(api.World)) continue;
                    api.RegisterCraftingRecipe(subRecipe);
                    recipeQuantity++;
                }

            }
            else
            {
                if (!recipe.ResolveIngredients(api.World)) return;
                api.RegisterCraftingRecipe(recipe);
                recipeQuantity++;
            }

        }

        void HandlePredicate(EntityProperties obj)
        {
        }


        public void TestRecipe()
        {
            GridRecipe recipe = new GridRecipe
            {
                IngredientPattern = "C,B",
                Ingredients = new Dictionary<string, CraftingRecipeIngredient>(),
                Width = 1,
                Height = 2,
                Shapeless = true,
                Output = new CraftingRecipeIngredient
                {
                    Type = EnumItemClass.Item,
                    Code = new AssetLocation(CaptureAnimals.MOD_ID + ":cage-{type}-full")
                },
                Name = new AssetLocation(CaptureAnimals.MOD_ID + ":cage-{type}-bait")
            };
            recipe.Ingredients.Add("C", new CraftingRecipeIngredient
            {
                Type = EnumItemClass.Item,
                Code = new AssetLocation(CaptureAnimals.MOD_ID + ":cage-*-empty"),
                Name = "type"
            });
            recipe.Ingredients.Add("B", new CraftingRecipeIngredient
            {
                Type = EnumItemClass.Item,
                Code = new AssetLocation("game:vegetable-turnip")
            });
            recipe.ResolveIngredients((api as ICoreAPI).World);
            api.RegisterCraftingRecipe(recipe);

            //ingredientPattern: "P,C",
            //ingredients:
            //        {
            //            "P": { type: "item", code: "game:resin" },
            //    "C": { type: "item", code: "cage-*-case", name: "type" }
            //        },
            //width: 1,
            //height: 2,
            //shapeless: true,
            //output: { type: "item", code: "cage-{type}-empty" }
        }
    }
}