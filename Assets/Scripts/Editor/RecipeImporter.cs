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
// - สร้าง/อัปเดตไอเทมจานอาหาร (ItemType.Dish) ของทุกสูตรใน Assets/Items/Dishes (itemId = recipe_id)
// - ไม่เขียนทับ icon และ description ที่ใส่เอง
// - อ่านค่าที่ฟื้นเมื่อกิน (Health / Stamina / Mana / Fever) ใส่ให้วัตถุดิบและจานอาหาร
// - หาคอลัมน์จากชื่อหัวคอลัมน์ ไม่ใช่ลำดับ (ย้ายคอลัมน์ได้ แต่ห้ามเปลี่ยนชื่อหัว)
// - [2.5b] อ่านความยาก (Difficulty 1–3) ช่องว่าง = 0 (คิดเอง) / ไม่มีคอลัมน์ทั้งคอลัมน์ = เตือน แล้วไม่แตะค่าเดิม
// - [2.5c] อ่านเชฟจาก Chefs.csv (สร้าง ChefData ใน Assets/Chefs ไม่ทับ portrait) และทางได้สูตร/จดหมายเชฟ
//   (Obtain, Obtain Note, Mail Shop Lv, Mail Reputation, Chef, Letter) ไม่มีคอลัมน์ = เตือน แล้วไม่แตะค่าเดิม
public static class RecipeImporter
{
    // ---------- ตำแหน่งไฟล์ ----------

    private const string IngredientsCsvPath = "Assets/Data/Ingredients.csv";
    private const string RecipesCsvPath = "Assets/Data/Recipes.csv";
    private const string NewItemFolder = "Assets/Items";
    private const string DishFolder = "Assets/Items/Dishes";
    private const string RecipeFolder = "Assets/Recipes";
    private const string RecipeDatabasePath = "Assets/Recipes/RecipeDatabase.asset";
    private const string ChefsCsvPath = "Assets/Data/Chefs.csv";
    private const string ChefFolder = "Assets/Chefs";

    // ---------- ชื่อหัวคอลัมน์ (ใส่ได้หลายชื่อ เผื่อเปลี่ยนชื่อในอนาคต) ----------

    private static readonly string[] ColItemId = { "item_id", "itemid" };
    private static readonly string[] ColItemNameTh = { "ชื่อไทย", "name_th" };
    private static readonly string[] ColNameEn = { "name (en)", "name_en", "name" };
    private static readonly string[] ColCategory = { "category" };
    private static readonly string[] ColSource = { "source" };
    private static readonly string[] ColSellPrice = { "sell price", "sell_price", "price" };

    // ค่าที่ฟื้นเมื่อกิน (มีทั้งในชีต Ingredients และ Recipes ถ้าไม่มีคอลัมน์ จะไม่แตะค่าเดิม)
    private static readonly string[] ColHealth = { "health", "restore health" };
    private static readonly string[] ColStamina = { "stamina", "restore stamina" };
    private static readonly string[] ColMana = { "mana", "restore mana" };
    private static readonly string[] ColFever = { "fever", "restore fever" };

    private static readonly string[] ColRecipeId = { "recipe_id", "recipeid" };
    private static readonly string[] ColRecipeNameTh = { "ชื่อเมนู", "name_th" };
    private static readonly string[] ColCountry = { "country", "cuisine" };
    private static readonly string[] ColCourse = { "course" };
    private static readonly string[] ColSecret = { "secret", "is_secret" };
    private static readonly string[] ColDemo = { "vertical slice", "in_demo", "demo" };
    private static readonly string[] ColKeys = { "key 1", "key 2", "key 3", "key 4", "key 5" };

    // [v2] คอลัมน์ใหม่ (ชีต Ingredients v2 / Recipes v2)
    private static readonly string[] ColInStore = { "in store", "in_store" };
    private static readonly string[] ColEarly = { "early", "early game" };
    private static readonly string[] ColType = { "type" };
    private static readonly string[] ColSeason = { "season", "seasons" };
    private static readonly string[] ColBaseRecipe = { "base recipe", "base_recipe" };

    // [v2] Key ที่ขึ้นต้นด้วยคำนี้ = ช่องหมวด เช่น any_fish
    private const string AnyPrefix = "any_";
    private static readonly string[] ColDifficulty = { "difficulty" };

