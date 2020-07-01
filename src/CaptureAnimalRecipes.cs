using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

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

        public void AddRecipes()
        {
            List<EntityProperties> types = api.World.EntityTypes;
            Dictionary<string, List<string>> baitForAnimals = new Dictionary<string, List<string>>(); // Key = bait, Value = animals
            foreach (var type in types)
            {
                if (type.Class == "EntityAgent" && type.Attributes != null)
                {
                    if (type.Attributes[CaptureAnimals.MOD_ID]["bait"].Exists &&
                        type.Attributes[CaptureAnimals.MOD_ID]["chance"].Exists &&
                        type.Attributes[CaptureAnimals.MOD_ID]["chance"]["min"].Exists &&
                        type.Attributes[CaptureAnimals.MOD_ID]["chance"]["maxchancehealth"].Exists)
                    {
                        string bait = type.Attributes[CaptureAnimals.MOD_ID]["bait"].AsString();
                        List<string> animals = new List<string>
                        {
                            type.Code.Domain + type.Code.Path
                        };

                        if (baitForAnimals.ContainsKey(bait))
                        {
                            animals.AddRange(baitForAnimals[bait]);
                            baitForAnimals[bait] = animals;
                        }
                        else
                        {
                            baitForAnimals.Add(bait, animals);
                        }
                    }
                }
            }        

            foreach (var val in baitForAnimals) {
                AssetLocation loc = new AssetLocation(CaptureAnimals.MOD_ID + ":cage-bait-" + val.Key.Replace(':', '-'));

                AssetLocation asset = new AssetLocation(CaptureAnimals.MOD_ID + ":cage-copper-empty");
                ItemStack output = new ItemStack(api.World.GetItem(asset));
                output.Attributes.SetString("bait", val.Key);
                output.Attributes.SetString("animals", Util.ListStrToStr(val.Value));

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
                        Attributes = new JsonObject(output.Attributes.ToJsonToken())
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
                    Code = new AssetLocation(val.Key),
                    Name = "bait"
                });

                gridRecipes.Add(loc, recipe);
            }
        }

        public void LoadGridRecipes()
        {
            int recipeQuantity = 0;

            foreach (var val in gridRecipes)
            {
                LoadRecipe(val.Key, val.Value, ref recipeQuantity);
            }

            api.World.Logger.Event("{0} cage with bait recipes created by CaptureAnimals", recipeQuantity);
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