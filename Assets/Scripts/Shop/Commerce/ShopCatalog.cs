using System;
using System.Collections.Generic;
using UnityEngine;

// [2.6b] ชนิดของสินค้าในร้าน (เพิ่มค่าใหม่ต่อท้ายเท่านั้น)
public enum ShopEntryKind
{
    Item,        // ไอเทมเข้ากระเป๋า (ราคา = sellPrice × buyMultiplier)
    BagUpgrade,  // อัปเกรดกระเป๋าเป็นระดับ bagLevel (ต้องตั้ง priceOverride)
    RecipeBook   // สมุดสูตร: ซื้อแล้วรู้สูตรทันที (ราคา = ราคาจาน × recipeBookMultiplier)
}

// [2.6b] สินค้าหนึ่งแถวในร้าน
[Serializable]
public class ShopEntry
{
    public ShopEntryKind kind = ShopEntryKind.Item;

    [Tooltip("ไอเทมที่ขาย (ใช้กับ Item)")]
    public ItemData item;

    [Tooltip("สูตรที่ขาย (ใช้กับ RecipeBook)")]
    public RecipeData recipe;

    [Tooltip("ระดับกระเป๋าหลังอัปเกรด (ใช้กับ BagUpgrade)")]
    [Min(2)] public int bagLevel = 2;

    [Tooltip("ราคาตายตัว (0 = คิดเองจากราคาขาย)")]
    [Min(0)] public int priceOverride;

    // ชื่อที่แสดงในร้าน
    public string DisplayName
    {
        get
        {
            switch (kind)
            {
                case ShopEntryKind.Item: return item != null ? item.itemName : "(missing item)";
                case ShopEntryKind.RecipeBook: return recipe != null ? $"Recipe: {recipe.recipeName}" : "(missing recipe)";
                case ShopEntryKind.BagUpgrade: return $"Bag Upgrade (Level {bagLevel})";
                default: return "?";
            }
        }
    }
}

// [2.6b] รายการของร้านหนึ่งร้าน + เวลาเปิด (สร้างที่ Assets > Create > Cozy > Shop Catalog หรือ Build Shops)
// เพิ่มร้าน/พ่อค้าเร่ในเฟส 3 = สร้างไฟล์นี้ใหม่ + วาง ShopKeeperCounter ไม่ต้องเขียนโค้ด
[CreateAssetMenu(fileName = "NewShopCatalog", menuName = "Cozy/Shop Catalog")]
public class ShopCatalog : ScriptableObject
{
    [Header("Shop")]
    public string shopName = "Shop";

    [Tooltip("ชั่วโมงที่เปิด (0–23)")]
    [Range(0, 23)] public int openHour = 9;

    [Tooltip("ชั่วโมงที่ปิด (เปิดถึงก่อนเวลานี้)")]
    [Range(1, 24)] public int closeHour = 17;

    [Tooltip("รับซื้อของจากผู้เล่นไหม (มีแท็บ Sell ได้เงินทันที)")]
    public bool buysFromPlayer;

    [Header("Prices")]
    [Tooltip("ราคาซื้อไอเทม = sellPrice × ค่านี้")]
    [Min(0f)] public float buyMultiplier = 2f;

    [Tooltip("ราคาสมุดสูตร = ราคาจาน × ค่านี้")]
    [Min(0f)] public float recipeBookMultiplier = 10f;

    [Header("Entries")]
    public List<ShopEntry> entries = new List<ShopEntry>();

    // ราคาซื้อต่อชิ้นของแถวนี้
    public int GetPrice(ShopEntry entry)
    {
        if (entry == null) return 0;
        if (entry.priceOverride > 0) return entry.priceOverride;

        switch (entry.kind)
        {
            case ShopEntryKind.Item:
                return entry.item != null ? Mathf.RoundToInt(entry.item.sellPrice * buyMultiplier) : 0;
            case ShopEntryKind.RecipeBook:
                return entry.recipe != null ? Mathf.RoundToInt(entry.recipe.sellPrice * recipeBookMultiplier) : 0;
            default:
                return 0;
        }
    }

    // ตอนนี้ร้านเปิดอยู่ไหม (ไม่มีนาฬิกา = เปิดตลอด)
    public bool IsOpenAt(int hour)
    {
        return hour >= openHour && hour < closeHour;
    }

    private void OnValidate()
    {
        closeHour = Mathf.Max(closeHour, openHour + 1);
    }
}
