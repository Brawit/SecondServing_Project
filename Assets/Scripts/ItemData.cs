using UnityEngine;

// สร้างไฟล์ข้อมูลวัตถุดิบได้จากเมนู Assets > Create > Cozy > Item Data
[CreateAssetMenu(fileName = "NewItem", menuName = "Cozy/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Save")]
    [Tooltip("รหัสประจำไอเทม ใช้ในไฟล์เซฟ ต้องไม่ซ้ำกับไอเทมอื่น และไม่ควรเปลี่ยนหลังปล่อยเกม (เว้นว่างไว้ จะถูกตั้งจากชื่อไฟล์ให้อัตโนมัติ)")]
    public string itemId;

    [Header("Info")]
    public string itemName = "New Item"; // ชื่อที่แสดงในเกม
    public Sprite icon;                  // ไอคอนในกระเป๋า (ใส่ทีหลังได้)
    [TextArea] public string description; // คำอธิบายสั้นๆ

#if UNITY_EDITOR
    // ถ้ายังไม่มีรหัส ให้ตั้งจากชื่อไฟล์เป็นตัวพิมพ์เล็ก เช่น "Mushroom" -> "mushroom"
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(itemId) && !string.IsNullOrEmpty(name))
        {
            itemId = name.Trim().ToLowerInvariant().Replace(' ', '_');
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
