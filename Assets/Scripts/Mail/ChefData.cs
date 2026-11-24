using UnityEngine;

// เชฟผู้มีประสบการณ์ที่ส่งจดหมายพร้อมสูตรมาให้ (ชาติละ 1 คน)
// ตัว import สร้าง/อัปเดตจาก Assets/Data/Chefs.csv (ชีต Chefs) ไว้ที่ Assets/Chefs/<chefId>.asset ไม่เขียนทับ portrait
// เฟส 3: ใช้ข้อมูลนี้ต่อได้ เช่น ให้เชฟมาเยี่ยมร้าน
[CreateAssetMenu(fileName = "NewChef", menuName = "Cozy/Chef Data")]
public class ChefData : ScriptableObject
{
    [Header("Save")]
    [Tooltip("รหัสเชฟ (คอลัมน์ chef_id) เช่น chef_th ต้องไม่ซ้ำ")]
    public string chefId;

    [Header("Info")]
    [Tooltip("ชื่อที่แสดงในจดหมาย")]
    public string displayName = "Chef";

    [Tooltip("ชาติของเชฟ (แสดงในหัวจดหมาย)")]
    public Cuisine cuisine = Cuisine.Thailand;

    [Tooltip("คำลงท้ายจดหมาย ขึ้นบรรทัดใหม่ได้")]
    [TextArea(2, 4)] public string signOff;

    [Tooltip("รูปเชฟ (ยังไม่ใช้ เผื่อเฟส 4) ตัว import ไม่เขียนทับช่องนี้")]
    public Sprite portrait;

    [Tooltip("โน้ตตัวละครสำหรับคนเขียนเรื่อง ไม่แสดงในเกม")]
    [TextArea(2, 6)] public string bio;
}
