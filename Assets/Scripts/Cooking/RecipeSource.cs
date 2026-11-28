using System;

// ทางที่ผู้เล่นได้สูตรมา (คอลัมน์ Obtain ในตาราง Recipes)
// หมายเหตุ: เพิ่มค่าใหม่ต่อท้ายเท่านั้น ห้ามสลับลำดับ เพราะ Unity เก็บเป็นตัวเลข
public enum RecipeSource
{
    Starter,        // สูตรเริ่มต้น
    ChefMail,       // [2.5c] จดหมายจากเชฟ (เลเวลร้าน + ชื่อเสียงถึงเกณฑ์)
    ShopBook,       // [2.6b] ซื้อสมุดสูตรที่ร้านค้า/พ่อค้าเร่
    MonsterPages,   // [เฟส 3] เศษหน้าสูตรจากมอนสเตอร์ ครบ 3 หน้า
    Boss,           // [เฟส 3] ตีบอสครั้งแรก
    DigBox,         // [เฟส 3] กล่องข้าวเก่าจากการขุด
    FishingBottle,  // [เฟส 3] ขวดจดหมายจากการตกปลา
    FoodTruck,      // [เฟส 3] กินจานของฟู้ดทรักครบ N ครั้ง
    RequestBoard,   // [เฟส 3] กระดานคำขอของชาวบ้าน
    Story,          // [เฟส 3] สูตรลับ: side story
    Event,          // [เฟส 3] สูตรลับ: อีเวนต์ลับ
    Festival,       // [เฟส 3] สูตรลับ: ชนะแข่งทำอาหารในเทศกาล
    Mastery         // [v2] Fine-dining ★4: เสิร์ฟเมนูต้นทางครบจำนวน + ร้านเลเวล 4
}

public static class RecipeSourceUtil
{
    // แปลงข้อความในตาราง เช่น "Chef Mail", "monster pages" เป็น RecipeSource (ไม่สนช่องว่าง/ตัวพิมพ์/ขีด)
    public static bool TryParse(string sheetText, out RecipeSource source)
    {
        source = RecipeSource.Starter;
        if (string.IsNullOrWhiteSpace(sheetText))
        {
            return false;
        }

        string compact = sheetText.Replace(" ", "").Replace("_", "").Replace("-", "").Trim();
        return Enum.TryParse(compact, true, out source) && Enum.IsDefined(typeof(RecipeSource), source);
    }
}
