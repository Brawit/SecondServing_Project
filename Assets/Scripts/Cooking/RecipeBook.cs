using System;
using System.Collections.Generic;
using UnityEngine;

// สมุดสูตรของผู้เล่น: จำว่ารู้สูตรไหนแล้วบ้าง
// เริ่มเกมด้วยสูตรที่ติ๊ก In Demo (ชุดสูตรเริ่มต้น) และสูตรใน Extra Starter Recipes
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

    // สูตรที่รู้แล้ว
    private readonly HashSet<RecipeData> known = new HashSet<RecipeData>();

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
}
