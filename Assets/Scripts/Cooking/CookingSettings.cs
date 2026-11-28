using System.Collections.Generic;
using UnityEngine;

// ค่าตั้งต้นของระบบทำอาหาร (มีไฟล์เดียวในโปรเจกต์ ผูกไว้ที่ CookingSession)
// - ขั้นตอนตั้งต้นตามประเภทเมนู (Main / Dessert / Drink) ใช้กับสูตรที่ไม่ได้ตั้งขั้นตอนเอง
// - ค่าปกติของมินิเกมแต่ละแบบ (ใช้เมื่อค่าใน CookingStep เป็น 0)
// - ภาพตั้งต้นตามชนิดมินิเกม และเกณฑ์คิดคุณภาพจาน
// - [2.5b] ค่า Hold / Stir / Shake / Whisk และตัวคูณตามความยาก ★1–3 (มินิเกมยากขึ้น + ลูกค้าพอใจมากขึ้น)
// - [v2] ค่าเศรษฐกิจ: ราคาจานแยกแบบ ตัวคูณราคาขาย ดาว × คุณภาพ ค่าพลังตามคุณภาพ เพดานค่าพลังจาน (DishPricing อ่านผ่าน CookingSettings.Active)
// สร้างได้จาก Assets > Create > Cozy > Cooking Settings หรือ Tools > Second Serving > Build Cooking System
[CreateAssetMenu(fileName = "CookingSettings", menuName = "Cozy/Cooking Settings")]
public class CookingSettings : ScriptableObject
{
    [Header("Default steps by course")]
    [Tooltip("ขั้นตอนตั้งต้นของเมนู Main")]
    public CookingProfile mainProfile;

    [Tooltip("ขั้นตอนตั้งต้นของเมนู Dessert")]
    public CookingProfile dessertProfile;

    [Tooltip("ขั้นตอนตั้งต้นของเมนู Drink")]
    public CookingProfile drinkProfile;

    [Header("Timing defaults")]
    [Range(0.05f, 1f)] public float timingZoneWidth = 0.24f;
    [Range(0.01f, 1f)] public float timingPerfectWidth = 0.08f;

    [Tooltip("รอบไป-กลับต่อวินาที (น้อย = ช้า)")]
    [Min(0.1f)] public float timingNeedleSpeed = 0.55f;

    [Min(1)] public int timingHits = 1;

    [Tooltip("โซนเลื่อน: รอบไป-กลับต่อวินาที")]
    [Min(0.05f)] public float timingZoneMoveSpeed = 0.35f;

    [Tooltip("รอกดได้นานเท่านี้ต่อครั้ง ไม่กด = นับเป็นพลาด")]
    [Min(0.5f)] public float timingTimeLimit = 4f;

    [Header("Mash defaults")]
    [Tooltip("กดกี่ครั้งแถบเต็ม (ถ้าไม่หยุดกด)")]
    [Min(1)] public int mashPresses = 8;
    [Min(0.5f)] public float mashTimeLimit = 4f;

    [Tooltip("หยุดกดแล้วแถบลดลงเท่านี้ต่อวินาที (สัดส่วนของแถบ เช่น 0.35 = ลด 35% ต่อวินาที)")]
    [Range(0f, 2f)] public float mashDecayPerSecond = 0.35f;

    [Tooltip("หยุดกดนานเกินเท่านี้ (วินาที) แถบถึงเริ่มลด (0 = ลดทันทีที่ไม่ได้กดในเฟรมนั้น ผู้ใช้เลือก)")]
    [Min(0f)] public float mashDecayDelay = 0f;

    [Tooltip("กดครบภายในสัดส่วนเวลานี้ = Perfect (เช่น 0.6 = ใช้เวลาไม่เกิน 60%)")]
    [Range(0.1f, 1f)] public float mashPerfectTimeRatio = 0.6f;

    [Header("Sequence defaults")]
    [Min(1)] public int sequenceLength = 4;

    [Tooltip("เวลาต่อปุ่ม (วินาที) รวมเป็นเวลานับถอยหลังทั้งลำดับ = ความยาว × ค่านี้")]
    [Min(0.3f)] public float sequenceTimePerKey = 1.8f;

    [Tooltip("ผิดได้ไม่เกินเท่านี้ = Good (ผิด 0 = Perfect)")]
    [Min(0)] public int sequenceMistakesForGood = 1;