    // [2.5c] ทางได้สูตร + จดหมายเชฟ (ชีต Recipes)
    private static readonly string[] ColObtain = { "obtain" };
    private static readonly string[] ColObtainNote = { "obtain note", "obtain_note" };
    private static readonly string[] ColMailLevel = { "mail shop lv", "mail shop level", "mail_shop_lv" };
    private static readonly string[] ColMailReputation = { "mail reputation", "mail_reputation" };
    private static readonly string[] ColChef = { "chef", "chef_id" };
    private static readonly string[] ColLetter = { "letter" };

    // [2.5c] ชีต Chefs
    private static readonly string[] ColChefId = { "chef_id", "chefid" };
    private static readonly string[] ColChefName = { "name", "name (en)" };
    private static readonly string[] ColSignOff = { "sign-off", "sign off", "signoff" };
    private static readonly string[] ColBio = { "bio" };
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

            EditorUtility.DisplayProgressBar("Import Recipes", "Importing chefs...", 0.4f);
            Dictionary<string, ChefData> chefs = ImportChefs(report);

            EditorUtility.DisplayProgressBar("Import Recipes", "Importing recipes...", 0.6f);
            ImportRecipes(items, chefs, report);

            EditorUtility.DisplayProgressBar("Import Recipes", "Refreshing databases...", 0.9f);
            RefreshDatabases(report);

            // [v2] รายงานของเก่าที่ไม่มีใน CSV แล้ว (ไม่ลบ)
            ReportObsolete(report);

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
        FoodColumns foodCols = FoodColumns.Find(header);

        // [v2] ไม่มีคอลัมน์ = CSV เก่า คงค่าเดิม
        int colInStore = header.Find(ColInStore);
        int colEarly = header.Find(ColEarly);
        int colType = header.Find(ColType);
        int colSeason = header.Find(ColSeason);

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
            item.itemType = ItemType.Ingredient;

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
            foodCols.ApplyTo(item, row);

            // [v2] ร้าน / ต้นเกม / ประเภท / ฤดู
            if (colInStore >= 0) item.inStore = ParseBool(Cell(row, colInStore));
            if (colEarly >= 0) item.earlyGame = ParseBool(Cell(row, colEarly));
            if (colType >= 0)
            {
                string typeText = Cell(row, colType);
                if (TryParseEnum(typeText, out ItemOriginType originType))
                {
                    item.originType = originType;
                }
                else if (!string.IsNullOrEmpty(typeText))
                {
                    report.Warn($"Ingredients.csv row {r + 1} ({id}): Unknown type '{typeText}'. Value kept.");
                }
            }
            if (colSeason >= 0)
            {
                string seasonText = Cell(row, colSeason);
                if (SeasonUtil.TryParse(seasonText, out Season seasons))
                {
                    item.seasons = seasons;
                }
                else
                {
                    item.seasons = Season.All;
                    if (!string.IsNullOrEmpty(seasonText))
                    {
                        report.Warn($"Ingredients.csv row {r + 1} ({id}): Unknown season '{seasonText}'. Using All.");
                    }
                }
            }

