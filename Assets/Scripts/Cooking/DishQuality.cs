using UnityEngine;

// คุณภาพของจานอาหาร (ได้จากมินิเกมทำอาหารขั้น 2.5) ลำดับจากต่ำไปสูง
// หมายเหตุ: เพิ่มค่าใหม่ต่อท้ายเท่านั้น ห้ามสลับลำดับ (ใช้เป็นตัวเลขในไฟล์เซฟ)
public enum DishQuality
{
    Ok,      // ★ ทำพลาด แต่ยังกินได้
    Good,    // ★★ ปกติ (จานจากเซฟเก่า และจานที่ไม่รู้คุณภาพ นับเป็นระดับนี้)
    Perfect  // ★★★ ทำได้ดีเยี่ยม
}

// ตัวช่วยเรื่องคุณภาพจาน: ชื่อ สี ข้อความ
// ตอนนี้ใช้คำ (Perfect / Good / OK) แทนดาว เพราะฟอนต์ TMP ตั้งต้นอาจไม่มีตัว ★ เปลี่ยนที่ Label ได้ภายหลัง
public static class DishQualityUtil
{
    public const int Count = 3;

    // ลำดับจากดีที่สุดไปต่ำสุด (ใช้เรียงตัวเลือกตอนเสิร์ฟ)
    public static readonly DishQuality[] BestFirst = { DishQuality.Perfect, DishQuality.Good, DishQuality.Ok };

    public static string Label(DishQuality quality)
    {
        switch (quality)
        {
            case DishQuality.Perfect: return "Perfect";
            case DishQuality.Good: return "Good";
            default: return "OK";
        }
    }

    public static Color GetColor(DishQuality quality)
    {
        switch (quality)
        {
            case DishQuality.Perfect: return new Color(1f, 0.82f, 0.3f);
            case DishQuality.Good: return new Color(0.5f, 0.88f, 0.45f);
            default: return new Color(0.85f, 0.85f, 0.85f);
        }
    }

    // ชื่อคุณภาพพร้อมสี (rich text ของ TMP)
    public static string ColoredLabel(DishQuality quality)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(GetColor(quality))}>{Label(quality)}</color>";
    }
}
