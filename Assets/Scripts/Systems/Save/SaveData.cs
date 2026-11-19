using System;
using System.Collections.Generic;

// โครงข้อมูลที่เขียนลงไฟล์ save.json (ใช้กับ JsonUtility จึงต้องเป็น field แบบ public และติด [Serializable])
[Serializable]
public class SaveData
{
    // เวอร์ชันของรูปแบบไฟล์เซฟ เพิ่มเลขเมื่อเปลี่ยนโครงสร้าง จะได้แปลงเซฟเก่าได้
    // 1 = วัน กระเป๋า ของบนพื้น
    // 2 = เพิ่มไอเทมที่เคยเจอ สูตรที่รู้ และระดับกระเป๋า
    public const int CurrentVersion = 2;

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