            report.CsvItemIds.Add(id);
            EditorUtility.SetDirty(item);
        }

        return items;
    }

    // ---------- สูตรอาหาร ----------

    // ---------- [2.5c] เชฟ ----------

    // สร้าง/อัปเดต ChefData ตาม chef_id (ไม่แตะ portrait) ไม่มีไฟล์ Chefs.csv = เตือน แล้วใช้ของที่มีในโปรเจกต์
    private static Dictionary<string, ChefData> ImportChefs(ImportReport report)
    {
        Dictionary<string, ChefData> chefs = LoadExistingChefs();

        if (!File.Exists(ChefsCsvPath))
        {
            report.Warn($"{ChefsCsvPath} not found. Existing chefs were kept. Export the Chefs sheet as CSV.");
            return chefs;
        }

        List<List<string>> rows = ReadCsv(ChefsCsvPath);
        if (rows.Count == 0)
        {
            report.Warn("Chefs.csv is empty.");
            return chefs;
        }

        CsvHeader header = new CsvHeader(rows[0]);
        int colId = header.Require(ColChefId, "chef_id", ChefsCsvPath);
        int colName = header.Find(ColChefName);
        int colCountry = header.Find(ColCountry);
        int colSignOff = header.Find(ColSignOff);
        int colBio = header.Find(ColBio);

        EnsureFolder(ChefFolder);

        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            string id = Cell(row, colId);
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            if (!chefs.TryGetValue(id, out ChefData chef))
            {
                chef = ScriptableObject.CreateInstance<ChefData>();
                chef.chefId = id;
                AssetDatabase.CreateAsset(chef, AssetDatabase.GenerateUniqueAssetPath($"{ChefFolder}/{id}.asset"));
                chefs.Add(id, chef);
                report.ChefsCreated++;
            }
            else
            {
                report.ChefsUpdated++;
            }

            string displayName = Cell(row, colName);
            if (!string.IsNullOrEmpty(displayName))
            {
                chef.displayName = displayName;
            }

            string countryText = Cell(row, colCountry);
            if (TryParseEnum(countryText, out Cuisine cuisine))
            {
                chef.cuisine = cuisine;
            }
            else if (!string.IsNullOrEmpty(countryText))
            {
                report.Warn($"Chefs.csv row {r + 1} ({id}): Unknown country '{countryText}'. Value kept.");
            }

            if (colSignOff >= 0) chef.signOff = CleanMultiline(Cell(row, colSignOff));
            if (colBio >= 0) chef.bio = CleanMultiline(Cell(row, colBio));

            EditorUtility.SetDirty(chef);
        }

        return chefs;
    }

    private static Dictionary<string, ChefData> LoadExistingChefs()
    {
        Dictionary<string, ChefData> result = new Dictionary<string, ChefData>();

        foreach (string guid in AssetDatabase.FindAssets("t:ChefData"))
        {
            ChefData chef = AssetDatabase.LoadAssetAtPath<ChefData>(AssetDatabase.GUIDToAssetPath(guid));
            if (chef == null || string.IsNullOrWhiteSpace(chef.chefId))
            {
                continue;
            }

            if (!result.ContainsKey(chef.chefId))
            {
                result.Add(chef.chefId, chef);
            }
            else
            {
                Debug.LogWarning($"RecipeImporter: Duplicate chefId '{chef.chefId}' in project ({chef.name}). Using the first one.", chef);
            }
        }

        return result;
    }

    // ข้อความหลายบรรทัดจาก CSV: ตัด \r ออก ให้เหลือ \n อย่างเดียว
    private static string CleanMultiline(string text)
    {
        return string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\r", "");
    }

    // ---------- สูตร ----------

    private static void ImportRecipes(Dictionary<string, ItemData> items, Dictionary<string, ChefData> chefs, ImportReport report)
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
        FoodColumns foodCols = FoodColumns.Find(header);

        // [2.5b] ความยาก: ไม่มีคอลัมน์ = CSV เก่า (export ก่อนเพิ่มคอลัมน์) เตือนแล้วคงค่าเดิมในโปรเจกต์
        int colDifficulty = header.Find(ColDifficulty);
        if (colDifficulty < 0)
        {
            report.Warn("Recipes.csv has no 'Difficulty' column. Existing difficulty values were kept (blank = auto by starter/normal/secret). Export the CSV again from the sheet.");
        }

        // [2.5c] ทางได้สูตร + จดหมายเชฟ: ไม่มีคอลัมน์ = CSV เก่า เตือนแล้วคงค่าเดิม
        int colObtain = header.Find(ColObtain);
        int colObtainNote = header.Find(ColObtainNote);
        int colMailLevel = header.Find(ColMailLevel);
        int colMailReputation = header.Find(ColMailReputation);
        int colChef = header.Find(ColChef);
        int colLetter = header.Find(ColLetter);
        if (colObtain < 0 || colMailLevel < 0 || colMailReputation < 0 || colChef < 0 || colLetter < 0)
        {
            report.Warn("Recipes.csv is missing some of 'Obtain', 'Mail Shop Lv', 'Mail Reputation', 'Chef', 'Letter'. Missing columns were skipped and existing values kept. Export the CSV again from the sheet.");
        }

        int colBaseRecipe = header.Find(ColBaseRecipe);
        Dictionary<RecipeData, string> pendingBase = new Dictionary<RecipeData, string>();

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

            // [2.5b] ความยาก 1–3 (ช่องว่าง = 0 = คิดเองจากสูตรเริ่มต้น/ทั่วไป/สูตรลับ)
            if (colDifficulty >= 0)
            {
                string difficultyText = Cell(row, colDifficulty);
                int difficulty = ParseInt(difficultyText);
                if (difficulty < 0 || difficulty > RecipeData.MaxDifficulty)
                {
                    report.Warn($"Recipes.csv row {r + 1} ({id}): Difficulty '{difficultyText}' is not 1-{RecipeData.MaxDifficulty}. Using auto.");
                    difficulty = 0;
                }
                recipe.difficulty = difficulty;
            }

            // [2.5c] ทางได้สูตร
            if (colObtain >= 0)
            {
                string obtainText = Cell(row, colObtain);
                if (RecipeSourceUtil.TryParse(obtainText, out RecipeSource source))
                {
                    recipe.obtainSource = source;
                }
                else
                {
                    recipe.obtainSource = RecipeSource.Starter;
                    if (!recipe.inDemo)
                    {
                        report.Warn($"Recipes.csv row {r + 1} ({id}): Obtain '{obtainText}' is empty or unknown. Using Starter.");
                    }
                }
            }
            if (colObtainNote >= 0) recipe.obtainNote = Cell(row, colObtainNote);
            if (colMailLevel >= 0) recipe.mailShopLevel = ParseInt(Cell(row, colMailLevel));
            if (colMailReputation >= 0) recipe.mailReputation = ParseInt(Cell(row, colMailReputation));
            if (colLetter >= 0) recipe.letterBody = CleanMultiline(Cell(row, colLetter));

            if (colChef >= 0)
            {
                string chefId = Cell(row, colChef);
                if (string.IsNullOrEmpty(chefId))
                {
                    recipe.chef = null;
                }
                else if (chefs.TryGetValue(chefId, out ChefData chef))
                {
                    recipe.chef = chef;
                }
                else
                {
                    recipe.chef = null;
                    report.Warn($"Recipes.csv row {r + 1} ({id}): Unknown chef '{chefId}'. Add it to the Chefs sheet.");
                }
            }

            if (recipe.obtainSource == RecipeSource.ChefMail && !recipe.IsChefMail)
            {
                report.Warn($"Recipes.csv row {r + 1} ({id}): Chef Mail recipe needs both 'Chef' and 'Letter'. No letter will be sent.");
            }

            // [v2] ช่องวัตถุดิบ: item_id = ช่องตายตัว / any_xxx = ช่องหมวด
            recipe.slots.Clear();
            recipe.ingredients.Clear();
            int categorySlots = 0;
            foreach (int col in colKeys)
            {
                string itemId = Cell(row, col);
                if (string.IsNullOrEmpty(itemId))
                {
                    continue;
                }

                if (itemId.StartsWith(AnyPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string categoryText = itemId.Substring(AnyPrefix.Length);
                    if (TryParseEnum(categoryText, out ItemCategory anyCategory))
                    {
                        recipe.slots.Add(RecipeSlot.AnyOf(anyCategory));
                        categorySlots++;
                    }
                    else
                    {
                        report.Warn($"Recipes.csv row {r + 1} ({id}): Unknown category slot '{itemId}'. Skipped.");
                    }
                    continue;
                }

                if (items.TryGetValue(itemId, out ItemData item))
                {
                    recipe.slots.Add(RecipeSlot.Fixed(item));
                }
                else
                {
                    report.Warn($"Recipes.csv row {r + 1} ({id}): Unknown item_id '{itemId}'. Skipped.");
                }
            }

            if (recipe.slots.Count == 0)
            {
                report.Warn($"Recipes.csv row {r + 1} ({id}): Recipe has no ingredients.");
            }
            if (categorySlots > 1)
            {
                report.Warn($"Recipes.csv row {r + 1} ({id}): Has {categorySlots} category slots (any_). Only 1 is supported; the first one will be used.");
            }

            // [v2] เมนูต้นทาง ★4 (ตั้งหลังสร้างสูตรครบ เพราะอาจชี้ไปสูตรที่อยู่แถวหลัง)
            if (colBaseRecipe >= 0)
            {
                recipe.baseRecipe = null;
                string baseId = Cell(row, colBaseRecipe);
                if (!string.IsNullOrEmpty(baseId)) pendingBase[recipe] = baseId;
            }

            report.CsvRecipeIds.Add(id);

            // ไอเทมจานอาหารของสูตรนี้
            recipe.dishItem = CreateOrUpdateDish(recipe, items, report);

            // ค่าที่ฟื้นเมื่อกินจานนี้ (เก็บไว้ที่ไอเทมจานอาหาร)
            if (recipe.dishItem != null)
            {
                foodCols.ApplyTo(recipe.dishItem, row);
            }

            EditorUtility.SetDirty(recipe);
        }

        // [v2] รอบสอง: ผูกเมนูต้นทาง
        foreach (KeyValuePair<RecipeData, string> pair in pendingBase)
        {
            if (recipes.TryGetValue(pair.Value, out RecipeData baseRecipe))
            {
                pair.Key.baseRecipe = baseRecipe;
                EditorUtility.SetDirty(pair.Key);
            }
            else
            {
                report.Warn($"Recipes.csv ({pair.Key.recipeId}): Base Recipe '{pair.Value}' not found.");
            }
        }
    }

    // ---------- [v2] ของเก่า ----------

    // วัตถุดิบ/สูตร/จาน ในโปรเจกต์ที่ไม่มีใน CSV แล้ว → พิมพ์รายชื่อ (ไม่ลบ ใช้เมนู Move Obsolete Data To Trash ถ้าต้องการ)
    private static void ReportObsolete(ImportReport report)
    {
        List<string> lines = new List<string>();
        foreach (string path in FindObsoleteAssetPaths(report.CsvItemIds, report.CsvRecipeIds))
        {
            lines.Add(path);
        }

        report.ObsoleteCount = lines.Count;
        if (lines.Count > 0)
        {
            Debug.LogWarning($"RecipeImporter: {lines.Count} asset(s) are not in the CSV anymore (not deleted). " +
                             "Use Tools > Second Serving > Move Obsolete Recipe Data To Trash to remove them.\n" + string.Join("\n", lines));
        }
    }

    // ไฟล์ ItemData (วัตถุดิบ/จาน) และ RecipeData ที่ไม่มีใน CSV (ไม่นับของสวมใส่/เครื่องมือ)
    private static List<string> FindObsoleteAssetPaths(HashSet<string> csvItemIds, HashSet<string> csvRecipeIds)
    {
        List<string> result = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null || string.IsNullOrWhiteSpace(item.itemId)) continue;

            bool isOldIngredient = item.itemType == ItemType.Ingredient && !csvItemIds.Contains(item.itemId);
            bool isOldDish = item.itemType == ItemType.Dish && !csvRecipeIds.Contains(item.itemId);
            if (isOldIngredient || isOldDish) result.Add(path);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:RecipeData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RecipeData recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
            if (recipe != null && !csvRecipeIds.Contains(recipe.recipeId)) result.Add(path);
        }

        return result;
    }

    // [v2] ย้ายของเก่าไปถังขยะของระบบ (กู้คืนได้) แล้ว refresh ฐานข้อมูล
    [MenuItem("Tools/Second Serving/Move Obsolete Recipe Data To Trash")]
    public static void MoveObsoleteToTrash()
    {
        HashSet<string> itemIds = ReadCsvIds(IngredientsCsvPath, ColItemId);
        HashSet<string> recipeIds = ReadCsvIds(RecipesCsvPath, ColRecipeId);
        if (itemIds.Count == 0 || recipeIds.Count == 0)
        {
            EditorUtility.DisplayDialog("Obsolete Recipe Data", "Could not read Ingredients.csv / Recipes.csv.", "OK");
            return;
        }

        List<string> paths = FindObsoleteAssetPaths(itemIds, recipeIds);
        if (paths.Count == 0)
        {
            EditorUtility.DisplayDialog("Obsolete Recipe Data", "Nothing to remove. Everything matches the CSV.", "OK");
            return;
        }

        string preview = string.Join("\n", paths.GetRange(0, Mathf.Min(15, paths.Count)));
        if (paths.Count > 15) preview += $"\n... and {paths.Count - 15} more (see Console)";
        Debug.Log("RecipeImporter: Obsolete assets:\n" + string.Join("\n", paths));

        if (!EditorUtility.DisplayDialog("Obsolete Recipe Data",
                $"Move {paths.Count} asset(s) that are not in the CSV to the system trash?\n" +
                "Old saves that contain these items will skip them when loading.\n\n" + preview,
                "Move To Trash", "Cancel"))
        {
            return;
        }

        List<string> failed = new List<string>();
        AssetDatabase.MoveAssetsToTrash(paths.ToArray(), failed);
        ImportReport report = new ImportReport();
        RefreshDatabases(report);
        AssetDatabase.SaveAssets();

        string message = $"Moved {paths.Count - failed.Count} asset(s) to trash." + (failed.Count > 0 ? $" {failed.Count} failed (see Console)." : "");
        foreach (string path in failed) Debug.LogWarning($"RecipeImporter: Could not move {path} to trash.");
        Debug.Log($"RecipeImporter: {message}");
        EditorUtility.DisplayDialog("Obsolete Recipe Data", message, "OK");
    }

    private static HashSet<string> ReadCsvIds(string path, string[] idColumn)
    {
        HashSet<string> result = new HashSet<string>();
        if (!File.Exists(path)) return result;

        List<List<string>> rows = ReadCsv(path);
        if (rows.Count == 0) return result;

        int col = new CsvHeader(rows[0]).Find(idColumn);
        if (col < 0) return result;

        for (int r = 1; r < rows.Count; r++)
        {
            string id = Cell(rows[r], col);
            if (!string.IsNullOrEmpty(id)) result.Add(id);
        }
        return result;
    }

    // สร้าง/อัปเดตไอเทมจานอาหารของสูตร (itemId = recipeId) ไม่แตะ icon และ description
    private static ItemData CreateOrUpdateDish(RecipeData recipe, Dictionary<string, ItemData> items, ImportReport report)
    {
        string dishId = recipe.recipeId;

        if (items.TryGetValue(dishId, out ItemData dish))
        {
            // กันรหัสชนกับวัตถุดิบ
            if (dish.itemType != ItemType.Dish)
            {
                report.Warn($"Dish id '{dishId}' is already used by a non-dish item ({dish.name}). Dish was not created.");
                return null;
            }

            report.DishesUpdated++;
        }
        else
        {
            EnsureFolder(DishFolder);
            dish = ScriptableObject.CreateInstance<ItemData>();
            dish.itemId = dishId;
            string fileName = MakeFileName(string.IsNullOrEmpty(recipe.recipeName) ? dishId : recipe.recipeName);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DishFolder}/{fileName}.asset");
            AssetDatabase.CreateAsset(dish, path);
            items.Add(dishId, dish);
            report.DishesCreated++;
        }

        dish.itemType = ItemType.Dish;
        dish.itemName = recipe.recipeName;
        dish.nameTh = recipe.nameTh;
        dish.category = ItemCategory.Other;
        dish.sellPrice = recipe.sellPrice;

        EditorUtility.SetDirty(dish);
        return dish;
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

        string compact = text.Replace(" ", "").Replace("_", "").Replace("-", "").Replace("/", "");
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

    // ตำแหน่งคอลัมน์ค่าที่ฟื้นเมื่อกิน (Health / Stamina / Mana / Fever)
    private class FoodColumns
    {
        private int health = -1;
        private int stamina = -1;
        private int mana = -1;
        private int fever = -1;

        public static FoodColumns Find(CsvHeader header)
        {
            return new FoodColumns
            {
                health = header.Find(ColHealth),
                stamina = header.Find(ColStamina),
                mana = header.Find(ColMana),
                fever = header.Find(ColFever)
            };
        }

        // ใส่ค่าให้ไอเทม เฉพาะคอลัมน์ที่มีในไฟล์ (คอลัมน์ไหนไม่มี จะไม่แตะค่าเดิม)
        public void ApplyTo(ItemData item, List<string> row)
        {
            if (item == null)
            {
                return;
            }

            if (health >= 0) item.restoreHealth = ParseInt(Cell(row, health));
            if (stamina >= 0) item.restoreStamina = ParseInt(Cell(row, stamina));
            if (mana >= 0) item.restoreMana = ParseInt(Cell(row, mana));
            if (fever >= 0) item.restoreFever = ParseInt(Cell(row, fever));

            EditorUtility.SetDirty(item);
        }
    }

    // สรุปผลการ import
    private class ImportReport
    {
        public int ItemsCreated;
        public int ItemsUpdated;
        public int RecipesCreated;
        public int RecipesUpdated;
        public int ChefsCreated;
        public int ChefsUpdated;
        public int DishesCreated;
        public int DishesUpdated;
        public int ItemDatabaseCount;
        public int RecipeDatabaseCount;
        public int ObsoleteCount;
        // [v2] รหัสที่อยู่ใน CSV (ใช้หาของเก่า)
        public readonly HashSet<string> CsvItemIds = new HashSet<string>();
        public readonly HashSet<string> CsvRecipeIds = new HashSet<string>();
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
            sb.AppendLine($"Dishes: {DishesCreated} created, {DishesUpdated} updated.");
            sb.AppendLine($"Chefs: {ChefsCreated} created, {ChefsUpdated} updated.");
            if (ObsoleteCount > 0) sb.AppendLine($"Not in CSV anymore: {ObsoleteCount} asset(s) (not deleted, see Console).");
            sb.Append(warnings.Count == 0 ? "No warnings." : $"{warnings.Count} warning(s). See Console.");
            return sb.ToString();
        }
    }
}