    [Tooltip("ข้อความแทนปุ่มขึ้น / ลง / ซ้าย / ขวา (ใช้ตัวอักษรธรรมดาเพราะฟอนต์ TMP อาจไม่มีลูกศร)")]
    public string keyUpText = "^";
    public string keyDownText = "v";
    public string keyLeftText = "<";
    public string keyRightText = ">";

    [Header("Hold defaults (2.5b)")]
    [Tooltip("กดค้างนานเท่านี้ (วินาที) แถบเต็ม (น้อย = เร็ว)")]
    [Min(0.3f)] public float holdFillSeconds = 1.8f;

    [Tooltip("ความกว้างช่วง Good รอบเส้นเป้า (0–1 ของแถบ)")]
    [Range(0.02f, 1f)] public float holdZoneWidth = 0.16f;

    [Tooltip("ความกว้างช่วง Perfect รอบเส้นเป้า (0–1 ของแถบ)")]
    [Range(0.01f, 1f)] public float holdPerfectWidth = 0.06f;

    [Tooltip("ตำแหน่งเส้นเป้าสุ่มระหว่างค่านี้ (0–1 ของแถบ)")]
    [Range(0.2f, 0.95f)] public float holdTargetMin = 0.55f;
    [Range(0.2f, 0.95f)] public float holdTargetMax = 0.85f;

    [Tooltip("ต้องเริ่มกดภายในเวลานี้ (วินาที) ไม่งั้นนับเป็นพลาด")]
    [Min(0.5f)] public float holdTimeLimit = 4f;

    [Header("Stir defaults (2.5b) — คุมกระทะ (A / D) ให้อยู่ใต้อาหารที่วิ่งไปมา")]
    [Tooltip("ความกว้างกระทะ (0–1 ของแถบ)")]
    [Range(0.05f, 1f)] public float stirPanWidth = 0.32f;

    [Tooltip("ความเร็วอาหาร (หน่วยแถบต่อวินาที)")]
    [Min(0.05f)] public float stirFoodSpeed = 0.26f;

    [Tooltip("อาหารเปลี่ยนจุดหมายทุกๆ ช่วงเวลานี้ (วินาที สุ่มระหว่าง min–max)")]
    [Min(0.1f)] public float stirFoodRetargetMin = 0.7f;
    [Min(0.1f)] public float stirFoodRetargetMax = 1.6f;

    [Tooltip("แรงเร่งกระทะตอนกด A / D (หน่วยแถบต่อวินาที²)")]
    [Min(0.1f)] public float stirPanAcceleration = 4f;

    [Tooltip("ความเร็วสูงสุดของกระทะ")]
    [Min(0.1f)] public float stirPanMaxSpeed = 1.1f;

    [Tooltip("ปล่อยปุ่มแล้วกระทะหยุดเร็วแค่ไหน (มาก = หยุดเร็ว แรงเฉื่อยน้อย)")]
    [Min(0f)] public float stirPanFriction = 6f;

    [Tooltip("อาหารอยู่ในกระทะ แถบเพิ่มเท่านี้ต่อวินาที")]
    [Min(0.01f)] public float stirFillPerSecond = 0.42f;

    [Tooltip("อาหารหลุดกระทะ แถบลดเท่านี้ต่อวินาที")]
    [Min(0f)] public float stirDrainPerSecond = 0.16f;

    [Tooltip("แถบเริ่มที่เท่าไร (0–1)")]
    [Range(0f, 0.9f)] public float stirStartProgress = 0.2f;

    [Min(0.5f)] public float stirTimeLimit = 7f;

    [Tooltip("เต็มภายในสัดส่วนเวลานี้ = Perfect")]
    [Range(0.1f, 1f)] public float stirPerfectTimeRatio = 0.55f;

    [Header("Whisk defaults (2.5b) — กดสลับ A / D (ตีผสม)")]
    [Tooltip("กดสลับซ้าย-ขวากี่ครั้งแถบเต็ม (นับทั้งซ้ายและขวา)")]
    [Min(2)] public int whiskPresses = 9;
    [Min(0.5f)] public float whiskTimeLimit = 4.5f;

    [Tooltip("หยุดกดแล้วแถบลดลงเท่านี้ต่อวินาที")]
    [Range(0f, 2f)] public float whiskDecayPerSecond = 0.3f;

