using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// [v2] เมนู: Tools > Second Serving > Sync Shop Catalogs From Data
// อัปเดตรายการสินค้าในร้านให้ตรงกับข้อมูลที่ import แล้ว (ชีต Ingredients v2 / Recipes v2) กดซ้ำได้ทุกครั้งหลัง import
// - ร้านของชำ (Assets/Shops/GeneralStore): แถวไอเทม = วัตถุดิบทุกชนิดที่ In Store = Y (ItemData.inStore) เรียงตามลำดับแถวใน Ingredients.csv
// - ร้านหนังสือ (Assets/Shops/Bookstore): แถวสมุดสูตร = ทุกสูตรที่ Obtain = Shop Book เรียงราคาถูกไปแพง (แบบเดียวกับ Build Shops)
// - แถวที่ของหายไปแล้ว (missing) ถูกลบ / ราคาตายตัว (priceOverride) ของแถวที่ยังอยู่ = คงไว้ / แถวชนิดอื่น (เช่น อัปเกรดกระเป๋า) = ไม่แตะ
// - ไม่แตะร้านกระเป๋า (BagShop) / ไม่แตะฉาก / ย้อนกลับได้ด้วย Ctrl+Z
public static class ShopCatalogSync
{
    private const string GeneralStorePath = "Assets/Shops/GeneralStore.asset";
    private const string BookstorePath = "Assets/Shops/Bookstore.asset";
    private const string IngredientsCsvPath = "Assets/Data/Ingredients.csv";

    [MenuItem("Tools/Second Serving/Sync Shop Catalogs From Data")]
    public static void Sync()
    {
        List<string> report = new List<string>();

        ShopCatalog general = AssetDatabase.LoadAssetAtPath<ShopCatalog>(GeneralStorePath);
        ShopCatalog books = AssetDatabase.LoadAssetAtPath<ShopCatalog>(BookstorePath);

        if (general == null) report.Add($"{GeneralStorePath} not found (run Build Shops first).");
        else SyncGeneralStore(general, report);

        if (books == null) report.Add($"{BookstorePath} not found (run Build Shops first).");
        else SyncBookstore(books, report);

        AssetDatabase.SaveAssets();

        string message = string.Join("\n", report);
        Debug.Log("ShopCatalogSync:\n" + message);
        EditorUtility.DisplayDialog("Sync Shop Catalogs", message, "OK");
    }

    // ---------- ร้านของชำ ----------

