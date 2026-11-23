using System;
using UnityEngine;

// ชนิดมินิเกมทำอาหาร
// หมายเหตุ: เพิ่มค่าใหม่ต่อท้ายเท่านั้น ห้ามสลับลำดับ (Unity เก็บเป็นตัวเลขใน asset)
public enum CookingMinigameType
{
    Timing,   // เข็มวิ่ง กด Space ให้หยุดในโซน (ผัด ทอด อบ พลิก)
    Mash,     // รัว Space ให้ครบ (หั่น ตำ นวด คั้น)
    Sequence, // กดลูกศร/WASD ตามลำดับ (ปรุง จัดจาน แต่งหน้า)
    Hold,     // [2.5b] กด Space ค้างแล้วปล่อยให้พอดีเส้น (เท ตวง)
    Stir,     // [2.5b] กดค้าง A / D คุมกระทะให้อยู่ใต้อาหารที่วิ่งไปมา (ผัด คน)
    Shake,    // [2.5b] กดสลับขึ้น-ลง (เขย่า)
    Whisk     // [2.5b] กดสลับซ้าย-ขวา (ตี ผสม)
}

// ขั้นตอนทำอาหาร 1 ขั้น (ใช้ใน CookingProfile และ RecipeData.customSteps)
// ค่าปรับที่เป็น 0 = ใช้ค่าปกติจาก CookingSettings / ความยากของสูตร (★1–3) คูณทับอีกชั้น
[Serializable]
public class CookingStep
{
    [Tooltip("ชนิดมินิเกมของขั้นนี้")]
    public CookingMinigameType type = CookingMinigameType.Timing;

    [Tooltip("ชื่อท่าที่แสดงบนแถบ เช่น Pound! / Flip! / Wok toss! (เว้นว่าง = ใช้ชื่อตั้งต้นตามชนิด)")]
    public string label;

    [Tooltip("ภาพของขั้นนี้ เช่น ครก กระทะ เตาอบ (เว้นว่าง = หาจาก Profile ของสูตร แล้วค่อยใช้ภาพตั้งต้นตามชนิด)")]
    public Sprite visual;

    [Header("Timing / Hold (0 = default)")]
    [Tooltip("ความกว้างโซน Good (0–1 ของแถบ) ใช้กับ Timing, ช่วงรอบเส้นเป้าของ Hold และความกว้างกระทะของ Stir")]
    [Range(0f, 1f)] public float zoneWidth;

    [Tooltip("ความกว้างโซน Perfect (0–1 ของแถบ อยู่กลางโซน Good) ใช้กับ Timing และ Hold")]
    [Range(0f, 1f)] public float perfectWidth;

    [Tooltip("Timing = ความเร็วเข็ม (รอบไป-กลับต่อวินาที) / Hold = ตัวคูณความเร็วแถบ / Stir = ตัวคูณความเร็วอาหาร (2 = เร็วเป็นสองเท่า)")]
    [Min(0f)] public float needleSpeed;

    [Tooltip("ต้องกดกี่ครั้ง (เช่น ผัด 3 จังหวะ)")]
    [Min(0)] public int hits;

    [Tooltip("โซนเลื่อนไปมา (ต้องจับจังหวะ เช่น พลิกแพนเค้ก)")]
    public bool movingZone;

    [Header("Mash / Whisk / Shake (0 = default)")]
    [Tooltip("Mash = กด Space กี่ครั้งแถบเต็ม / Whisk = กดสลับซ้าย-ขวากี่ครั้ง / Shake = กดสลับขึ้น-ลงกี่ครั้ง (หยุดกดแถบจะค่อยๆ ลด)")]
    [Min(0)] public int presses;

    [Header("Sequence (0 = default)")]
    [Tooltip("ความยาวของลำดับปุ่ม")]
    [Min(0)] public int length;

    [Tooltip("เวลาต่อปุ่ม (วินาที) รวมเป็นเวลานับถอยหลังทั้งลำดับ")]
    [Min(0f)] public float timePerKey;

    [Header("Shared (0 = default)")]
    [Tooltip("เวลาสูงสุดของขั้นนี้ (วินาที) Timing = รอกดได้นานเท่านี้ต่อครั้ง / Mash/Stir/Whisk/Shake = ต้องทำแถบให้เต็มภายในเวลานี้ / Sequence = เวลารวมทั้งลำดับ / Hold = ต้องเริ่มกดภายในเวลานี้")]
    [Min(0f)] public float timeLimit;

    // ชนิดที่เล่นจริง ([2.5b] ครบทุกชนิดแล้ว เก็บไว้เผื่อเพิ่มชนิดใหม่ที่ยังไม่มีมินิเกม)
    public CookingMinigameType PlayableType => type;

    // สร้างขั้นตอนแบบสั้นๆ (ใช้ในตัวช่วยสร้าง asset)
    public static CookingStep Create(CookingMinigameType type, string label)
    {
        return new CookingStep { type = type, label = label };
    }

    public CookingStep Clone()
    {
        return (CookingStep)MemberwiseClone();
    }
}
