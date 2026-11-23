using System.Collections.Generic;
using UnityEngine;

// ชุดท่าทำอาหาร (ใช้ร่วมกันหลายสูตร) เช่น "ผัดไฟแรง" "ตำ" "อบ" "นึ่ง"
// สร้างได้จาก Assets > Create > Cozy > Cooking Profile
// แล้วลากใส่ช่อง Cooking Profile ของ RecipeData ที่ทำแบบเดียวกัน
[CreateAssetMenu(fileName = "NewCookingProfile", menuName = "Cozy/Cooking Profile")]
public class CookingProfile : ScriptableObject
{
    [Tooltip("ชื่อชุดท่า (ไว้ดูใน Editor)")]
    public string displayName = "New Profile";

    [Tooltip("ขั้นตอนตามลำดับ แนะนำ 2–3 ขั้น (ขั้นละ ~2–3 วินาที)")]
    public List<CookingStep> steps = new List<CookingStep>();

    public bool HasSteps => steps != null && steps.Count > 0;
}
