using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// [v2] เมนู Debug ตรวจราคาจานแยกแบบ เทียบกับชีตได้ (ไม่แก้ asset ใดๆ)
// Tools > Second Serving > Debug > Print Dish Price Table
// พิมพ์ทุกสูตรที่มีช่องหมวด × ทุกวัตถุดิบในหมวด: ราคาแบบ, H/S/M, ราคาขาย OK / Good / Perfect
public static class DishPriceDebugMenu
{
    [MenuItem("Tools/Second Serving/Debug/Print Dish Price Table")]
    public static void PrintDishPriceTable()
    {
        ItemDatabase items = LoadSingle<ItemDatabase>();
        RecipeDatabase recipes = LoadSingle<RecipeDatabase>();
        CookingSettings settings = LoadSingle<CookingSettings>();

        if (items == null || recipes == null)
        {
            Debug.LogWarning("DishPriceDebugMenu: ItemDatabase or RecipeDatabase not found.");
            return;
        }

        if (settings == null)
        {
            Debug.LogWarning("DishPriceDebugMenu: CookingSettings not found. Using default economy values.");
        }

        CategoryStats.Build(items);

        // ตัวคูณราคาขาย ดาว × คุณภาพ
        StringBuilder header = new StringBuilder("DishPricing: Sale multipliers (OK / Good / Perfect)\n");
        for (int stars = 1; stars <= RecipeData.MaxDifficulty; stars++)
        {
            header.AppendLine($"  Star {stars}: {DishPricing.PriceMultiplier(stars, DishQuality.Ok):0.###} / " +
                              $"{DishPricing.PriceMultiplier(stars, DishQuality.Good):0.###} / {DishPricing.PriceMultiplier(stars, DishQuality.Perfect):0.###}");
        }
        header.AppendLine($"  Eat stat multipliers: {DishPricing.QualityStatMultiplier(DishQuality.Ok):0.##} / " +
                          $"{DishPricing.QualityStatMultiplier(DishQuality.Good):0.##} / {DishPricing.QualityStatMultiplier(DishQuality.Perfect):0.##}");
        Debug.Log(header.ToString());

        List<RecipeData> sorted = new List<RecipeData>();
        foreach (RecipeData recipe in recipes.Recipes)
        {
            if (recipe != null && recipe.HasCategorySlot) sorted.Add(recipe);
        }
        sorted.Sort((a, b) => string.CompareOrdinal(a.recipeId, b.recipeId));

        int printed = 0;
        foreach (RecipeData recipe in sorted)
        {
            ItemCategory category = recipe.CategorySlot.category;
            List<ItemData> pool = new List<ItemData>();
            foreach (ItemData item in items.Items)
            {
                if (item != null && recipe.CategorySlot.Accepts(item)) pool.Add(item);
            }
            pool.Sort((a, b) => a.sellPrice != b.sellPrice ? a.sellPrice.CompareTo(b.sellPrice) : string.CompareOrdinal(a.itemId, b.itemId));

            ItemData dish = recipe.dishItem;
            Vector3 avg = CategoryStats.AverageRestore(category);
            string secretText = recipe.isSecret ? " secret" : "";
            string sheetStats = dish != null
                ? $"{dish.restoreHealth}/{dish.restoreStamina}/{dish.restoreMana} F{dish.restoreFever}"
                : "(no dish item)";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"DishPricing: {recipe.recipeId} '{recipe.recipeName}' star {recipe.Difficulty}{secretText} " +
                          $"sheet price {recipe.sellPrice}, sheet H/S/M {sheetStats} | " +
                          $"{RecipeSlot.CategoryLabel(category)} x{CategoryStats.CountIn(category)} avg price {CategoryStats.AveragePrice(category):0.#}, avg H/S/M {avg.x:0.#}/{avg.y:0.#}/{avg.z:0.#}");

            foreach (ItemData ingredient in pool)
            {
                int price = DishPricing.VariantPrice(recipe, ingredient);
                Vector3Int restore = DishPricing.VariantRestore(recipe, ingredient);
                int stars = recipe.Difficulty;
                sb.AppendLine($"  {recipe.recipeId}__{ingredient.itemId} ({ingredient.itemName}, {ingredient.sellPrice}): price {price} | " +
                              $"H/S/M {restore.x}/{restore.y}/{restore.z} | " +
                              $"sell OK {DishPricing.PriceFor(price, stars, DishQuality.Ok)} / Good {DishPricing.PriceFor(price, stars, DishQuality.Good)} / Perfect {DishPricing.PriceFor(price, stars, DishQuality.Perfect)}");
            }

            if (pool.Count == 0)
            {
                sb.AppendLine("  (no ingredient in this category)");
            }

            Debug.Log(sb.ToString());
            printed++;
        }

        Debug.Log($"DishPricing: Printed {printed} recipes with a category slot.");
    }

    // หา asset ชนิดนี้ตัวแรกในโปรเจกต์ (โหลดแล้ว OnEnable จะตั้ง Active ให้เอง)
    private static T LoadSingle<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        if (guids.Length == 0) return null;
        if (guids.Length > 1) Debug.LogWarning($"DishPriceDebugMenu: Found {guids.Length} {typeof(T).Name} assets. Using the first one.");
        return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}
