using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// ตัว import สูตรอาหารและวัตถุดิบจากไฟล์ CSV (export จากตาราง Second Serving – Recipes & Ingredients)
// ใช้งาน: เมนู Tools > Second Serving > Import Recipes & Ingredients
// - อ่าน Assets/Data/Ingredients.csv แล้วสร้าง/อัปเดต ItemData (หาไฟล์เดิมจาก itemId ทั้งโปรเจกต์)
// - อ่าน Assets/Data/Recipes.csv แล้วสร้าง/อัปเดต RecipeData ใน Assets/Recipes
// - ไม่เขียนทับ icon และ description ที่ใส่เอง
// - หาคอลัมน์จากชื่อหัวคอลัมน์ ไม่ใช่ลำดับ (ย้ายคอลัมน์ได้ แต่ห้ามเปลี่ยนชื่อหัว)
public static class RecipeImporter
{
    // ---------- ตำแหน่งไฟล์ ----------

    private const string IngredientsCsvPath = "Assets/Data/Ingredients.csv";
    private const string RecipesCsvPath = "Assets/Data/Recipes.csv";
    private const string NewItemFolder = "Assets/Items";
    private const string RecipeFolder = "Assets/Recipes";
    private const string RecipeDatabasePath = "Assets/Recipes/RecipeDatabase.asset";

    // ---------- ชื่อหัวคอลัมน์ (ใส่ได้หลายชื่อ เผื่อเปลี่ยนชื่อในอนาคต) ----------

    private static readonly string[] ColItemId = { "item_id", "itemid" };
    private static readonly string[] ColItemNameTh = { "ชื่อไทย", "name_th" };
    private static readonly string[] ColNameEn = { "name (en)", "name_en", "name" };
    private static readonly string[] ColCategory = { "category" };
    private static readonly string[] ColSource = { "source" };
    private static readonly string[] ColSellPrice = { "sell price", "sell_price", "price" };

    private static readonly string[] ColRecipeId = { "recipe_id", "recipeid" };
    private static readonly string[] ColRecipeNameTh = { "ชื่อเมนู", "name_th" };
    private static readonly string[] ColCountry = { "country", "cuisine" };
    private static readonly string[] ColCourse = { "course" };
    private static readonly string[] ColSecret = { "secret", "is_secret" };
    private static readonly string[] ColDemo = { "vertical slice", "in_demo", "demo" };
    private static readonly string[] ColKeys = { "key 1", "key 2", "key 3", "key 4" };
    // คอลัมน์ที่ชื่อยาว ใช้หาจากคำขึ้นต้น
    private const string ColFullIngredientsPrefix = "วัตถุดิบเต็ม";

    // ---------- เมนู ----------

