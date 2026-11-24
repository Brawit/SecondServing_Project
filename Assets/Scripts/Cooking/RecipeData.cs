using System.Collections.Generic;
using UnityEngine;

// ประเทศ/สัญชาติของเมนู
// หมายเหตุ: เพิ่มค่าใหม่ต่อท้ายเท่านั้น ห้ามสลับลำดับ เพราะ Unity เก็บเป็นตัวเลข
public enum Cuisine
{
    Thailand,
    Japan,
    China,
    Italy,
    USA,
    Mexico,
    HouseSpecial
}

// ประเภทของเมนู
// หมายเหตุ: เพิ่มค่าใหม่ต่อท้ายเท่านั้น ห้ามสลับลำดับ
public enum DishCourse
{
    Main,
    Dessert,
    Drink
}

// สูตรอาหาร 1 เมนู สร้างได้จาก Assets > Create > Cozy > Recipe Data
// หรือสร้าง/อัปเดตทั้งหมดจาก CSV ผ่านเมนู Tools > Second Serving > Import Recipes & Ingredients
[CreateAssetMenu(fileName = "NewRecipe", menuName = "Cozy/Recipe Data")]
public class RecipeData : ScriptableObject
{
    // จำนวนวัตถุดิบหลักสูงสุดต่อสูตร
    public const int MaxIngredients = 4;

    // ความยากสูงสุด (★)
    public const int MaxDifficulty = 3;

    [Header("Save")]
    [Tooltip("รหัสประจำสูตร ใช้ในไฟล์เซฟ ต้องไม่ซ้ำ และไม่ควรเปลี่ยนหลังปล่อยเกม (เว้นว่างไว้ จะถูกตั้งจากชื่อไฟล์ให้อัตโนมัติ)")]
    public string recipeId;

    [Header("Info")]
    [Tooltip("ชื่อที่แสดงในเกม (ตอนนี้ใช้ชื่ออังกฤษ จนกว่าจะมีฟอนต์ไทย)")]
    public string recipeName = "New Recipe";

    [Tooltip("ชื่อภาษาไทย (เก็บไว้ใช้ตอนมีฟอนต์ไทยใน TMP)")]
    public string nameTh;

    public Sprite icon;

    public Cuisine cuisine = Cuisine.Thailand;
    public DishCourse course = DishCourse.Main;

    [Tooltip("สูตรลับ (ต้องปลดล็อกพิเศษ)")]
    public bool isSecret = false;

    [Tooltip("ใส่ในเดโมบนเว็บ (Vertical Slice)")]
    public bool inDemo = false;

    [Header("Result")]
    [Tooltip("ไอเทมจานอาหารที่ได้เมื่อทำสูตรนี้ (ตัว import สร้างให้อัตโนมัติ)")]
    public ItemData dishItem;

    [Header("Ingredients")]
    [Tooltip("วัตถุดิบหลักที่ต้องใช้ อย่างละ 1 ชิ้น ใส่ซ้ำได้ถ้าต้องใช้มากกว่า 1 ชิ้น (สูงสุด 4 ช่อง)")]
    public List<ItemData> ingredients = new List<ItemData>();

    [Tooltip("รายการวัตถุดิบเต็มสำหรับแสดงในสมุดสูตร (ของในตู้กับข้าวไม่ต้องมีจริง)")]
    [TextArea(2, 5)] public string fullIngredientsText;

    [Header("Economy")]
    [Tooltip("ราคาขายในร้าน (0 = ยังไม่ได้ตั้ง)")]
    [Min(0)] public int sellPrice = 0;

    [Header("Cooking (2.5)")]
    [Tooltip("ความยาก ★1–3 (มาจากคอลัมน์ Difficulty ในตาราง) 0 = คิดเอง: สูตรเริ่มต้น ★1 / สูตรทั่วไป ★2 / สูตรลับ ★3\n" +
             "ความยากทำให้มินิเกมยากขึ้น และลูกค้าพอใจมากขึ้น (CookingSettings)")]
    [Range(0, MaxDifficulty)] public int difficulty = 0;

    [Tooltip("ชุดท่าทำอาหารที่ใช้ร่วมกับสูตรอื่น (เว้นว่าง = ใช้ค่าตั้งต้นตาม Course ใน CookingSettings) ตัว import ไม่เขียนทับช่องนี้")]
    public CookingProfile cookingProfile;

    [Tooltip("ขั้นตอนเฉพาะสูตรนี้ (ไม่ว่าง = ใช้แทน Cooking Profile) ใช้กับจานเด่น/สูตรลับ ตัว import ไม่เขียนทับช่องนี้")]
    public List<CookingStep> customSteps = new List<CookingStep>();

    [Header("Obtain (2.5c)")]
    [Tooltip("ทางที่ผู้เล่นได้สูตรนี้ (คอลัมน์ Obtain) ตอนนี้ทำงานจริงแค่ Starter กับ Chef Mail")]
    public RecipeSource obtainSource = RecipeSource.Starter;

    [Tooltip("โน้ตสั้นๆ ว่าทำไมได้จากทางนี้ (คอลัมน์ Obtain Note) ไม่แสดงในเกม")]
    public string obtainNote;

    [Tooltip("Chef Mail: เลเวลร้านขั้นต่ำ (คอลัมน์ Mail Shop Lv)")]
    [Min(0)] public int mailShopLevel = 0;

    [Tooltip("Chef Mail: ชื่อเสียงขั้นต่ำ (คอลัมน์ Mail Reputation)")]
    [Min(0)] public int mailReputation = 0;

    [Tooltip("Chef Mail: เชฟที่ส่งจดหมาย (คอลัมน์ Chef)")]
    public ChefData chef;

    [Tooltip("Chef Mail: เนื้อจดหมาย ภาษาอังกฤษ (คอลัมน์ Letter)")]
    [TextArea(4, 12)] public string letterBody;

    // [2.5c] สูตรนี้ได้จากจดหมายเชฟ และข้อมูลครบพอส่งจดหมาย
    public bool IsChefMail => obtainSource == RecipeSource.ChefMail && chef != null && !string.IsNullOrWhiteSpace(letterBody);

    // [2.5b] ความยากที่ใช้จริง (1–3) ช่อง difficulty เป็น 0 = คิดเองจากประเภทสูตร
    public int Difficulty
    {
        get
        {
            if (difficulty > 0) return Mathf.Min(difficulty, MaxDifficulty);
            if (inDemo) return 1;
            return isSecret ? 3 : 2;
        }
    }

    // นับว่าแต่ละวัตถุดิบต้องใช้กี่ชิ้น (รวมช่องที่ใส่ซ้ำ) ข้ามช่องว่าง
    public Dictionary<ItemData, int> GetRequiredCounts()
    {
        Dictionary<ItemData, int> result = new Dictionary<ItemData, int>();

        foreach (ItemData item in ingredients)
        {
            if (item == null)
            {
                continue;
            }

            result.TryGetValue(item, out int current);
            result[item] = current + 1;
        }

        return result;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // ถ้ายังไม่มีรหัส ให้ตั้งจากชื่อไฟล์
        if (string.IsNullOrWhiteSpace(recipeId) && !string.IsNullOrEmpty(name))
        {
            recipeId = name.Trim().ToLowerInvariant().Replace(' ', '_');
            UnityEditor.EditorUtility.SetDirty(this);
        }

        if (ingredients.Count > MaxIngredients)
        {
            Debug.LogWarning($"RecipeData '{name}': Has {ingredients.Count} ingredients (max {MaxIngredients}).", this);
        }
    }
#endif
}
