using System;
using UnityEngine;

// [v2] ราคาและค่าพลังของจาน (static ล้วน ค่าคงที่ทั้งหมดอยู่ใน CookingSettings หัวข้อ Economy (v2))
// - จานที่ไม่มีช่องหมวด: ใช้ราคา/ค่าพลังจากชีตตรงๆ (ห้ามคำนวณทับ)
// - จานแบบ (มีช่องหมวด): ราคาชีตคิดจากค่าเฉลี่ยหมวด → ปรับตามราคาวัตถุดิบจริงที่ใส่
//   ราคาแบบ = round5(ราคาชีต + (ราคาวัตถุดิบ − ค่าเฉลี่ยหมวด) × 2.4 × (★4 2.0 / สูตรลับ 1.3 / ปกติ 1.0)) ไม่ต่ำกว่า 5
//   ค่าพลัง = ค่าชีต + (ค่าวัตถุดิบสุก − ค่าเฉลี่ยหมวด) × (H 1.5 / S 1.5 / M 1.2) × (สูตรลับ 1.2) ตัดเพดาน H 80 (★4 100) / S 250 / M 150
// - ราคาขาย = ราคาจาน × ตัวคูณ ดาว × คุณภาพ ปัดลง / ค่าพลังตอนกินจาน × คุณภาพ (OK 1.00 / Good 1.15 / Perfect 1.30) ไม่คูณ FEVER
public static class DishPricing
{
    // ตั้งค่าที่ใช้ (ไม่มี = ใช้ค่าตั้งต้น)
    private static CookingSettings Settings => CookingSettings.Active;

    // ---------- จานแบบ ----------

    // ตัวคูณส่วนต่างราคาของสูตรนี้ (★4 / สูตรลับ / ปกติ)
    public static float RecipePriceFactor(RecipeData recipe)
    {
        CookingSettings s = Settings;
        if (recipe == null) return 1f;
        if (recipe.Difficulty >= RecipeData.MaxDifficulty) return s != null ? s.fineDiningPriceFactor : CookingSettings.DefaultFineDiningPriceFactor;
        if (recipe.isSecret) return s != null ? s.secretPriceFactor : CookingSettings.DefaultSecretPriceFactor;
        return 1f;
    }

    // ราคาของจานแบบ (ingredient = วัตถุดิบที่ใส่ช่องหมวด) ไม่มีช่องหมวด/ไม่มีวัตถุดิบ = ราคาชีต
    public static int VariantPrice(RecipeData recipe, ItemData ingredient)
    {
        if (recipe == null)
        {
            return 0;
        }

        if (!recipe.HasCategorySlot || ingredient == null)
        {
            return recipe.sellPrice;
        }

        CookingSettings s = Settings;
        float factor = s != null ? s.variantPriceFactor : CookingSettings.DefaultVariantPriceFactor;
        float average = CategoryStats.AveragePrice(recipe.CategorySlot.category);
        float price = recipe.sellPrice + (ingredient.sellPrice - average) * factor * RecipePriceFactor(recipe);
        return Mathf.Max(5, Round5(price));
    }

    // ค่าฟื้นของจานแบบ (x = Health, y = Stamina, z = Mana) FEVER = ค่าของจานแม่เสมอ
    public static Vector3Int VariantRestore(RecipeData recipe, ItemData ingredient)
    {
        if (recipe == null)
        {
            return Vector3Int.zero;
        }

        ItemData dish = recipe.dishItem;
        Vector3 baseRestore = dish != null
            ? new Vector3(dish.restoreHealth, dish.restoreStamina, dish.restoreMana)
            : Vector3.zero;

        if (!recipe.HasCategorySlot || ingredient == null)
        {
            return new Vector3Int(Mathf.RoundToInt(baseRestore.x), Mathf.RoundToInt(baseRestore.y), Mathf.RoundToInt(baseRestore.z));
        }

        CookingSettings s = Settings;
        Vector3 factors = s != null ? s.variantStatFactors : CookingSettings.DefaultVariantStatFactors;
        float secret = recipe.isSecret ? (s != null ? s.secretStatFactor : CookingSettings.DefaultSecretStatFactor) : 1f;

        Vector3 diff = CategoryStats.CookedRestore(ingredient) - CategoryStats.AverageRestore(recipe.CategorySlot.category);
        Vector3 result = baseRestore + Vector3.Scale(diff, factors) * secret;

        Vector3Int caps = DishCaps(recipe);
        return new Vector3Int(
            Mathf.Clamp(RoundHalfUp(result.x), 0, caps.x),
            Mathf.Clamp(RoundHalfUp(result.y), 0, caps.y),
            Mathf.Clamp(RoundHalfUp(result.z), 0, caps.z));
    }

