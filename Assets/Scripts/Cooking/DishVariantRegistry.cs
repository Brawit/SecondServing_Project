using System.Collections.Generic;
using UnityEngine;

// [v2] จานแยกแบบตามวัตถุดิบในช่องหมวด เช่น "Sushi (Salmon)" = jp_sushi__salmon
// - สร้างไอเทมตอนรันเกมเท่านั้น (ScriptableObject.CreateInstance) ไม่สร้างไฟล์ใน Assets / สร้างครั้งเดียวต่อแบบ แล้วใช้ตัวเดิม
// - ระบบอื่น (กระเป๋า เซฟ เมนู กล่องขาย) มองจานแบบเป็น ItemData ธรรมดา / ItemDatabase.GetById หาไม่เจอ → ถามที่นี่ (โหลดเซฟได้เอง)
// - สูตรที่ไม่มีช่องหมวด = ใช้ไอเทมจานแม่ (recipe.dishItem) ตามเดิม
// - หาสูตร/วัตถุดิบผ่าน RecipeDatabase.Active / ItemDatabase.Active (ไฟล์เดียวในโปรเจกต์ ตั้งตอนโหลด)
public static class DishVariantRegistry
{
    // ตัวคั่นระหว่างรหัสสูตรกับรหัสวัตถุดิบ (รหัสในชีตใช้ _ ตัวเดียว จึงไม่ชนกัน)
    public const string Separator = "__";

    private static readonly Dictionary<string, ItemData> variants = new Dictionary<string, ItemData>();

    // รหัสที่เคยเตือนแล้วว่าหาไม่เจอ (เตือนครั้งเดียวต่อรหัส)
    private static readonly HashSet<string> warnedIds = new HashSet<string>();

    // จำนวนแบบที่สร้างแล้ว
    public static int Count => variants.Count;

    public static IEnumerable<ItemData> Variants => variants.Values;

