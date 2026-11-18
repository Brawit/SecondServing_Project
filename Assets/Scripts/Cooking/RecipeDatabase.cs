using System.Collections.Generic;
using UnityEngine;

// ไฟล์ข้อมูลรวมสูตรอาหารทุกสูตร สร้างได้จาก Assets > Create > Cozy > Recipe Database (ควรมีไฟล์เดียว)
// ตัว import จะสร้างให้อัตโนมัติที่ Assets/Recipes/RecipeDatabase.asset ถ้ายังไม่มี
[CreateAssetMenu(fileName = "RecipeDatabase", menuName = "Cozy/Recipe Database")]
public class RecipeDatabase : ScriptableObject
{
    [Header("Recipes")]
    [Tooltip("สูตรทุกสูตรในเกม (คลิกขวาที่หัวคอมโพเนนต์ > Find All Recipes เพื่อดึงเข้ามาอัตโนมัติ)")]
    [SerializeField] private List<RecipeData> recipes = new List<RecipeData>();

    // ตารางค้นหา รหัส -> สูตร สร้างตอนเรียกใช้ครั้งแรก
    private Dictionary<string, RecipeData> lookup;

    public IReadOnlyList<RecipeData> Recipes => recipes;

    // หาสูตรจากรหัส คืนค่า null ถ้าไม่เจอ
    public RecipeData GetById(string recipeId)
    {
        if (string.IsNullOrWhiteSpace(recipeId))
        {
            return null;
        }

        if (lookup == null)
        {
            BuildLookup();
        }

        lookup.TryGetValue(recipeId, out RecipeData recipe);
        return recipe;
    }

    // กระเป๋ามีวัตถุดิบพอทำสูตรนี้ 1 จานหรือไม่
    public static bool CanCook(RecipeData recipe, Inventory inventory)
    {
        return GetMaxServings(recipe, inventory) > 0;
    }

    // วัตถุดิบในกระเป๋าทำสูตรนี้ได้กี่จาน (0 = ทำไม่ได้)
    public static int GetMaxServings(RecipeData recipe, Inventory inventory)
    {
        if (recipe == null || inventory == null)
        {
            return 0;
        }

        Dictionary<ItemData, int> required = recipe.GetRequiredCounts();

        // สูตรที่ไม่มีวัตถุดิบเลย ถือว่าทำไม่ได้ (น่าจะลืมใส่)
        if (required.Count == 0)
        {
            return 0;
        }

        int maxServings = int.MaxValue;
        foreach (KeyValuePair<ItemData, int> pair in required)
        {
            int servingsFromThis = inventory.GetCount(pair.Key) / pair.Value;
            if (servingsFromThis < maxServings)
            {
                maxServings = servingsFromThis;
            }
        }

        return maxServings;
    }

    // เติมรายการสูตรที่ทำได้ตอนนี้ลงใน result (ล้างของเดิมก่อน) includeSecret = รวมสูตรลับด้วย
    public void GetCookable(Inventory inventory, List<RecipeData> result, bool includeSecret = true)
    {
        if (result == null)
        {
            return;
        }

        result.Clear();

        foreach (RecipeData recipe in recipes)
        {
            if (recipe == null)
            {
                continue;
            }

            if (!includeSecret && recipe.isSecret)
            {
                continue;
            }

            if (CanCook(recipe, inventory))
            {
                result.Add(recipe);
            }
        }
    }

    // สร้างตารางค้นหาใหม่ และเตือนถ้ามีรหัสว่างหรือซ้ำ
    private void BuildLookup()
    {
        lookup = new Dictionary<string, RecipeData>();

        foreach (RecipeData recipe in recipes)
        {
            if (recipe == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(recipe.recipeId))
            {
                Debug.LogWarning($"RecipeDatabase: Recipe '{recipe.name}' has no recipeId.", recipe);
                continue;
            }

            if (lookup.ContainsKey(recipe.recipeId))
            {
                Debug.LogWarning($"RecipeDatabase: Duplicate recipeId '{recipe.recipeId}' ({lookup[recipe.recipeId].name} and {recipe.name}).", recipe);
                continue;
            }

            lookup.Add(recipe.recipeId, recipe);
        }
    }

    private void OnValidate()
    {
        BuildLookup();
    }

    private void OnEnable()
    {
        lookup = null;
    }

#if UNITY_EDITOR
    [ContextMenu("Find All Recipes")]
    private void FindAllRecipes()
    {
        RefreshFromProject();
    }

    // ดึง RecipeData ทุกตัวในโปรเจกต์เข้ามาในรายการ คืนค่าจำนวนที่เจอ
    public int RefreshFromProject()
    {
        recipes.Clear();

        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:RecipeData");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            RecipeData recipe = UnityEditor.AssetDatabase.LoadAssetAtPath<RecipeData>(path);
            if (recipe != null)
            {
                recipes.Add(recipe);
            }
        }

        BuildLookup();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"RecipeDatabase: Found {recipes.Count} recipes.");
        return recipes.Count;
    }
#endif
}