    [Tooltip("หยุดกดนานเกินเท่านี้ (วินาที) แถบถึงเริ่มลด (0 = ลดทันทีที่ไม่ได้กดในเฟรมนั้น ผู้ใช้เลือก)")]
    [Min(0f)] public float whiskDecayDelay = 0f;

    [Tooltip("เต็มภายในสัดส่วนเวลานี้ = Perfect")]
    [Range(0.1f, 1f)] public float whiskPerfectTimeRatio = 0.65f;

    [Header("Shake defaults (2.5b)")]
    [Tooltip("กดสลับขึ้น-ลงกี่ครั้งแถบเต็ม (นับทั้งขึ้นและลง)")]
    [Min(2)] public int shakePresses = 10;
    [Min(0.5f)] public float shakeTimeLimit = 4.5f;

    [Tooltip("หยุดกดแล้วแถบลดลงเท่านี้ต่อวินาที")]
    [Range(0f, 2f)] public float shakeDecayPerSecond = 0.35f;

    [Tooltip("หยุดกดนานเกินเท่านี้ (วินาที) แถบถึงเริ่มลด (0 = ลดทันทีที่ไม่ได้กดในเฟรมนั้น ผู้ใช้เลือก)")]
    [Min(0f)] public float shakeDecayDelay = 0f;

    [Tooltip("เต็มภายในสัดส่วนเวลานี้ = Perfect")]
    [Range(0.1f, 1f)] public float shakePerfectTimeRatio = 0.65f;

    [Header("Difficulty (2.5b) — index 0 = ★1, 1 = ★2, 2 = ★3")]
    [Tooltip("คูณความเร็ว (เข็ม Timing, โซนเลื่อน, ความเร็วแถบ Hold, อาหารใน Stir) มาก = ยาก")]
    public float[] difficultySpeed = { 1f, 1.12f, 1.25f };

    [Tooltip("คูณความกว้างโซน (Timing, Hold, กระทะของ Stir) น้อย = ยาก")]
    public float[] difficultyZone = { 1f, 0.9f, 0.78f };

    [Tooltip("คูณจำนวนกด (Mash, Whisk, Shake) มาก = ยาก")]
    public float[] difficultyPresses = { 1f, 1.15f, 1.3f };

    [Tooltip("คูณเวลา (Mash, Stir, Whisk, Shake, Sequence, เวลารอกด) น้อย = ยาก")]
    public float[] difficultyTime = { 1f, 0.95f, 0.88f };

    [Tooltip("เพิ่มความยาวลำดับปุ่ม Sequence")]
    public int[] difficultyExtraKeys = { 0, 0, 1 };

    [Tooltip("คูณความพอใจของลูกค้า (ขั้น 2.6 จะใช้คิดเงิน/ชื่อเสียง) มาก = ได้มาก")]
    public float[] difficultySatisfaction = { 1f, 1.1f, 1.25f };

    [Tooltip("ข้อความดาวความยาก (ฟอนต์ TMP อาจไม่มีตัว ★ ถ้าฟอนต์มี เปลี่ยนเป็น ★ / ☆ ได้)")]
    public string starFilledText = "*";
    public string starEmptyText = "-";

    [Header("Quality")]
    [Tooltip("คะแนนเฉลี่ยทุกขั้น (OK 0 / Good 1 / Perfect 2) ตั้งแต่ค่านี้ = Perfect")]
    [Range(0f, 2f)] public float perfectAverage = 1.6f;

    [Tooltip("คะแนนเฉลี่ยตั้งแต่ค่านี้ = Good (ต่ำกว่า = OK)")]
    [Range(0f, 2f)] public float goodAverage = 0.6f;

    [Header("Economy (v2)")]
    [Tooltip("จานแบบ: ส่วนต่างราคาวัตถุดิบจริงกับค่าเฉลี่ยหมวด คูณเท่านี้ (เท่ากับตัวคูณวัตถุดิบในสูตรราคาจาน)")]
    [Min(0f)] public float variantPriceFactor = DefaultVariantPriceFactor;

    [Tooltip("จานแบบของสูตรลับ: คูณส่วนต่างราคาเพิ่มเท่านี้")]
    [Min(0f)] public float secretPriceFactor = DefaultSecretPriceFactor;

    [Tooltip("จานแบบของ ★4 Fine-dining: คูณส่วนต่างราคาเพิ่มเท่านี้ (ใช้แทนตัวคูณสูตรลับ)")]
    [Min(0f)] public float fineDiningPriceFactor = DefaultFineDiningPriceFactor;