    // ล้างของเก่าตอนเริ่ม Play (เผื่อปิด Domain Reload ไว้ใน Editor)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        variants.Clear();
        warnedIds.Clear();
        CategoryStats.Invalidate();
    }

    // รหัสของจานแบบ เช่น jp_sushi__salmon
    public static string MakeId(RecipeData recipe, ItemData ingredient)
    {
        return recipe.recipeId + Separator + ingredient.itemId;
    }

    // ไอเทมจานที่ได้จากสูตรนี้ + วัตถุดิบในช่องหมวด (ไม่มีช่องหมวด / ingredient null = จานแม่)
    public static ItemData GetOrCreate(RecipeData recipe, ItemData ingredient)
    {
        if (recipe == null)
        {
            return null;
        }

        if (!recipe.HasCategorySlot || ingredient == null)
        {
            return recipe.dishItem;
        }

        if (!recipe.CategorySlot.Accepts(ingredient))
        {
            Debug.LogWarning($"DishVariantRegistry: {ingredient.itemName} doesn't fit the category slot of {recipe.recipeName}. Using the base dish.");
            return recipe.dishItem;
        }

        if (string.IsNullOrWhiteSpace(recipe.recipeId) || string.IsNullOrWhiteSpace(ingredient.itemId))
        {
            Debug.LogWarning($"DishVariantRegistry: {recipe.recipeName} or {ingredient.itemName} has no id. Using the base dish.");
            return recipe.dishItem;
        }

        string id = MakeId(recipe, ingredient);
        if (variants.TryGetValue(id, out ItemData existing) && existing != null)
        {
            return existing;
        }

        ItemData variant = Create(recipe, ingredient, id);
        variants[id] = variant;
        Debug.Log($"DishVariantRegistry: Created {variant.itemName} ({id}) price {variant.sellPrice}, " +
                  $"H/S/M {variant.restoreHealth}/{variant.restoreStamina}/{variant.restoreMana}. Variants: {variants.Count}.");
        return variant;
    }

    // แปลงรหัสจานแบบกลับเป็นไอเทม (ใช้ตอนโหลดเซฟ) หาสูตร/วัตถุดิบไม่เจอ = false + เตือนครั้งเดียว ไม่สร้างไอเทมผี
    public static bool TryResolve(string itemId, out ItemData item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        if (variants.TryGetValue(itemId, out item) && item != null)
        {
            return true;
        }

        int split = itemId.IndexOf(Separator, System.StringComparison.Ordinal);
        if (split <= 0 || split + Separator.Length >= itemId.Length)
        {
            return false;
        }

        string recipeId = itemId.Substring(0, split);
        string ingredientId = itemId.Substring(split + Separator.Length);

        RecipeData recipe = RecipeDatabase.Active != null ? RecipeDatabase.Active.GetById(recipeId) : null;
        ItemData ingredient = ItemDatabase.Active != null ? ItemDatabase.Active.GetById(ingredientId) : null;

        if (recipe == null || ingredient == null || !recipe.HasCategorySlot || !recipe.CategorySlot.Accepts(ingredient))
        {
            if (warnedIds.Add(itemId))
            {
                string reason = recipe == null ? $"recipe '{recipeId}' not found"
                    : ingredient == null ? $"ingredient '{ingredientId}' not found"
                    : !recipe.HasCategorySlot ? $"recipe '{recipeId}' has no category slot"
                    : $"'{ingredientId}' doesn't fit the category slot";
                Debug.LogWarning($"DishVariantRegistry: Can't resolve dish variant '{itemId}' ({reason}).");
            }
            item = null;
            return false;
        }

        item = GetOrCreate(recipe, ingredient);
        return item != null;
    }

    // สูตรแม่ของจาน (จานแม่หรือจานแบบ) ไม่ใช่จาน = null
    public static RecipeData RecipeOf(ItemData dish)
    {
        return DishPricing.RecipeOf(dish);
    }

    // จานของสูตรนี้ทุกตัวที่มีในกระเป๋า (จานแม่ก่อน แล้วจานแบบเรียงตามรหัส)
    public static List<ItemData> DishItemsIn(RecipeData recipe, Inventory inventory)
    {
        List<ItemData> result = new List<ItemData>();
        if (recipe == null || inventory == null)
        {
            return result;
        }

        if (recipe.dishItem != null && inventory.GetCount(recipe.dishItem) > 0)
        {
            result.Add(recipe.dishItem);
        }

        List<ItemData> found = new List<ItemData>();
        foreach (KeyValuePair<ItemData, int> pair in inventory.Items)
        {
            if (pair.Key != null && pair.Value > 0 && pair.Key.variantRecipe == recipe)
            {
                found.Add(pair.Key);
            }
        }
        found.Sort((a, b) => string.CompareOrdinal(a.itemId, b.itemId));
        result.AddRange(found);
        return result;
    }

    // สร้างไอเทมจานแบบจากจานแม่ + วัตถุดิบ
    private static ItemData Create(RecipeData recipe, ItemData ingredient, string id)
    {
        ItemData parent = recipe.dishItem;
        ItemData variant = ScriptableObject.CreateInstance<ItemData>();
        variant.hideFlags = HideFlags.DontSave;
        variant.name = id;

        string baseName = parent != null ? parent.itemName : recipe.recipeName;
        string baseNameTh = parent != null ? parent.nameTh : recipe.nameTh;

        variant.itemId = id;
        variant.itemName = $"{baseName} ({ingredient.itemName})";
        variant.nameTh = string.IsNullOrEmpty(baseNameTh) ? "" : $"{baseNameTh} ({ingredient.nameTh})";
        variant.icon = parent != null ? parent.icon : recipe.icon;
        variant.description = parent != null ? parent.description : "";
        variant.itemType = ItemType.Dish;
        variant.category = parent != null ? parent.category : ItemCategory.Other;
        variant.source = parent != null ? parent.source : ItemSource.Forage;
        variant.originType = parent != null ? parent.originType : ItemOriginType.Unknown;
        variant.seasons = parent != null ? parent.seasons : Season.All;

        variant.sellPrice = DishPricing.VariantPrice(recipe, ingredient);
        Vector3Int restore = DishPricing.VariantRestore(recipe, ingredient);
        variant.restoreHealth = restore.x;
        variant.restoreStamina = restore.y;
        variant.restoreMana = restore.z;
        variant.restoreFever = parent != null ? parent.restoreFever : 0;

        variant.variantRecipe = recipe;
        variant.variantIngredient = ingredient;
        return variant;
    }

    // ---------- Debug ----------

    public static void PrintStatus()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder($"DishVariantRegistry: {variants.Count} variant(s) created.");
        List<string> ids = new List<string>(variants.Keys);
        ids.Sort(System.StringComparer.Ordinal);
        foreach (string id in ids)
        {
            ItemData v = variants[id];
            if (v == null) continue;
            sb.Append($"\n  {id} '{v.itemName}' price {v.sellPrice}, H/S/M {v.restoreHealth}/{v.restoreStamina}/{v.restoreMana} F{v.restoreFever}");
        }
        Debug.Log(sb.ToString());
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Second Serving/Debug/Print Variant Registry")]
    private static void MenuPrintStatus()
    {
        PrintStatus();
    }
#endif
}
