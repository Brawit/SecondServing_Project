using System;
using System.Collections.Generic;

// โครงข้อมูลที่เขียนลงไฟล์ save.json (ใช้กับ JsonUtility จึงต้องเป็น field แบบ public และติด [Serializable])
[Serializable]
public class SaveData
{
    // เวอร์ชันของรูปแบบไฟล์เซฟ เพิ่มเลขเมื่อเปลี่ยนโครงสร้าง จะได้แปลงเซฟเก่าได้ในอนาคต
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;

    // วันที่จะเริ่มเล่นเมื่อโหลด (เซฟตอนจบวัน จึงเก็บเป็น "วันถัดไป")
    public int day = 1;

    // ของในกระเป๋าผู้เล่น
    public List<InventoryEntrySave> inventory = new List<InventoryEntrySave>();

    // ของป่าที่วางอยู่บนพื้นในแต่ละโซน
    public List<GroundItemSave> groundItems = new List<GroundItemSave>();
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