    [MenuItem("Tools/Second Serving/Import Recipes & Ingredients")]
    public static void ImportAll()
    {
        ImportReport report = new ImportReport();

        if (!File.Exists(IngredientsCsvPath))
        {
            EditorUtility.DisplayDialog("Import Recipes", $"File not found:\n{IngredientsCsvPath}", "OK");
            return;
        }
        if (!File.Exists(RecipesCsvPath))
        {
            EditorUtility.DisplayDialog("Import Recipes", $"File not found:\n{RecipesCsvPath}", "OK");
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("Import Recipes", "Importing ingredients...", 0.2f);
            Dictionary<string, ItemData> items = ImportIngredients(report);

            EditorUtility.DisplayProgressBar("Import Recipes", "Importing recipes...", 0.6f);
            ImportRecipes(items, report);

            EditorUtility.DisplayProgressBar("Import Recipes", "Refreshing databases...", 0.9f);
            RefreshDatabases(report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            Debug.LogError($"RecipeImporter: Import failed. {e}");
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Import Recipes", $"Import failed:\n{e.Message}\n\nSee Console for details.", "OK");
            return;
        }

        EditorUtility.ClearProgressBar();

        string summary = report.BuildSummary();
        Debug.Log($"RecipeImporter: {summary}");
        EditorUtility.DisplayDialog("Import Recipes", summary, "OK");
    }

    // ---------- วัตถุดิบ ----------

    private static Dictionary<string, ItemData> ImportIngredients(ImportReport report)
    {
        // ไอเทมที่มีอยู่แล้วในโปรเจกต์ (หาจาก itemId)
        Dictionary<string, ItemData> items = LoadExistingItems();

        List<List<string>> rows = ReadCsv(IngredientsCsvPath);
        if (rows.Count == 0)
        {
            Debug.LogWarning("RecipeImporter: Ingredients.csv is empty.");
            return items;
        }

        CsvHeader header = new CsvHeader(rows[0]);
        int colId = header.Require(ColItemId, "item_id", IngredientsCsvPath);
        int colTh = header.Find(ColItemNameTh);
        int colEn = header.Find(ColNameEn);
        int colCategory = header.Find(ColCategory);
        int colSource = header.Find(ColSource);
        int colPrice = header.Find(ColSellPrice);

        EnsureFolder(NewItemFolder);
        HashSet<string> seenIds = new HashSet<string>();

        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            string id = Cell(row, colId);
            if (string.IsNullOrEmpty(id))
            {
                continue; // แถวว่าง
            }

            if (!seenIds.Add(id))
            {
                report.Warn($"Ingredients.csv row {r + 1}: Duplicate item_id '{id}'. Skipped.");
                continue;
            }

            string nameEn = Cell(row, colEn);
            bool isNew = !items.TryGetValue(id, out ItemData item);

            if (isNew)
            {
                item = ScriptableObject.CreateInstance<ItemData>();
                item.itemId = id;
                string fileName = MakeFileName(string.IsNullOrEmpty(nameEn) ? id : nameEn);
                string path = AssetDatabase.GenerateUniqueAssetPath($"{NewItemFolder}/{fileName}.asset");
                AssetDatabase.CreateAsset(item, path);
                items.Add(id, item);
                report.ItemsCreated++;
            }
            else
            {
                report.ItemsUpdated++;
            }

            // อัปเดตค่า (ไม่แตะ icon และ description)
            if (!string.IsNullOrEmpty(nameEn))
            {
                item.itemName = nameEn;
            }
            item.nameTh = Cell(row, colTh);

            string categoryText = Cell(row, colCategory);
            if (!string.IsNullOrEmpty(categoryText))
            {
                if (TryParseEnum(categoryText, out ItemCategory category))
                {
                    item.category = category;
                }
                else
                {
                    report.Warn($"Ingredients.csv row {r + 1} ({id}): Unknown category '{categoryText}'. Set to Other.");
                    item.category = ItemCategory.Other;
                }
            }

            string sourceText = Cell(row, colSource);
            if (!string.IsNullOrEmpty(sourceText))
            {
                if (TryParseEnum(sourceText, out ItemSource source))
                {
                    item.source = source;
                }
                else
                {
                    report.Warn($"Ingredients.csv row {r + 1} ({id}): Unknown source '{sourceText}'. Value kept.");
                }
            }

            item.sellPrice = ParseInt(Cell(row, colPrice));

            EditorUtility.SetDirty(item);
        }

        return items;
    }

    // ---------- สูตรอาหาร ----------