    [Tooltip("จานแบบ: ส่วนต่างค่าพลังวัตถุดิบกับค่าเฉลี่ยหมวด คูณเท่านี้ (x = Health, y = Stamina, z = Mana)")]
    public Vector3 variantStatFactors = DefaultVariantStatFactors;

    [Tooltip("จานแบบของสูตรลับ: คูณส่วนต่างค่าพลังเพิ่มเท่านี้")]
    [Min(0f)] public float secretStatFactor = DefaultSecretStatFactor;

    [Tooltip("เพดานค่าพลังของจาน (x = Health, y = Stamina, z = Mana)")]
    public Vector3Int dishCaps = DefaultDishCaps;

    [Tooltip("เพดาน Health ของจาน ★4 Fine-dining (แทน x ของ Dish Caps)")]
    [Min(0)] public int fineDiningHealthCap = DefaultFineDiningHealthCap;

    [Tooltip("วัตถุดิบที่กินดิบไม่ได้ (ทุกค่าเป็น 0) นับเป็นค่าเท่านี้ตอนทำสุก (x = Health, y = Stamina)")]
    public Vector2Int cookedZeroValues = DefaultCookedZeroValues;

    [Tooltip("ตัวคูณราคาขายจาน ดาว × คุณภาพ เรียงเป็น ★1 OK, ★1 Good, ★1 Perfect, ★2 OK, ... ★4 Perfect (12 ค่า)\n" +
             "ใช้ทั้งขายที่ร้านของชำ กล่องขาย และลูกค้าในร้านจ่าย")]
    public float[] priceMultipliers = (float[])DefaultPriceMultipliers.Clone();

    [Tooltip("ตัวคูณค่า Health / Stamina / Mana ตอนกินจาน ตามคุณภาพ OK, Good, Perfect (ไม่คูณ FEVER)")]
    public float[] qualityStatMultipliers = (float[])DefaultQualityStatMultipliers.Clone();

    [Header("Feedback")]
    [Tooltip("แถบกระพริบสีตามผลกี่วินาที ก่อนไปขั้นถัดไป/ปิด")]
    [Min(0f)] public float resultFlashDuration = 0.5f;

    [Tooltip("รอก่อนเริ่มขั้นแรก (วินาที) ให้ผู้เล่นเห็นชื่อท่าก่อน")]
    [Min(0f)] public float stepIntroDelay = 0.35f;

    [Header("Default visuals by minigame (empty = placeholder)")]
    public Sprite timingVisual;
    public Sprite mashVisual;
    public Sprite sequenceVisual;
    public Sprite holdVisual;
    public Sprite stirVisual;
    public Sprite shakeVisual;
    public Sprite whiskVisual;

    [Header("Default labels")]
    public string timingLabel = "Cook!";
    public string mashLabel = "Chop!";
    public string sequenceLabel = "Season!";
    public string holdLabel = "Pour!";
    public string stirLabel = "Stir!";
    public string shakeLabel = "Shake!";
    public string whiskLabel = "Whisk!";

    // ---------- [v2] ค่าตั้งต้นเศรษฐกิจ (ใช้เมื่อยังไม่มี CookingSettings โหลดอยู่) ----------

    public const float DefaultVariantPriceFactor = 2.4f;
    public const float DefaultSecretPriceFactor = 1.3f;
    public const float DefaultFineDiningPriceFactor = 2.0f;
    public const float DefaultSecretStatFactor = 1.2f;
    public const int DefaultFineDiningHealthCap = 100;
    public static readonly Vector3 DefaultVariantStatFactors = new Vector3(1.5f, 1.5f, 1.2f);
    public static readonly Vector3Int DefaultDishCaps = new Vector3Int(80, 250, 150);
    public static readonly Vector2Int DefaultCookedZeroValues = new Vector2Int(5, 20);

    // ★1–★4 × OK / Good / Perfect
    public static readonly float[] DefaultPriceMultipliers =
    {
        1.00f, 1.10f, 1.25f,
        1.10f, 1.275f, 1.50f,
        1.20f, 1.45f, 1.75f,
        1.30f, 1.625f, 2.00f
    };

    public static readonly float[] DefaultQualityStatMultipliers = { 1f, 1.15f, 1.3f };

    // [v2] ไฟล์ตั้งค่าที่โหลดอยู่ (มีไฟล์เดียวในโปรเจกต์) ให้ DishPricing ใช้
    public static CookingSettings Active { get; private set; }

