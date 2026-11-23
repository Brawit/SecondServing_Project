using UnityEngine.InputSystem;

// มินิเกมทำอาหาร 1 แบบ (CookingSession เรียกทีละขั้น)
// มินิเกมรู้แค่การเล่น (เข็ม โซน ปุ่ม) ส่วนภาพและชื่อท่ามาจาก CookingStep ที่ส่งมา
// เพิ่มมินิเกมใหม่: สร้างคลาสใหม่ที่ใช้ interface นี้ แล้วเพิ่มใน CookingSession.CreateMinigame
public interface ICookingMinigame
{
    // เริ่มขั้นนี้ (ตั้งค่าแถบ/ข้อความบน view) difficulty = ความยากของสูตร 1–3 (ใช้กับตัวคูณใน CookingSettings)
    void Begin(CookingStep step, CookingSettings settings, CookingMinigameView view, int difficulty);

    // อัปเดตทุกเฟรม คืนค่า true เมื่อจบขั้นนี้แล้ว (อ่าน Result ได้)
    bool Tick(float deltaTime, Keyboard keyboard);

    // ผลของขั้นนี้ (อ่านหลัง Tick คืนค่า true)
    DishQuality Result { get; }
}
