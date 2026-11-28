using System;
using System.Collections.Generic;
using UnityEngine;

// สมุดสูตรของผู้เล่น: จำว่ารู้สูตรไหนแล้วบ้าง
// เริ่มเกมด้วยสูตรที่ติ๊ก In Demo (ชุดสูตรเริ่มต้น) และสูตรใน Extra Starter Recipes
// [v2] จำ "วัตถุดิบที่เลือกล่าสุด" ของสูตรที่มีช่องหมวด (recipe → item) ใช้เป็นค่าเริ่มต้นในหน้าต่างทำอาหาร และตอนทำสดในร้าน / เซฟ v9
// ใส่คอมโพเนนต์นี้บนตัว Player (ข้าง Inventory)
public class RecipeBook : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ไฟล์ข้อมูลรวมสูตรอาหาร (Assets/Recipes/RecipeDatabase)")]
    [SerializeField] private RecipeDatabase recipeDatabase;

    [Header("Starter Recipes")]
    [Tooltip("เริ่มเกมโดยรู้สูตรที่ติ๊ก In Demo ทั้งหมด")]
    [SerializeField] private bool startWithDemoRecipes = true;

    [Tooltip("สูตรเริ่มต้นเพิ่มเติม (นอกจากสูตร In Demo)")]
    [SerializeField] private List<RecipeData> extraStarterRecipes = new List<RecipeData>();

    [Header("Debug")]
    [Tooltip("เมนู Debug: Give Ingredients ... ให้วัตถุดิบพอทำสูตรละกี่จาน")]
    [Min(1)] [SerializeField] private int debugServings = 5;

    // สูตรที่รู้แล้ว
    private readonly HashSet<RecipeData> known = new HashSet<RecipeData>();

    // [v2] วัตถุดิบที่เลือกล่าสุดของสูตรที่มีช่องหมวด
    private readonly Dictionary<RecipeData, ItemData> lastChoices = new Dictionary<RecipeData, ItemData>();

    // ---------- Events ----------

    // ส่งสัญญาณเมื่อรายการสูตรเปลี่ยน
    public event Action OnChanged;

    // ส่งสัญญาณเมื่อเรียนรู้สูตรใหม่ระหว่างเล่น
    public event Action<RecipeData> OnRecipeLearned;

    public RecipeDatabase Database => recipeDatabase;
    public IReadOnlyCollection<RecipeData> KnownRecipes => known;

    private void Awake()
    {
        if (recipeDatabase == null)
        {
            Debug.LogWarning("RecipeBook: Recipe Database is not assigned.", this);
        }

        // ใส่สูตรเริ่มต้นไว้ก่อน ถ้ามีเซฟ SaveManager จะแทนที่ด้วยรายการจากเซฟทีหลัง
        ResetToStarterRecipes();
    }

    // รู้สูตรนี้แล้วหรือยัง
    public bool Knows(RecipeData recipe)
    {
        return recipe != null && known.Contains(recipe);
    }

    // เรียนรู้สูตรใหม่ คืนค่า true ถ้าเพิ่งรู้ครั้งแรก
    public bool Learn(RecipeData recipe)
    {
        if (recipe == null || !known.Add(recipe))
        {
            return false;
        }

        Debug.Log($"RecipeBook: Learned {recipe.recipeName}!");
        NotificationUI.Post($"New recipe: {recipe.recipeName}!");
        OnRecipeLearned?.Invoke(recipe);
        OnChanged?.Invoke();
        return true;
    }

    // ตั้งรายการสูตรที่รู้ (ใช้ตอนโหลดเซฟ) สูตรเริ่มต้นจะถูกใส่เพิ่มให้เสมอ เผื่อเพิ่มสูตรเริ่มต้นหลังปล่อยเกม
    public void SetKnown(IEnumerable<RecipeData> recipes)
    {
        known.Clear();
        AddStarterRecipes();

        if (recipes != null)
        {
            foreach (RecipeData recipe in recipes)
            {
                if (recipe != null)
                {
                    known.Add(recipe);
                }
            }
        }

        OnChanged?.Invoke();
    }

    // ---------- [v2] วัตถุดิบที่เลือกล่าสุด ----------

    // ตัวที่เลือกล่าสุดของสูตรนี้ (ไม่เคยเลือก = null) ผู้เรียกต้องเช็กเองว่ายังมีพอไหม (RecipeRequirements.DefaultChoice)
    public ItemData GetLastChoice(RecipeData recipe)
    {
        return recipe != null && lastChoices.TryGetValue(recipe, out ItemData item) ? item : null;
    }

    // จำตัวที่เลือก (ต้องใส่ช่องหมวดของสูตรได้ / item null = ลืม)
    public void SetLastChoice(RecipeData recipe, ItemData item)
    {
        if (recipe == null || !recipe.HasCategorySlot)
        {
            return;
        }

        if (item == null)
        {
            lastChoices.Remove(recipe);
            return;
        }

        if (!recipe.CategorySlot.Accepts(item))
        {
            Debug.LogWarning($"RecipeBook: {item.itemName} doesn't fit the category slot of {recipe.recipeName}. Not remembered.", this);
            return;
        }

        lastChoices[recipe] = item;
    }

    // เขียนตัวที่เลือกล่าสุดลงรายการรหัส (ใช้ตอนเซฟ) สองรายการลำดับเดียวกัน
    public void CaptureChoices(List<string> recipeIds, List<string> itemIds)
    {
        recipeIds.Clear();
        itemIds.Clear();
        foreach (KeyValuePair<RecipeData, ItemData> pair in lastChoices)
        {
            if (pair.Key == null || pair.Value == null) continue;
            if (string.IsNullOrWhiteSpace(pair.Key.recipeId) || string.IsNullOrWhiteSpace(pair.Value.itemId)) continue;
            recipeIds.Add(pair.Key.recipeId);
            itemIds.Add(pair.Value.itemId);
        }
    }

    // ตั้งตัวที่เลือกล่าสุดจากรายการรหัส (ใช้ตอนโหลดเซฟ / null = ล้าง) รหัสที่หาไม่เจอหรือใส่ช่องไม่ได้ = ข้ามพร้อมเตือน
    public void RestoreChoices(IList<string> recipeIds, IList<string> itemIds)
    {
        lastChoices.Clear();
        if (recipeIds == null || itemIds == null)
        {
            return;
        }

        ItemDatabase items = ItemDatabase.Active;
        int count = Mathf.Min(recipeIds.Count, itemIds.Count);
        for (int i = 0; i < count; i++)
        {
            RecipeData recipe = recipeDatabase != null ? recipeDatabase.GetById(recipeIds[i]) : null;
            ItemData item = items != null ? items.GetById(itemIds[i]) : null;
            if (recipe == null || item == null || !recipe.HasCategorySlot || !recipe.CategorySlot.Accepts(item))
            {
                Debug.LogWarning($"RecipeBook: Skipped saved ingredient choice '{recipeIds[i]}' -> '{itemIds[i]}' (not found or doesn't fit).", this);
                continue;
            }
            lastChoices[recipe] = item;
        }
    }

    // กลับไปรู้แค่สูตรเริ่มต้น
    public void ResetToStarterRecipes()
    {
        known.Clear();
        AddStarterRecipes();
        OnChanged?.Invoke();
    }

    private void AddStarterRecipes()
    {
        if (startWithDemoRecipes && recipeDatabase != null)
        {
            foreach (RecipeData recipe in recipeDatabase.Recipes)
            {
                if (recipe != null && recipe.inDemo)
                {
                    known.Add(recipe);
                }
            }
        }

        foreach (RecipeData recipe in extraStarterRecipes)
        {
            if (recipe != null)
            {
                known.Add(recipe);
            }
        }
    }

    // ---------- Debug ----------

    // [v2] คลิกขวาระหว่าง Play: พิมพ์วัตถุดิบที่เลือกล่าสุดของทุกสูตร
    [ContextMenu("Debug: Print Last Ingredient Choices")]
    private void DebugPrintChoices()
    {
        List<string> lines = new List<string>();
        foreach (KeyValuePair<RecipeData, ItemData> pair in lastChoices)
        {
            if (pair.Key != null && pair.Value != null) lines.Add($"{pair.Key.recipeId} -> {pair.Value.itemId}");
        }
        Debug.Log($"RecipeBook: {lines.Count} last ingredient choice(s). {string.Join(", ", lines)}");
    }

    // คลิกขวาที่หัวคอมโพเนนต์ใน Inspector ระหว่าง Play เพื่อเรียนรู้ทุกสูตร
    [ContextMenu("Debug: Learn All Recipes")]
    private void DebugLearnAll()
    {
        if (recipeDatabase == null)
        {
            return;
        }

        foreach (RecipeData recipe in recipeDatabase.Recipes)
        {
            if (recipe != null)
            {
                known.Add(recipe);
            }
        }

        Debug.Log($"RecipeBook: Learned all {known.Count} recipes.");
        OnChanged?.Invoke();
    }

    // [2.5] คลิกขวาระหว่าง Play: ให้วัตถุดิบพอทำสูตรเริ่มต้น (In Demo + Extra Starter) สูตรละ Debug Servings จาน
    [ContextMenu("Debug: Give Starter Ingredients")]
    private void DebugGiveStarterIngredients()
    {
        List<RecipeData> recipes = new List<RecipeData>();
        if (recipeDatabase != null)
        {
            foreach (RecipeData recipe in recipeDatabase.Recipes)
            {
                if (recipe != null && recipe.inDemo) recipes.Add(recipe);
            }
        }
        foreach (RecipeData recipe in extraStarterRecipes)
        {
            if (recipe != null && !recipes.Contains(recipe)) recipes.Add(recipe);
        }

        GiveIngredientsFor(recipes, "starter");
    }

    // [2.5] คลิกขวาระหว่าง Play: ให้วัตถุดิบพอทำทุกสูตรที่รู้ตอนนี้ สูตรละ Debug Servings จาน
    // (ใช้คู่กับ Debug: Learn All Recipes ได้ แต่ของจะติดเพดานกระเป๋าง่าย)
    [ContextMenu("Debug: Give Ingredients For Known Recipes")]
    private void DebugGiveKnownIngredients()
    {
        GiveIngredientsFor(new List<RecipeData>(known), "known");
    }

    // [2.5b] คลิกขวาระหว่าง Play: เรียนรู้ทุกสูตรที่มีขั้น Stir แล้วให้วัตถุดิบสูตรละ Debug Servings จาน
    [ContextMenu("Debug: Learn & Stock Stir Recipes")]
    private void DebugStockStirRecipes()
    {
        LearnAndStockByMinigame(CookingMinigameType.Stir);
    }

    // [2.5b] คลิกขวาระหว่าง Play: เรียนรู้ทุกสูตรที่มีขั้น Whisk แล้วให้วัตถุดิบสูตรละ Debug Servings จาน
    [ContextMenu("Debug: Learn & Stock Whisk Recipes")]
    private void DebugStockWhiskRecipes()
    {
        LearnAndStockByMinigame(CookingMinigameType.Whisk);
    }

    // [2.5b] คลิกขวาระหว่าง Play: เรียนรู้ทุกสูตรที่มีขั้น Shake แล้วให้วัตถุดิบสูตรละ Debug Servings จาน
    [ContextMenu("Debug: Learn & Stock Shake Recipes")]
    private void DebugStockShakeRecipes()
    {
        LearnAndStockByMinigame(CookingMinigameType.Shake);
    }

    // [2.5b] คลิกขวาระหว่าง Play: เรียนรู้ทุกสูตรที่มีขั้น Hold แล้วให้วัตถุดิบสูตรละ Debug Servings จาน
    [ContextMenu("Debug: Learn & Stock Hold Recipes")]
    private void DebugStockHoldRecipes()
    {
        LearnAndStockByMinigame(CookingMinigameType.Hold);
    }

    // หาสูตรที่ขั้นตอนจริง (ขั้นตอนเฉพาะสูตร → Cooking Profile → ตาม Course) มีมินิเกมชนิดนี้ แล้วเรียนรู้ + ให้วัตถุดิบ
    private void LearnAndStockByMinigame(CookingMinigameType type)
    {
        CookingSettings settings = CookingSession.Instance != null ? CookingSession.Instance.Settings : null;
        if (settings == null || recipeDatabase == null)
        {
            Debug.LogWarning("RecipeBook: Needs Play mode with a CookingSession (Cooking Settings assigned) and a Recipe Database.", this);
            return;
        }

        List<RecipeData> recipes = new List<RecipeData>();
        List<CookingStep> steps = new List<CookingStep>();
        foreach (RecipeData recipe in recipeDatabase.Recipes)
        {
            if (recipe == null || recipe.dishItem == null)
            {
                continue;
            }

            settings.ResolveSteps(recipe, steps);
            foreach (CookingStep step in steps)
            {
                if (step != null && step.type == type)
                {
                    recipes.Add(recipe);
                    break;
                }
            }
        }

        if (recipes.Count == 0)
        {
            Debug.LogWarning($"RecipeBook: No recipe uses {type}.", this);
            return;
        }

        // เรียนรู้แบบเงียบ (ไม่ขึ้นแจ้งเตือนทีละสูตร)
        int learned = 0;
        foreach (RecipeData recipe in recipes)
        {
            if (known.Add(recipe)) learned++;
        }
        OnChanged?.Invoke();

        List<string> names = recipes.ConvertAll(r => $"{r.recipeName} (difficulty {r.Difficulty})");
        Debug.Log($"RecipeBook: {recipes.Count} recipes use {type} ({learned} newly learned): {string.Join(", ", names)}");

        GiveIngredientsFor(recipes, type.ToString(), true);
    }

    // [v2] Debug: หาวัตถุดิบตัวแรก (เรียงตาม itemId) ที่ใส่ช่องหมวดนี้ได้
    private static ItemData FindAnyInCategory(RecipeSlot slot)
    {
        ItemDatabase database = ItemDatabase.Active;
        if (database == null) return null;

        ItemData best = null;
        foreach (ItemData item in database.Items)
        {
            if (slot.Accepts(item) && (best == null || string.CompareOrdinal(item.itemId, best.itemId) < 0)) best = item;
        }
        return best;
    }

    // รวมวัตถุดิบที่ทุกสูตรต้องใช้ × จำนวนจาน แล้วใส่กระเป๋า (ใส่ได้ไม่เกินเพดานต่อชนิด ส่วนที่เกินจะแจ้งใน Console)
    // upgradeBagIfNeeded = อัปเกรดกระเป๋าให้เองจนใส่พอ (ใช้กับ Debug ที่ให้ของเยอะ ระดับกระเป๋าจะติดไปในเซฟถ้านอน)
    private void GiveIngredientsFor(List<RecipeData> recipes, string label, bool upgradeBagIfNeeded = false)
    {
        Inventory inventory = GetComponent<Inventory>();
        if (inventory == null)
        {
            Debug.LogWarning("RecipeBook: No Inventory on this GameObject.", this);
            return;
        }

        if (recipes.Count == 0)
        {
            Debug.LogWarning($"RecipeBook: No {label} recipes found.", this);
            return;
        }

        Dictionary<ItemData, int> totals = new Dictionary<ItemData, int>();
        foreach (RecipeData recipe in recipes)
        {
            // [v2] ช่องหมวด: ให้ตัวแรกที่เจอในฐานข้อมูลของหมวดนั้น (Debug)
            ItemData debugChoice = recipe.HasCategorySlot ? FindAnyInCategory(recipe.CategorySlot) : null;
            foreach (KeyValuePair<ItemData, int> pair in RecipeRequirements.Resolve(recipe, debugChoice))
            {
                totals.TryGetValue(pair.Key, out int current);
                totals[pair.Key] = current + pair.Value * debugServings;
            }
        }

        if (upgradeBagIfNeeded)
        {
            int largest = 0;
            foreach (KeyValuePair<ItemData, int> pair in totals)
            {
                largest = Mathf.Max(largest, pair.Value);
            }

            int startLevel = inventory.BagLevel;
            while (inventory.StackCap < largest && inventory.CanUpgradeBag)
            {
                inventory.UpgradeBag();
            }

            if (inventory.BagLevel != startLevel)
            {
                Debug.Log($"RecipeBook: Upgraded bag {startLevel} -> {inventory.BagLevel} (stack cap {inventory.StackCap}) to fit {largest} of one item.");
            }
        }

        int kinds = 0;
        List<string> capped = new List<string>();
        foreach (KeyValuePair<ItemData, int> pair in totals)
        {
            // ของที่มีอยู่แล้วนับรวม: เติมให้ถึงจำนวนที่ต้องใช้เท่านั้น
            int need = pair.Value - inventory.GetCount(pair.Key);
            if (need <= 0)
            {
                continue;
            }

            int amount = Mathf.Min(need, inventory.GetSpaceFor(pair.Key));
            if (amount > 0 && inventory.AddItem(pair.Key, amount))
            {
                kinds++;
            }
            if (amount < need)
            {
                capped.Add($"{pair.Key.itemName} (short {need - amount})");
            }
        }

        Debug.Log($"RecipeBook: Gave ingredients for {recipes.Count} {label} recipes x{debugServings} ({kinds} item types topped up).");
        if (capped.Count > 0)
        {
            Debug.LogWarning($"RecipeBook: Bag stack cap reached for: {string.Join(", ", capped)}. Upgrade the bag or lower Debug Servings.", this);
        }
        NotificationUI.Post($"Debug: ingredients for {recipes.Count} recipes x{debugServings}.");
    }
}
