using UnityEngine;

// สร้างไฟล์ข้อมูลวัตถุดิบได้จากเมนู Assets > Create > Cozy > Item Data
[CreateAssetMenu(fileName = "NewItem", menuName = "Cozy/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Info")]
    public string itemName = "New Item"; // ชื่อที่แสดงในเกม
    public Sprite icon;                  // ไอคอนในกระเป๋า (ใส่ทีหลังได้)
    [TextArea] public string description; // คำอธิบายสั้นๆ
}