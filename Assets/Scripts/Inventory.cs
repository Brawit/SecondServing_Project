using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int maxSlots = 20; // จำนวนชนิดของที่เก็บได้สูงสุด

    // เก็บข้อมูลเป็นคู่ "ชนิดของ -> จำนวน"
    private readonly Dictionary<ItemData, int> items = new Dictionary<ItemData, int>();

    // ส่งสัญญาณเมื่อกระเป๋าเปลี่ยน (ไว้ให้ UI มาฟังทีหลัง)
    public event Action OnChanged;

    // เพิ่มของเข้ากระเป๋า คืนค่า true ถ้าเพิ่มสำเร็จ
    public bool AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        // ถ้าเป็นของชนิดใหม่และช่องเต็มแล้ว ให้ปฏิเสธ
        if (!items.ContainsKey(item) && items.Count >= maxSlots)
        {
            Debug.Log("Inventory is full!");
            return false;
        }

        items.TryGetValue(item, out int current);
        items[item] = current + amount;

        OnChanged?.Invoke();
        return true;
    }

    // เอาของออกจากกระเป๋า คืนค่า true ถ้ามีพอและเอาออกสำเร็จ
    public bool RemoveItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;
        if (!items.TryGetValue(item, out int current) || current < amount) return false;

        if (current == amount) items.Remove(item);
        else items[item] = current - amount;

        OnChanged?.Invoke();
        return true;
    }

    // ดูว่ามีของชนิดนี้กี่ชิ้น
    public int GetCount(ItemData item)
    {
        return items.TryGetValue(item, out int count) ? count : 0;
    }

    // พิมพ์รายการในกระเป๋าลง Console (ใช้ทดสอบก่อนมี UI)
    public void PrintInventory()
    {
        if (items.Count == 0)
        {
            Debug.Log("Inventory: (empty)");
            return;
        }

        foreach (var pair in items)
        {
            Debug.Log($"Inventory: {pair.Key.itemName} x{pair.Value}");
        }
    }
}