    private void OnEnable()
    {
        Active = this;
    }

    private void OnValidate()
    {
        priceMultipliers = FixLength(priceMultipliers, DefaultPriceMultipliers);
        qualityStatMultipliers = FixLength(qualityStatMultipliers, DefaultQualityStatMultipliers);
    }

    // อาร์เรย์ยาวไม่ตรง: เติมค่าตั้งต้นให้ครบ (เก็บค่าที่จูนไว้)
    private static float[] FixLength(float[] values, float[] defaults)
    {
        if (values != null && values.Length == defaults.Length) return values;
        float[] fixedArray = (float[])defaults.Clone();
        if (values != null)
        {
            for (int i = 0; i < Mathf.Min(values.Length, fixedArray.Length); i++) fixedArray[i] = values[i];
        }
        return fixedArray;
    }

    // ตัวคูณราคาขาย ดาว (1–4) × คุณภาพ
    public float PriceMultiplier(int stars, DishQuality quality) => PickPriceMultiplier(priceMultipliers, stars, quality);

    // ตัวคูณค่าพลังตามคุณภาพจาน
    public float QualityStatMultiplier(DishQuality quality) => PickQualityStat(qualityStatMultipliers, quality);

    public static float PickPriceMultiplier(float[] table, int stars, DishQuality quality)
    {
        if (table == null || table.Length < DishQualityUtil.Count) table = DefaultPriceMultipliers;
        int rows = table.Length / DishQualityUtil.Count;
        int row = Mathf.Clamp(stars - 1, 0, rows - 1);
        int index = row * DishQualityUtil.Count + Mathf.Clamp((int)quality, 0, DishQualityUtil.Count - 1);
        return Mathf.Max(0f, table[index]);
    }

    public static float PickQualityStat(float[] table, DishQuality quality)
    {
        if (table == null || table.Length == 0) table = DefaultQualityStatMultipliers;
        return Mathf.Max(0f, table[Mathf.Clamp((int)quality, 0, table.Length - 1)]);
    }

    // ---------- [2.5b] ความยาก ----------

    // อ่านค่าในอาร์เรย์ตามความยาก (1–3) ถ้าอาร์เรย์สั้นกว่า ใช้ตัวสุดท้าย / ว่าง = fallback
    private static float Pick(float[] values, int difficulty, float fallback)
    {
        if (values == null || values.Length == 0) return fallback;
        int i = Mathf.Clamp(difficulty - 1, 0, values.Length - 1);
        return values[i] > 0f ? values[i] : fallback;
    }

    private static int Pick(int[] values, int difficulty)
    {
        if (values == null || values.Length == 0) return 0;
        return Mathf.Max(0, values[Mathf.Clamp(difficulty - 1, 0, values.Length - 1)]);
    }

    public float SpeedScale(int difficulty) => Pick(difficultySpeed, difficulty, 1f);
    public float ZoneScale(int difficulty) => Pick(difficultyZone, difficulty, 1f);
    public float PressScale(int difficulty) => Pick(difficultyPresses, difficulty, 1f);
    public float TimeScale(int difficulty) => Pick(difficultyTime, difficulty, 1f);
    public int ExtraKeys(int difficulty) => Pick(difficultyExtraKeys, difficulty);
    public float SatisfactionScale(int difficulty) => Pick(difficultySatisfaction, difficulty, 1f);