    private static void SyncGeneralStore(ShopCatalog catalog, List<string> report)
    {
        // ลำดับตามแถวใน CSV (ไม่เจอใน CSV = ต่อท้าย เรียงตาม itemId)
        Dictionary<string, int> csvOrder = ReadFirstColumnOrder(IngredientsCsvPath);

        List<ItemData> wanted = new List<ItemData>();
        HashSet<string> seenIds = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null || string.IsNullOrEmpty(item.itemId)) continue;
            if (item.itemType != ItemType.Ingredient || !item.inStore) continue;
            if (!seenIds.Add(item.itemId)) continue; // itemId ซ้ำ = ใช้ไฟล์แรกที่เจอ
            wanted.Add(item);
        }

        wanted.Sort((a, b) =>
        {
            int ia = csvOrder.TryGetValue(a.itemId, out int x) ? x : int.MaxValue;
            int ib = csvOrder.TryGetValue(b.itemId, out int y) ? y : int.MaxValue;
            return ia != ib ? ia.CompareTo(ib) : string.CompareOrdinal(a.itemId, b.itemId);
        });

        SyncEntries(catalog, ShopEntryKind.Item, wanted.ConvertAll(i => (Object)i), report, "items");
    }

    // ---------- ร้านหนังสือ ----------

    private static void SyncBookstore(ShopCatalog catalog, List<string> report)
    {
        List<RecipeData> wanted = new List<RecipeData>();
        HashSet<string> seenIds = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:RecipeData"))
        {
            RecipeData recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(AssetDatabase.GUIDToAssetPath(guid));
            if (recipe == null || recipe.obtainSource != RecipeSource.ShopBook) continue;
            string id = string.IsNullOrEmpty(recipe.recipeId) ? recipe.name : recipe.recipeId;
            if (!seenIds.Add(id)) continue;
            wanted.Add(recipe);
        }

        wanted.Sort((a, b) => a.sellPrice != b.sellPrice ? a.sellPrice.CompareTo(b.sellPrice) : string.CompareOrdinal(a.recipeId, b.recipeId));

        SyncEntries(catalog, ShopEntryKind.RecipeBook, wanted.ConvertAll(r => (Object)r), report, "recipe books");
    }

    // ---------- ส่วนกลาง ----------

    // แทนแถวชนิด kind ทั้งหมดด้วยรายการ wanted (ตามลำดับ) / แถวชนิดอื่นคงตำแหน่งเดิมไว้ด้านหลัง
    private static void SyncEntries(ShopCatalog catalog, ShopEntryKind kind, List<Object> wanted, List<string> report, string label)
    {
        Undo.RecordObject(catalog, "Sync Shop Catalog");

        // เก็บราคาตายตัวของแถวเดิมไว้ใช้ต่อ + นับแถวที่หาย
        Dictionary<Object, int> oldOverrides = new Dictionary<Object, int>();
        HashSet<Object> oldTargets = new HashSet<Object>();
        int missingCount = 0;
        List<ShopEntry> others = new List<ShopEntry>();

        foreach (ShopEntry entry in catalog.entries)
        {
            if (entry == null) continue;
            if (entry.kind != kind)
            {
                others.Add(entry);
                continue;
            }

            Object target = TargetOf(entry);
            if (target == null)
            {
                missingCount++;
                continue;
            }

            oldTargets.Add(target);
            if (entry.priceOverride > 0 && !oldOverrides.ContainsKey(target)) oldOverrides.Add(target, entry.priceOverride);
        }

        List<string> added = new List<string>();
        List<string> removed = new List<string>();
        HashSet<Object> wantedSet = new HashSet<Object>(wanted);

        foreach (Object target in oldTargets)
        {
            if (!wantedSet.Contains(target)) removed.Add(NameOf(target));
        }

        List<ShopEntry> result = new List<ShopEntry>();
        foreach (Object target in wanted)
        {
            ShopEntry entry = new ShopEntry { kind = kind };
            if (kind == ShopEntryKind.Item) entry.item = (ItemData)target;
            else entry.recipe = (RecipeData)target;
            if (oldOverrides.TryGetValue(target, out int price)) entry.priceOverride = price;
            if (!oldTargets.Contains(target)) added.Add(NameOf(target));
            result.Add(entry);
        }
        result.AddRange(others);

        catalog.entries = result;
        EditorUtility.SetDirty(catalog);

        report.Add($"{catalog.shopName}: {wanted.Count} {label} (added {added.Count}, removed {removed.Count}, missing rows removed {missingCount}).");
        if (added.Count > 0) report.Add("  + " + string.Join(", ", added));
        if (removed.Count > 0) report.Add("  - " + string.Join(", ", removed));
    }

    private static Object TargetOf(ShopEntry entry)
    {
        switch (entry.kind)
        {
            case ShopEntryKind.Item: return entry.item;
            case ShopEntryKind.RecipeBook: return entry.recipe;
            default: return null;
        }
    }

    private static string NameOf(Object target)
    {
        if (target is ItemData item) return item.itemId;
        if (target is RecipeData recipe) return recipe.recipeId;
        return target != null ? target.name : "?";
    }

    // อ่านคอลัมน์แรกของ CSV เป็นลำดับแถว (ข้ามหัวตาราง) / ไฟล์ Ingredients ไม่มีช่องขึ้นบรรทัดใหม่ จึงอ่านทีละบรรทัดได้
    private static Dictionary<string, int> ReadFirstColumnOrder(string path)
    {
        Dictionary<string, int> order = new Dictionary<string, int>();
        if (!File.Exists(path))
        {
            Debug.LogWarning($"ShopCatalogSync: {path} not found. Items are sorted by itemId instead.");
            return order;
        }

        string[] lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            int comma = line.IndexOf(',');
            string id = (comma >= 0 ? line.Substring(0, comma) : line).Trim().Trim('"');
            if (id.Length > 0 && !order.ContainsKey(id)) order.Add(id, i);
        }
        return order;
    }
}
