using System;
using System.Collections.Generic;
using UnityEngine;

// [2.6] ผลลัพธ์เมื่อก่อสร้างเสร็จ
// หมายเหตุ: เพิ่มค่าใหม่ต่อท้ายเท่านั้น (เฟส 3: HouseUpgrade, FishPond, ShrimpPond, Greenhouse ...)
public enum ConstructionEffect
{
    ShopLevelUp // ร้านเลเวลขึ้น 1
}

// [2.6] ของที่ช่างขอ 1 รายการ
[Serializable]
public class MaterialCost
{
    public ItemData item;
    [Min(1)] public int amount = 1;
}

// [2.6] งานก่อสร้าง 1 งานที่จ้างช่างได้ (ไฟล์ละงาน ที่ Assets/Construction/)
// เฟส 3 เพิ่มงานใหม่ = สร้างไฟล์ใหม่ + (ถ้าผลลัพธ์ใหม่) เพิ่ม ConstructionEffect และโค้ดใน ConstructionManager.ApplyEffect
[CreateAssetMenu(fileName = "NewConstruction", menuName = "Cozy/Construction Project")]
public class ConstructionProject : ScriptableObject
{
    [Header("Save")]
    [Tooltip("รหัสงาน ใช้ในไฟล์เซฟ ต้องไม่ซ้ำ")]
    public string projectId;

    [Header("Info")]
    public string displayName = "New Project";
    [TextArea(2, 4)] public string description;

    [Header("Cost")]
    [Min(0)] public int money;

    [Tooltip("ของที่ช่างขอ (ตอนนี้ยังว่าง เฟส 3 ใส่ไม้/หิน ฯลฯ)")]
    public List<MaterialCost> materials = new List<MaterialCost>();

    [Tooltip("จำนวนวันที่ใช้สร้าง (จ้างวันนี้ 1 วัน = เสร็จเช้าพรุ่งนี้)")]
    [Min(1)] public int days = 1;

    [Header("Requirements")]
    [Tooltip("เลเวลร้านตอนนี้ต้องเท่ากับค่านี้ (0 = ไม่สนเลเวลร้าน)")]
    [Min(0)] public int requiredShopLevel;

    [Tooltip("หลอดชื่อเสียงต้องเต็ม (เริ่มจ้าง = หลอดกลับเป็น 0)")]
    public bool requiresFullReputation;

    [Header("Result")]
    public ConstructionEffect effect = ConstructionEffect.ShopLevelUp;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(projectId) && !string.IsNullOrEmpty(name))
        {
            projectId = name.Trim().ToLowerInvariant().Replace(' ', '_');
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