    // ดาวความยาก เช่น "**-"
    public string Stars(int difficulty)
    {
        int d = Mathf.Clamp(difficulty, 0, RecipeData.MaxDifficulty);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < RecipeData.MaxDifficulty; i++)
        {
            sb.Append(i < d ? starFilledText : starEmptyText);
        }
        return sb.ToString();
    }

    // ---------- ขั้นตอนของสูตร ----------

    // ขั้นตอนของสูตรนี้ (customSteps → cookingProfile → ตาม Course) คัดลอกใส่ result
    public void ResolveSteps(RecipeData recipe, List<CookingStep> result)
    {
        result.Clear();
        if (recipe == null)
        {
            return;
        }

        List<CookingStep> source = null;
        if (recipe.customSteps != null && recipe.customSteps.Count > 0)
        {
            source = recipe.customSteps;
        }
        else if (recipe.cookingProfile != null && recipe.cookingProfile.HasSteps)
        {
            source = recipe.cookingProfile.steps;
        }
        else
        {
            CookingProfile byCourse = GetCourseProfile(recipe.course);
            if (byCourse != null && byCourse.HasSteps)
            {
                source = byCourse.steps;
            }
        }

        if (source != null)
        {
            foreach (CookingStep step in source)
            {
                if (step != null) result.Add(step.Clone());
            }
        }

        // ไม่มีอะไรเลย: อย่างน้อยให้มี 1 ขั้น เกมจะได้ไม่ค้าง
        if (result.Count == 0)
        {
            result.Add(CookingStep.Create(CookingMinigameType.Timing, ""));
        }
    }

    public CookingProfile GetCourseProfile(DishCourse course)
    {
        switch (course)
        {
            case DishCourse.Dessert: return dessertProfile;
            case DishCourse.Drink: return drinkProfile;
            default: return mainProfile;
        }
    }

    // ---------- ภาพ / ชื่อท่า ----------

    // ภาพของขั้นที่ index ของสูตรนี้ (ขั้นตอน → Profile ของสูตร ขั้นลำดับเดียวกันและชนิดเดียวกัน → ตั้งต้นตามชนิด) null = placeholder
    public Sprite ResolveVisual(RecipeData recipe, IList<CookingStep> steps, int index)
    {
        if (steps == null || index < 0 || index >= steps.Count || steps[index] == null)
        {
            return null;
        }

        CookingStep step = steps[index];
        if (step.visual != null)
        {
            return step.visual;
        }

        // ขั้นตอนเฉพาะสูตรที่ยังไม่มีภาพ: ยืมภาพจาก Profile ของสูตร ถ้าขั้นเดียวกันเป็นชนิดเดียวกัน
        bool usesCustom = recipe != null && recipe.customSteps != null && recipe.customSteps.Count > 0;
        if (usesCustom && recipe.cookingProfile != null && recipe.cookingProfile.HasSteps && index < recipe.cookingProfile.steps.Count)
        {
            CookingStep profileStep = recipe.cookingProfile.steps[index];
            if (profileStep != null && profileStep.type == step.type && profileStep.visual != null)
            {
                return profileStep.visual;
            }
        }

        return GetDefaultVisual(step.type);
    }

    public Sprite GetDefaultVisual(CookingMinigameType type)
    {
        switch (type)
        {
            case CookingMinigameType.Mash: return mashVisual;
            case CookingMinigameType.Sequence: return sequenceVisual;
            case CookingMinigameType.Hold: return holdVisual != null ? holdVisual : timingVisual;
            case CookingMinigameType.Stir: return stirVisual != null ? stirVisual : mashVisual;
            case CookingMinigameType.Shake: return shakeVisual != null ? shakeVisual : mashVisual;
            case CookingMinigameType.Whisk: return whiskVisual != null ? whiskVisual : mashVisual;
            default: return timingVisual;
        }
    }

    public string GetLabel(CookingStep step)
    {
        if (step == null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(step.label))
        {
            return step.label;
        }

        switch (step.type)
        {
            case CookingMinigameType.Mash: return mashLabel;
            case CookingMinigameType.Sequence: return sequenceLabel;
            case CookingMinigameType.Hold: return holdLabel;
            case CookingMinigameType.Stir: return stirLabel;
            case CookingMinigameType.Shake: return shakeLabel;
            case CookingMinigameType.Whisk: return whiskLabel;
            default: return timingLabel;
        }
    }

    // ---------- คุณภาพ ----------

    public static int Score(DishQuality quality)
    {
        switch (quality)
        {
            case DishQuality.Perfect: return 2;
            case DishQuality.Good: return 1;
            default: return 0;
        }
    }

    // รวมผลทุกขั้นเป็นคุณภาพจาน
    public DishQuality Combine(IList<DishQuality> results)
    {
        if (results == null || results.Count == 0)
        {
            return DishQuality.Good;
        }

        float total = 0f;
        foreach (DishQuality q in results)
        {
            total += Score(q);
        }

        float average = total / results.Count;
        if (average >= perfectAverage) return DishQuality.Perfect;
        if (average >= goodAverage) return DishQuality.Good;
        return DishQuality.Ok;
    }

    // ---------- อ่านค่า (ค่าในขั้นตอน 0 = ค่าปกติ) แล้วคูณตามความยาก (1–3) ----------

    public float ZoneWidth(CookingStep s, int d) => Mathf.Clamp(Raw(s?.zoneWidth, timingZoneWidth) * ZoneScale(d), 0.02f, 1f);
    public float PerfectWidth(CookingStep s, int d) => Mathf.Min(ZoneWidth(s, d), Mathf.Clamp(Raw(s?.perfectWidth, timingPerfectWidth) * ZoneScale(d), 0.01f, 1f));
    public float NeedleSpeed(CookingStep s, int d) => Raw(s?.needleSpeed, timingNeedleSpeed) * SpeedScale(d);
    public float ZoneMoveSpeed(int d) => timingZoneMoveSpeed * SpeedScale(d);
    public int Hits(CookingStep s) => s != null && s.hits > 0 ? s.hits : timingHits;
    public float TimingTimeLimit(CookingStep s, int d) => Mathf.Max(0.5f, Raw(s?.timeLimit, timingTimeLimit) * TimeScale(d));

    public int MashPresses(CookingStep s, int d) => Mathf.Max(1, Mathf.RoundToInt((s != null && s.presses > 0 ? s.presses : mashPresses) * PressScale(d)));
    public float MashTimeLimit(CookingStep s, int d) => Mathf.Max(0.5f, Raw(s?.timeLimit, mashTimeLimit) * TimeScale(d));

    public int SequenceLength(CookingStep s, int d) => (s != null && s.length > 0 ? s.length : sequenceLength) + ExtraKeys(d);
    public float SequenceTimePerKey(CookingStep s, int d) => Raw(s?.timePerKey, sequenceTimePerKey) * TimeScale(d);

    // Hold: ใช้ zoneWidth / perfectWidth / needleSpeed ของขั้น (needleSpeed = คูณความเร็วแถบ) / timeLimit = เวลารอเริ่มกด
    public float HoldZoneWidth(CookingStep s, int d) => Mathf.Clamp(Raw(s?.zoneWidth, holdZoneWidth) * ZoneScale(d), 0.02f, 1f);
    public float HoldPerfectWidth(CookingStep s, int d) => Mathf.Min(HoldZoneWidth(s, d), Mathf.Clamp(Raw(s?.perfectWidth, holdPerfectWidth) * ZoneScale(d), 0.01f, 1f));
    public float HoldFillSeconds(CookingStep s, int d) => Mathf.Max(0.2f, holdFillSeconds / (Raw(s?.needleSpeed, 1f) * SpeedScale(d)));
    public float HoldTimeLimit(CookingStep s, int d) => Mathf.Max(0.5f, Raw(s?.timeLimit, holdTimeLimit) * TimeScale(d));

    // Stir: zoneWidth = ความกว้างกระทะ / needleSpeed = ตัวคูณความเร็วอาหาร / timeLimit = เวลา
    public float StirPanWidth(CookingStep s, int d) => Mathf.Clamp(Raw(s?.zoneWidth, stirPanWidth) * ZoneScale(d), 0.05f, 1f);
    public float StirFoodSpeed(CookingStep s, int d) => stirFoodSpeed * Raw(s?.needleSpeed, 1f) * SpeedScale(d);
    public float StirTimeLimit(CookingStep s, int d) => Mathf.Max(0.5f, Raw(s?.timeLimit, stirTimeLimit) * TimeScale(d));

    // Whisk: presses = จำนวนกดสลับ / timeLimit = เวลา
    public int WhiskPresses(CookingStep s, int d) => Mathf.Max(2, Mathf.RoundToInt((s != null && s.presses > 0 ? s.presses : whiskPresses) * PressScale(d)));
    public float WhiskTimeLimit(CookingStep s, int d) => Mathf.Max(0.5f, Raw(s?.timeLimit, whiskTimeLimit) * TimeScale(d));

    // Shake: presses = จำนวนกดสลับขึ้น-ลง / timeLimit = เวลา
    public int ShakePresses(CookingStep s, int d) => Mathf.Max(2, Mathf.RoundToInt((s != null && s.presses > 0 ? s.presses : shakePresses) * PressScale(d)));
    public float ShakeTimeLimit(CookingStep s, int d) => Mathf.Max(0.5f, Raw(s?.timeLimit, shakeTimeLimit) * TimeScale(d));

    // ค่าในขั้นตอน (ถ้า > 0) ไม่งั้นค่าปกติ
    private static float Raw(float? stepValue, float fallback) => stepValue.HasValue && stepValue.Value > 0f ? stepValue.Value : fallback;
}
