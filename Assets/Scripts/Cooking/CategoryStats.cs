using System.Collections.Generic;
using UnityEngine;

// [v2] ค่าเฉลี่ยของวัตถุดิบแต่ละหมวด (ราคา และค่าฟื้นตอนทำสุก) ใช้คิดราคา/ค่าพลังของจานแยกแบบ
// - คิดจาก ItemData ชนิด Ingredient ทุกตัวใน ItemDatabase (สร้างครั้งแรกที่เรียกใช้ หรือเรียก Build เอง)
// - ค่าฟื้นตอนสุก: วัตถุดิบที่ทุกค่าเป็น 0 (กินดิบไม่ได้) = H 5 / S 20 (ค่าใน CookingSettings) / ค่าติดลบ = 0
public static class CategoryStats
{
    private struct Stat
    {
        public int count;
        public float priceSum;
        public Vector3 restoreSum;
    }

    private static readonly Dictionary<ItemCategory, Stat> stats = new Dictionary<ItemCategory, Stat>();
    private static ItemDatabase builtFrom;

    // ถูกสร้างจากฐานข้อมูลไหนแล้วหรือยัง
    public static bool IsBuilt => builtFrom != null;

    // คำนวณค่าเฉลี่ยใหม่ทั้งหมดจากฐานข้อมูลนี้
    public static void Build(ItemDatabase database)
    {
        stats.Clear();
        builtFrom = database;
        if (database == null)
        {
            Debug.LogWarning("CategoryStats: No ItemDatabase. Category averages are 0.");
            return;
        }

        foreach (ItemData item in database.Items)
        {
            if (item == null || item.itemType != ItemType.Ingredient)
            {
                continue;
            }

            stats.TryGetValue(item.category, out Stat stat);
            stat.count++;
            stat.priceSum += item.sellPrice;
            stat.restoreSum += CookedRestore(item);
            stats[item.category] = stat;
        }
    }

    // ล้างค่า ให้คำนวณใหม่ครั้งถัดไป (เช่น หลัง import)
    public static void Invalidate()
    {
        stats.Clear();
        builtFrom = null;
    }

    // ราคาเฉลี่ยของหมวด (ไม่มีวัตถุดิบในหมวด = 0)
    public static float AveragePrice(ItemCategory category)
    {
        Ensure();
        return stats.TryGetValue(category, out Stat stat) && stat.count > 0 ? stat.priceSum / stat.count : 0f;
    }

    // ค่าฟื้นเฉลี่ยตอนทำสุก (x = Health, y = Stamina, z = Mana)
    public static Vector3 AverageRestore(ItemCategory category)
    {
        Ensure();
        return stats.TryGetValue(category, out Stat stat) && stat.count > 0 ? stat.restoreSum / stat.count : Vector3.zero;
    }

    // จำนวนวัตถุดิบในหมวด (ใช้ใน Debug)
    public static int CountIn(ItemCategory category)
    {
        Ensure();
        return stats.TryGetValue(category, out Stat stat) ? stat.count : 0;
    }

    // ค่าฟื้นของวัตถุดิบเมื่อถูกทำสุกเป็นส่วนหนึ่งของจาน (x = Health, y = Stamina, z = Mana)
    public static Vector3 CookedRestore(ItemData item)
    {
        if (item == null)
        {
            return Vector3.zero;
        }

        int h = Mathf.Max(0, item.restoreHealth);
        int s = Mathf.Max(0, item.restoreStamina);
        int m = Mathf.Max(0, item.restoreMana);

        // กินดิบไม่ได้ (ทุกค่า 0) → ทำสุกแล้วได้ค่าตั้งต้น
        if (item.restoreHealth <= 0 && item.restoreStamina <= 0 && item.restoreMana <= 0 && item.restoreFever <= 0)
        {
            Vector2Int zero = CookingSettings.Active != null ? CookingSettings.Active.cookedZeroValues : CookingSettings.DefaultCookedZeroValues;
            return new Vector3(zero.x, zero.y, 0f);
        }

        return new Vector3(h, s, m);
    }

    private static bool warnedMissing;

    // สร้างตารางถ้ายังไม่มี หรือฐานข้อมูลเปลี่ยนตัว (ยังไม่มีฐานข้อมูล = ค่าเฉลี่ย 0 เตือนครั้งเดียว)
    private static void Ensure()
    {
        ItemDatabase current = ItemDatabase.Active;
        if (current == null)
        {
            if (builtFrom == null && !warnedMissing)
            {
                warnedMissing = true;
                Debug.LogWarning("CategoryStats: ItemDatabase.Active is not loaded yet. Category averages are 0.");
            }
            return;
        }

        if (builtFrom != current)
        {
            Build(current);
        }
    }
}
