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
    // 6 = จานอาหารในกระเป๋าแยกจำนวนตามคุณภาพ (qualityOk / qualityGood / qualityPerfect ใน InventoryEntrySave)
    // 7 = เลเวลร้าน + ชื่อเสียง และจดหมาย (ได้แล้ว / วันที่ได้ / อ่านแล้ว / ของแนบที่รับแล้ว / รอส่ง)
    public const int CurrentVersion = 7;

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

    // [v7] เลเวลร้านและชื่อเสียง (ShopProgress)
    public int shopLevel = 1;
    public int reputation = 0;

    // [v7] จดหมายที่ได้แล้ว ตามลำดับที่ได้ (ทุกรายการลำดับเดียวกัน)
    // รหัส: "chef:<recipeId>" = จดหมายสูตรจากเชฟ / "letter:<letterId>" = จดหมายทั่วไป (LetterData)
    // mailClaimed = บิตของแนบที่รับแล้ว (บิตที่ i = ชิ้นที่ i)
    public List<string> mailIds = new List<string>();
    public List<int> mailDays = new List<int>();
    public List<bool> mailRead = new List<bool>();
    public List<int> mailClaimed = new List<int>();

    // [v7] จดหมายทั่วไปที่รอส่งเช้าถัดไป (เช่น จดหมายตอนสลบ) / จดหมายเชฟที่ยังไม่มาไม่ต้องเซฟ (คำนวณใหม่ทุกเช้า)
    public List<string> mailPendingIds = new List<string>();
}

// ของหนึ่งชนิดในกระเป๋า
[Serializable]
public class InventoryEntrySave
{
    public string itemId;
    public int amount;

    // [v6] จำนวนแยกตามคุณภาพ (เฉพาะจานอาหาร ผลรวม = amount) เซฟเก่าไม่มี = 0 ทั้งหมด → นับเป็น Good
    public int qualityOk;
    public int qualityGood;
    public int qualityPerfect;
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