    // เพดานค่าพลังของจานจากสูตรนี้ (★4 ใช้เพดาน Health ของ Fine-dining)
    public static Vector3Int DishCaps(RecipeData recipe)
    {
        CookingSettings s = Settings;
        Vector3Int caps = s != null ? s.dishCaps : CookingSettings.DefaultDishCaps;
        if (recipe != null && recipe.Difficulty >= RecipeData.MaxDifficulty)
        {
            caps.x = s != null ? s.fineDiningHealthCap : CookingSettings.DefaultFineDiningHealthCap;
        }
        return caps;
    }

    // ---------- ราคาขาย ----------

    // ราคาขายต่อชิ้น: จาน = ราคาจาน × ตัวคูณ (ดาวของสูตรแม่ × คุณภาพ) ปัดลง / ของอื่น = ราคาเต็ม
    public static int SalePrice(ItemData item, DishQuality quality)
    {
        if (item == null || item.sellPrice <= 0)
        {
            return 0;
        }

        if (!Inventory.TracksQuality(item))
        {
            return item.sellPrice;
        }

        RecipeData recipe = RecipeOf(item);
        int stars = recipe != null ? recipe.Difficulty : 1;
        return PriceFor(item.sellPrice, stars, quality);
    }

    // ราคาฐาน × ตัวคูณ ดาว × คุณภาพ ปัดลง (ใช้ใน Debug ตอนยังไม่มีไอเทมจริง)
    public static int PriceFor(int basePrice, int stars, DishQuality quality)
    {
        if (basePrice <= 0) return 0;
        return Mathf.FloorToInt(basePrice * PriceMultiplier(stars, quality) + 0.0001f);
    }

    public static float PriceMultiplier(int stars, DishQuality quality)
    {
        CookingSettings s = Settings;
        return s != null ? s.PriceMultiplier(stars, quality) : CookingSettings.PickPriceMultiplier(CookingSettings.DefaultPriceMultipliers, stars, quality);
    }

    public static float QualityStatMultiplier(DishQuality quality)
    {
        CookingSettings s = Settings;
        return s != null ? s.QualityStatMultiplier(quality) : CookingSettings.PickQualityStat(CookingSettings.DefaultQualityStatMultipliers, quality);
    }

    // ---------- กิน ----------

    // ค่าที่ได้เมื่อกิน 1 ชิ้น (จานที่ระบุคุณภาพ: H/S/M × ตัวคูณคุณภาพ หลังตัดเพดานจาน / FEVER ไม่คูณ / วัตถุดิบดิบไม่คูณ)
    public static void EatValues(ItemData item, DishQuality? quality, out int health, out int stamina, out int mana, out int fever)
    {
        health = stamina = mana = fever = 0;
        if (item == null)
        {
            return;
        }

        health = Mathf.Max(0, item.restoreHealth);
        stamina = Mathf.Max(0, item.restoreStamina);
        mana = Mathf.Max(0, item.restoreMana);
        fever = Mathf.Max(0, item.restoreFever);

        if (!Inventory.TracksQuality(item) || !quality.HasValue)
        {
            return;
        }

        Vector3Int caps = DishCaps(RecipeOf(item));
        float multiplier = QualityStatMultiplier(quality.Value);
        health = RoundHalfUp(Mathf.Min(health, caps.x) * multiplier);
        stamina = RoundHalfUp(Mathf.Min(stamina, caps.y) * multiplier);
        mana = RoundHalfUp(Mathf.Min(mana, caps.z) * multiplier);
    }

    // ---------- ตัวช่วย ----------

    // สูตรแม่ของจาน (จานแบบ = สูตรที่สร้าง / จานแม่ = หาจาก RecipeDatabase) ไม่ใช่จาน/ไม่เจอ = null
    public static RecipeData RecipeOf(ItemData dish)
    {
        if (dish == null || dish.itemType != ItemType.Dish) return null;
        if (dish.variantRecipe != null) return dish.variantRecipe;
        return RecipeDatabase.Active != null ? RecipeDatabase.Active.FindByDish(dish) : null;
    }

    // ปัดเป็นทวีคูณของ 5 (ครึ่งปัดขึ้น แบบ ROUND ในชีต)
    public static int Round5(float value)
    {
        return (int)Math.Round(value / 5.0, MidpointRounding.AwayFromZero) * 5;
    }

    private static int RoundHalfUp(float value)
    {
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
