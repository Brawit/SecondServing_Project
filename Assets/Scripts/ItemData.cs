using UnityEngine;

// หมวดของวัตถุดิบ (ใช้จัดกลุ่มในสมุดสูตร/ร้านค้า)
// หมายเหตุ: เพิ่มค่าใหม่ต่อท้ายเท่านั้น ห้ามสลับลำดับ เพราะ Unity เก็บเป็นตัวเลข
public enum ItemCategory
{
    Other,
    Herb,
    Vegetable,
    Fruit,
    Flower,
    Nut,
    Seafood,
    Meat,
    AnimalProduct,
    Grain,
    Dairy,
    Sweetener,
    Sweet,
    DrinkBase,
    Paste,
    Fermented,
    SeaProduct
}

// แหล่งที่มาของวัตถุดิบ
// หมายเหตุ: เพิ่มค่าใหม่ต่อท้ายเท่านั้น ห้ามสลับลำดับ
public enum ItemSource
{
    Forage,  // เก็บในแผนที่สำรวจ
    Sea,     // ชายฝั่ง/ทะเล
    Market,  // ซื้อที่ตลาด
    Artisan, // ของเฉพาะทาง ของหมัก ของประจำชาติ
    Pantry   // ตู้กับข้าว มีไม่จำกัด
}

// สร้างไฟล์ข้อมูลวัตถุดิบได้จากเมนู Assets > Create > Cozy > Item Data
// หรือสร้าง/อัปเดตทั้งหมดจาก CSV ผ่านเมนู Tools > Second Serving > Import Recipes & Ingredients
[CreateAssetMenu(fileName = "NewItem", menuName = "Cozy/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Save")]
    [Tooltip("รหัสประจำไอเทม ใช้ในไฟล์เซฟ ต้องไม่ซ้ำกับไอเทมอื่น และไม่ควรเปลี่ยนหลังปล่อยเกม (เว้นว่างไว้ จะถูกตั้งจากชื่อไฟล์ให้อัตโนมัติ)")]
    public string itemId;

    [Header("Info")]
    public string itemName = "New Item"; // ชื่อที่แสดงในเกม (ตอนนี้ใช้ชื่ออังกฤษ จนกว่าจะมีฟอนต์ไทย)

    [Tooltip("ชื่อภาษาไทย (เก็บไว้ใช้ตอนมีฟอนต์ไทยใน TMP)")]
    public string nameTh;

    public Sprite icon;                  // ไอคอนในกระเป๋า (ใส่ทีหลังได้)
    [TextArea] public string description; // คำอธิบายสั้นๆ

    [Header("Classification")]
    [Tooltip("หมวดของวัตถุดิบ")]
    public ItemCategory category = ItemCategory.Other;

    [Tooltip("ได้วัตถุดิบนี้มาจากไหน")]
    public ItemSource source = ItemSource.Forage;

    [Header("Economy")]
    [Tooltip("ราคาที่ร้านค้ารับซื้อจากผู้เล่น (0 = ยังไม่ได้ตั้ง)")]
    [Min(0)] public int sellPrice = 0;

#if UNITY_EDITOR
    // ถ้ายังไม่มีรหัส ให้ตั้งจากชื่อไฟล์เป็นตัวพิมพ์เล็ก เช่น "Mushroom" -> "mushroom"
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(itemId) && !string.IsNullOrEmpty(name))
        {
            itemId = name.Trim().ToLowerInvariant().Replace(' ', '_');
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