    private static void ImportRecipes(Dictionary<string, ItemData> items, ImportReport report)
    {
        Dictionary<string, RecipeData> recipes = LoadExistingRecipes();

        List<List<string>> rows = ReadCsv(RecipesCsvPath);
        if (rows.Count == 0)
        {
            Debug.LogWarning("RecipeImporter: Recipes.csv is empty.");
            return;
        }

        CsvHeader header = new CsvHeader(rows[0]);
        int colId = header.Require(ColRecipeId, "recipe_id", RecipesCsvPath);
        int colTh = header.Find(ColRecipeNameTh);
        int colEn = header.Find(ColNameEn);
        int colCountry = header.Find(ColCountry);
        int colCourse = header.Find(ColCourse);
        int colSecret = header.Find(ColSecret);
        int colDemo = header.Find(ColDemo);
        int colFull = header.FindByPrefix(ColFullIngredientsPrefix);
        int colPrice = header.Find(ColSellPrice);

        int[] colKeys = new int[ColKeys.Length];
        for (int k = 0; k < ColKeys.Length; k++)
        {
            colKeys[k] = header.Find(new[] { ColKeys[k] });
        }

        EnsureFolder(RecipeFolder);
        HashSet<string> seenIds = new HashSet<string>();

        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            string id = Cell(row, colId);
            if (string.IsNullOrEmpty(id))
            {
                continue; // แถวว่าง
            }

            if (!seenIds.Add(id))
            {
                report.Warn($"Recipes.csv row {r + 1}: Duplicate recipe_id '{id}'. Skipped.");
                continue;
            }

            bool isNew = !recipes.TryGetValue(id, out RecipeData recipe);
            if (isNew)
            {
                recipe = ScriptableObject.CreateInstance<RecipeData>();
                recipe.recipeId = id;
                string path = AssetDatabase.GenerateUniqueAssetPath($"{RecipeFolder}/{id}.asset");
                AssetDatabase.CreateAsset(recipe, path);
                recipes.Add(id, recipe);
                report.RecipesCreated++;
            }
            else
            {
                report.RecipesUpdated++;
            }

            // อัปเดตค่า (ไม่แตะ icon)
            string nameEn = Cell(row, colEn);
            if (!string.IsNullOrEmpty(nameEn))
            {
                recipe.recipeName = nameEn;
            }
            recipe.nameTh = Cell(row, colTh);

            string countryText = Cell(row, colCountry);
            if (TryParseEnum(countryText, out Cuisine cuisine))
            {
                recipe.cuisine = cuisine;
            }
            else if (!string.IsNullOrEmpty(countryText))
            {
                report.Warn($"Recipes.csv row {r + 1} ({id}): Unknown country '{countryText}'. Value kept.");
            }

            string courseText = Cell(row, colCourse);
            if (TryParseEnum(courseText, out DishCourse course))
            {
                recipe.course = course;
            }
            else if (!string.IsNullOrEmpty(courseText))
            {
                report.Warn($"Recipes.csv row {r + 1} ({id}): Unknown course '{courseText}'. Value kept.");
            }

            recipe.isSecret = ParseBool(Cell(row, colSecret));
            recipe.inDemo = ParseBool(Cell(row, colDemo));
            recipe.fullIngredientsText = Cell(row, colFull);
            recipe.sellPrice = ParseInt(Cell(row, colPrice));

            // วัตถุดิบหลัก
            recipe.ingredients.Clear();
            foreach (int col in colKeys)
            {
                string itemId = Cell(row, col);
                if (string.IsNullOrEmpty(itemId))
                {
                    continue;
                }

                if (items.TryGetValue(itemId, out ItemData item))
                {
                    recipe.ingredients.Add(item);
                }
                else
                {
                    report.Warn($"Recipes.csv row {r + 1} ({id}): Unknown item_id '{itemId}'. Skipped.");
                }
            }

            if (recipe.ingredients.Count == 0)
            {
                report.Warn($"Recipes.csv row {r + 1} ({id}): Recipe has no ingredients.");
            }

            EditorUtility.SetDirty(recipe);
        }
    }

    // ---------- ฐานข้อมูล ----------

    private static void RefreshDatabases(ImportReport report)
    {
        // ItemDatabase: ใช้ไฟล์ที่มีอยู่ (ไม่สร้างใหม่ เพราะ SaveManager อ้างอิงไฟล์เดิมอยู่)
        string[] itemDbGuids = AssetDatabase.FindAssets("t:ItemDatabase");
        if (itemDbGuids.Length == 0)
        {
            report.Warn("No ItemDatabase asset found. Create one via Create > Cozy > Item Database, then run Find All Items.");
        }
        else
        {
            if (itemDbGuids.Length > 1)
            {
                report.Warn($"Found {itemDbGuids.Length} ItemDatabase assets. Only the first one was refreshed.");
            }

            string path = AssetDatabase.GUIDToAssetPath(itemDbGuids[0]);
            ItemDatabase itemDb = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
            if (itemDb != null)
            {
                report.ItemDatabaseCount = itemDb.RefreshFromProject();
            }
        }

        // RecipeDatabase: ถ้ายังไม่มี สร้างให้
        RecipeDatabase recipeDb = null;
        string[] recipeDbGuids = AssetDatabase.FindAssets("t:RecipeDatabase");
        if (recipeDbGuids.Length > 0)
        {
            if (recipeDbGuids.Length > 1)
            {
                report.Warn($"Found {recipeDbGuids.Length} RecipeDatabase assets. Only the first one was refreshed.");
            }
            recipeDb = AssetDatabase.LoadAssetAtPath<RecipeDatabase>(AssetDatabase.GUIDToAssetPath(recipeDbGuids[0]));
        }

        if (recipeDb == null)
        {
            EnsureFolder(RecipeFolder);
            recipeDb = ScriptableObject.CreateInstance<RecipeDatabase>();
            AssetDatabase.CreateAsset(recipeDb, RecipeDatabasePath);
            Debug.Log($"RecipeImporter: Created {RecipeDatabasePath}.");
        }

        report.RecipeDatabaseCount = recipeDb.RefreshFromProject();
    }

    private static Dictionary<string, ItemData> LoadExistingItems()
    {
        Dictionary<string, ItemData> result = new Dictionary<string, ItemData>();

        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null || string.IsNullOrWhiteSpace(item.itemId))
            {
                continue;
            }

            if (!result.ContainsKey(item.itemId))
            {
                result.Add(item.itemId, item);
            }
            else
            {
                Debug.LogWarning($"RecipeImporter: Duplicate itemId '{item.itemId}' in project ({item.name}). Using the first one.", item);
            }
        }

        return result;
    }

    private static Dictionary<string, RecipeData> LoadExistingRecipes()
    {
        Dictionary<string, RecipeData> result = new Dictionary<string, RecipeData>();

        foreach (string guid in AssetDatabase.FindAssets("t:RecipeData"))
        {
            RecipeData recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(AssetDatabase.GUIDToAssetPath(guid));
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.recipeId))
            {
                continue;
            }

            if (!result.ContainsKey(recipe.recipeId))
            {
                result.Add(recipe.recipeId, recipe);
            }
            else
            {
                Debug.LogWarning($"RecipeImporter: Duplicate recipeId '{recipe.recipeId}' in project ({recipe.name}). Using the first one.", recipe);
            }
        }

        return result;
    }

    // ---------- ตัวช่วย ----------

    // สร้างโฟลเดอร์ (และโฟลเดอร์แม่) ถ้ายังไม่มี เช่น "Assets/Recipes"
    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        string child = Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, child);
    }

    // แปลงชื่อเป็นชื่อไฟล์แบบ PascalCase เช่น "Holy Basil" -> "HolyBasil"
    private static string MakeFileName(string text)
    {
        StringBuilder sb = new StringBuilder();
        bool upperNext = true;

        foreach (char c in text)
        {
            if (char.IsLetterOrDigit(c) && c < 128)
            {
                sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
                upperNext = false;
            }
            else
            {
                upperNext = true;
            }
        }

        return sb.Length > 0 ? sb.ToString() : "Item";
    }

    // แปลงข้อความเป็น enum โดยไม่สนช่องว่างและตัวพิมพ์ เช่น "Drink Base" -> DrinkBase
    private static bool TryParseEnum<T>(string text, out T value) where T : struct
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string compact = text.Replace(" ", "").Replace("_", "").Replace("-", "");
        return Enum.TryParse(compact, true, out value) && Enum.IsDefined(typeof(T), value);
    }

    private static bool ParseBool(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string t = text.Trim().ToLowerInvariant();
        return t == "true" || t == "1" || t == "yes" || t == "y";
    }

    private static int ParseInt(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        // ตัดตัวคั่นหลักพันและเครื่องหมายเงินออก เช่น "1,200" หรือ "$50"
        string cleaned = text.Replace(",", "").Replace("$", "").Replace("฿", "").Trim();
        if (float.TryParse(cleaned, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float number))
        {
            return Mathf.Max(0, Mathf.RoundToInt(number));
        }

        return 0;
    }

    // อ่านค่าในช่อง ถ้าไม่มีคอลัมน์หรือแถวสั้นกว่า คืนค่าว่าง
    private static string Cell(List<string> row, int col)
    {
        if (col < 0 || col >= row.Count)
        {
            return string.Empty;
        }

        return row[col].Trim();
    }

    // ---------- ตัวอ่าน CSV ----------

    // อ่าน CSV แบบรองรับเครื่องหมายคำพูด จุลภาคในช่อง และขึ้นบรรทัดใหม่ในช่อง
    private static List<List<string>> ReadCsv(string path)
    {
        string text = File.ReadAllText(path, Encoding.UTF8);

        // ตัด BOM ออก ถ้ามี
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text.Substring(1);
        }

        List<List<string>> rows = new List<List<string>>();
        List<string> row = new List<string>();
        StringBuilder field = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // "" ในเครื่องหมายคำพูด = เครื่องหมายคำพูดจริง 1 ตัว
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    // ข้าม \r (จัดการตอนเจอ \n)
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        // แถวสุดท้ายที่ไม่มีขึ้นบรรทัดใหม่ปิดท้าย
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    // ---------- คลาสช่วย ----------

    // หาตำแหน่งคอลัมน์จากชื่อหัว (ไม่สนตัวพิมพ์และช่องว่างหน้า/หลัง)
    private class CsvHeader
    {
        private readonly List<string> names = new List<string>();

        public CsvHeader(List<string> headerRow)
        {
            foreach (string h in headerRow)
            {
                names.Add(h.Trim().ToLowerInvariant());
            }
        }

        public int Find(string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                int index = names.IndexOf(candidate.ToLowerInvariant());
                if (index >= 0)
                {
                    return index;
                }
            }
            return -1;
        }

        public int FindByPrefix(string prefix)
        {
            string p = prefix.ToLowerInvariant();
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i].StartsWith(p))
                {
                    return i;
                }
            }
            return -1;
        }

        // คอลัมน์ที่ต้องมี ถ้าไม่เจอจะหยุด import
        public int Require(string[] candidates, string displayName, string file)
        {
            int index = Find(candidates);
            if (index < 0)
            {
                throw new Exception($"Column '{displayName}' not found in {file}.");
            }
            return index;
        }
    }

    // สรุปผลการ import
    private class ImportReport
    {
        public int ItemsCreated;
        public int ItemsUpdated;
        public int RecipesCreated;
        public int RecipesUpdated;
        public int ItemDatabaseCount;
        public int RecipeDatabaseCount;
        private readonly List<string> warnings = new List<string>();

        public void Warn(string message)
        {
            warnings.Add(message);
            Debug.LogWarning($"RecipeImporter: {message}");
        }

        public string BuildSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Items: {ItemsCreated} created, {ItemsUpdated} updated (ItemDatabase: {ItemDatabaseCount}).");
            sb.AppendLine($"Recipes: {RecipesCreated} created, {RecipesUpdated} updated (RecipeDatabase: {RecipeDatabaseCount}).");
            sb.Append(warnings.Count == 0 ? "No warnings." : $"{warnings.Count} warning(s). See Console.");
            return sb.ToString();
        }
    }
}
