using System;
using System.Collections.Generic;

// โครงข้อมูลที่เขียนลงไฟล์ save.json (ใช้กับ JsonUtility จึงต้องเป็น field แบบ public และติด [Serializable])
[Serializable]
public class SaveData
{
    // เวอร์ชันของรูปแบบไฟล์เซฟ เพิ่มเลขเมื่อเปลี่ยนโครงสร้าง จะได้แปลงเซฟเก่าได้
    // 1 = วัน กระเป๋า ของบนพื้น
    // 2 = เพิ่มไอเทมที่เคยเจอ สูตรที่รู้ และระดับกระเป๋า
    // 3 = เพิ่มช่อง Hotbar, FEVER และค่าสูงสุดที่เพิ่มถาวร
    // 4 = เพิ่มของที่สวมอยู่ (8 ช่อง)
    // 5 = เพิ่มเมนูประจำวันล่าสุด (ใช้เป็นค่าเริ่มต้นของวันถัดไป)
    public const int CurrentVersion = 5;

    public int version = CurrentVersion;

    // วันที่จะเริ่มเล่นเมื่อโหลด (เซฟตอนจบวัน จึงเก็บเป็น "วันถัดไป")
    public int day = 1;

    // ของในกระเป๋าผู้เล่น
    public List<InventoryEntrySave> inventory = new List<InventoryEntrySave>();

    // ของป่าที่วางอยู่บนพื้นในแต่ละโซน
    public List<GroundItemSave> groundItems = new List<GroundItemSave>();

    // [v2] รหัสไอเทมที่เคยเจอแล้ว (ใช้แสดงแบบ Collectibles)
    public List<string> discoveredItems = new List<string>();

    // [v2] รหัสสูตรที่รู้แล้ว
    public List<string> knownRecipes = new List<string>();

    // [v2] ระดับกระเป๋า (1 = ระดับแรก)
    public int bagLevel = 1;

    // [v3] รหัสไอเทมใน Hotbar ทีละช่อง (10 ช่อง, ช่องว่าง = "")
    public List<string> hotbarItems = new List<string>();

    // [v3] FEVER เก็บข้ามวันได้ (ค่าปัจจุบันของ Health/Stamina/Mana ไม่ต้องเซฟ เพราะโหลด = เริ่มวันใหม่ เต็มเสมอ)
    public float fever = 0f;

    // [v3] ค่าสูงสุดที่เพิ่มถาวร เช่น จากอาหารบอส
    public int bonusMaxHealth = 0;
    public int bonusMaxStamina = 0;
    public int bonusMaxMana = 0;

    // [v4] รหัสไอเทมที่สวมอยู่ เรียงตาม EquipSlot: Shoes, Ring1, Ring2, Necklace, Head, Top, Bottom, Back (ช่องว่าง = "")
    // ของที่สวมอยู่ไม่อยู่ใน inventory (สวม = ย้ายออกจากกระเป๋า)
    public List<string> equippedItems = new List<string>();

    // [v5] เมนูที่ตั้งไว้ล่าสุด ทีละช่อง: รหัสสูตร (ช่องว่าง = "") และจำนวนจานที่ตั้งไว้
    public List<string> menuRecipes = new List<string>();
    public List<int> menuServings = new List<int>();
}

// ของหนึ่งชนิดในกระเป๋า
[Serializable]
public class InventoryEntrySave
{
    public string itemId;
    public int amount;
}

// ของหนึ่งชิ้นบนพื้น: อยู่โซนไหน เป็นอะไร อยู่ช่อง Tile ไหน
[Serializable]
public class GroundItemSave
{
    public string zoneId;
    public string itemId;
    public int cellX;
    public int cellY;
}
