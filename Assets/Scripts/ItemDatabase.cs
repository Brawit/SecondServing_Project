using System.Collections.Generic;
using UnityEngine;

// ไฟล์ข้อมูลรวม ItemData ทุกตัวในเกม ใช้แปลงรหัส (itemId) กลับเป็นไอเทมตอนโหลดเซฟ
// สร้างได้จากเมนู Assets > Create > Cozy > Item Database (ควรมีไฟล์เดียวในโปรเจกต์)
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Cozy/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [Header("Items")]
    [Tooltip("ItemData ทุกตัวในเกม (คลิกขวาที่หัวคอมโพเนนต์ > Find All Items เพื่อดึงเข้ามาอัตโนมัติ)")]
    [SerializeField] private List<ItemData> items = new List<ItemData>();

    // ตารางค้นหา รหัส -> ไอเทม สร้างตอนเรียกใช้ครั้งแรก
    private Dictionary<string, ItemData> lookup;

    public IReadOnlyList<ItemData> Items => items;

    // หาไอเทมจากรหัส คืนค่า null ถ้าไม่เจอ
    // [v2] ไม่เจอในรายการ + รหัสเป็นจานแบบ (มี "__") → ให้ DishVariantRegistry สร้าง/คืนไอเทมแบบ
    public ItemData GetById(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        if (lookup == null)
        {
            BuildLookup();
        }

        if (lookup.TryGetValue(itemId, out ItemData item))
        {
            return item;
        }

        if (itemId.Contains(DishVariantRegistry.Separator) && DishVariantRegistry.TryResolve(itemId, out ItemData variant))
        {
            return variant;
        }

        return null;
    }

    // สร้างตารางค้นหาใหม่ และเตือนถ้ามีรหัสว่างหรือซ้ำ
    private void BuildLookup()
    {
        lookup = new Dictionary<string, ItemData>();

        foreach (ItemData item in items)
        {
            if (item == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.itemId))
            {
                Debug.LogWarning($"ItemDatabase: Item '{item.name}' has no itemId.", item);
                continue;
            }

            if (lookup.ContainsKey(item.itemId))
            {
                Debug.LogWarning($"ItemDatabase: Duplicate itemId '{item.itemId}' ({lookup[item.itemId].name} and {item.name}).", item);
                continue;
            }

            lookup.Add(item.itemId, item);
        }
    }

    // เมื่อแก้ค่าใน Inspector ให้สร้างตารางใหม่ และตรวจรหัสซ้ำทันที
    private void OnValidate()
    {
        BuildLookup();
    }

    // ตอนเริ่มเกม/โหลด asset ให้ล้างตารางเก่า เผื่อ ItemData ถูกแก้รหัสไปแล้ว
    private void OnEnable()
    {
        lookup = null;
        Active = this;
    }

    // [v2] ฐานข้อมูลที่โหลดอยู่ (ไฟล์เดียวในโปรเจกต์) ให้ระบบที่ไม่มีช่องอ้างอิงใช้ เช่น ค่าเฉลี่ยหมวด / จานแยกแบบ
    public static ItemDatabase Active { get; private set; }

#if UNITY_EDITOR
    // ดึง ItemData ทุกตัวในโปรเจกต์เข้ามาในรายการ (ใช้ได้เฉพาะใน Editor)
    [ContextMenu("Find All Items")]
    private void FindAllItems()
    {
        RefreshFromProject();
    }

    // ให้ตัว import เรียกได้ หลังสร้างไอเทมใหม่ คืนค่าจำนวนไอเทมที่เจอ
    public int RefreshFromProject()
    {
        items.Clear();

        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null)
            {
                items.Add(item);
            }
        }

        BuildLookup();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"ItemDatabase: Found {items.Count} items.");
        return items.Count;
    }
#endif
}
