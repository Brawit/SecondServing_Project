using System;
using System.Collections.Generic;
using UnityEngine;

// Hotbar ด้านล่างจอ (ข้อมูล): 10 ช่อง แต่ละช่องเป็น "ทางลัด" ที่ชี้ไปยัง ItemData
// ของไม่ได้ถูกย้ายออกจากกระเป๋า จำนวนที่แสดงอ่านจากกระเป๋าเสมอ
// ไอเทมชนิดเดียวกันอยู่ได้ทีละช่อง (ใส่ซ้ำช่องอื่น = ย้ายมาช่องใหม่)
// [v2] ช่องที่เป็นจานจำคุณภาพได้ด้วย (เช่น "Sushi (Tilapia) · OK") → จานเดียวกันคนละคุณภาพอยู่คนละช่องได้
//   คุณภาพ null = ไม่ระบุ (กินต่ำสุดก่อน) / ของในช่องหมด = ช่องยังอยู่ (HotbarSlotUI แสดงไอคอนสีเทา)
// การรับปุ่มอยู่ที่ PlayerActionController
public class Hotbar : MonoBehaviour
{
    // จำนวนช่อง (ปุ่ม 1–9 และ 0)
    public const int SlotCount = 10;

    [Header("References")]
    [Tooltip("กระเป๋าของผู้เล่น (เว้นว่างไว้ จะหาบน GameObject เดียวกัน)")]
    [SerializeField] private Inventory inventory;

    [Header("Debug")]
    [Tooltip("รหัสไอเทม (itemId) ที่จะให้ เมื่อคลิกขวาหัวคอมโพเนนต์ > Debug: Give Item By Id (ระหว่าง Play)")]
    [SerializeField] private string debugItemId = "";

    [Tooltip("จำนวนที่จะให้")]
    [Min(1)] [SerializeField] private int debugAmount = 1;

    private readonly ItemData[] slots = new ItemData[SlotCount];

    // [v2] คุณภาพที่ช่องนี้จำไว้ (เฉพาะจาน / null = ไม่ระบุ)
    private readonly DishQuality?[] qualities = new DishQuality?[SlotCount];
    private int selectedIndex;

    // ---------- Events ----------

    // ส่งสัญญาณเมื่อของในช่องเปลี่ยน (ใส่/เอาออก/สลับ)
    public event Action OnSlotsChanged;

    // ส่งสัญญาณเมื่อเลือกช่องใหม่ (ส่งเลขช่อง 0–9)
    public event Action<int> OnSelectionChanged;

    // ---------- ข้อมูลให้ระบบอื่นอ่าน ----------

    public Inventory Inventory => inventory;
    public int SelectedIndex => selectedIndex;
    public ItemData SelectedItem => slots[selectedIndex];

    // [v2] คุณภาพของช่องที่เลือก (null = ไม่ระบุ)
    public DishQuality? SelectedQuality => qualities[selectedIndex];

    // จำนวนของในกระเป๋าของช่องที่เลือก
    public int SelectedCount => GetCount(selectedIndex);

    private void Awake()
    {
        if (inventory == null)
        {
            inventory = GetComponent<Inventory>();
        }

        if (inventory == null)
        {
            Debug.LogWarning("Hotbar: No Inventory found on this GameObject.", this);
        }
    }

    public ItemData GetSlot(int index)
    {
        return IsValidIndex(index) ? slots[index] : null;
    }

    // [v2] คุณภาพที่ช่องนี้จำไว้ (null = ไม่ระบุ / ไม่ใช่จาน)
    public DishQuality? GetQuality(int index)
    {
        return IsValidIndex(index) ? qualities[index] : null;
    }

    // จำนวนในกระเป๋าของไอเทมในช่องนี้ (ช่องว่าง = 0) / [v2] ช่องที่จำคุณภาพ = นับเฉพาะคุณภาพนั้น
    public int GetCount(int index)
    {
        ItemData item = GetSlot(index);
        if (item == null || inventory == null)
        {
            return 0;
        }

        DishQuality? quality = qualities[index];
        return quality.HasValue ? inventory.GetCount(item, quality.Value) : inventory.GetCount(item);
    }

    // ไอเทมนี้อยู่ช่องไหน (คุณภาพใดก็ได้) (-1 = ไม่อยู่ใน Hotbar)
    public int IndexOf(ItemData item)
    {
        if (item == null)
        {
            return -1;
        }

        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == item)
            {
                return i;
            }
        }
        return -1;
    }

    // [v2] ไอเทมนี้ + คุณภาพนี้อยู่ช่องไหน (-1 = ไม่อยู่)
    public int IndexOf(ItemData item, DishQuality? quality)
    {
        if (item == null)
        {
            return -1;
        }

        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == item && qualities[i] == quality)
            {
                return i;
            }
        }
        return -1;
    }

    // ---------- แก้ไขช่อง ----------

    // ไอเทมนี้ใส่ Hotbar ได้หรือไม่: อาวุธ เครื่องมือ หรือของที่กินได้
    // (วัตถุดิบที่กินดิบไม่ได้ ของสวมใส่ และเครื่องประดับ ใส่ไม่ได้)
    public static bool CanAssign(ItemData item)
    {
        if (item == null)
        {
            return false;
        }

        return item.itemType == ItemType.Weapon ||
               item.itemType == ItemType.Tool ||
               item.IsEdible;
    }

    // ใส่ไอเทมลงช่อง ถ้าไอเทมนี้อยู่ช่องอื่นอยู่แล้ว จะย้ายมา (ช่องเดิมว่าง) / คุณภาพ = ไม่ระบุ
    public void SetSlot(int index, ItemData item)
    {
        SetSlot(index, item, null);
    }

    // [v2] ใส่ไอเทมพร้อมคุณภาพ (เฉพาะจาน ของอื่นไม่จำคุณภาพ) ถ้าไอเทม + คุณภาพเดียวกันอยู่ช่องอื่น จะย้ายมา
    public void SetSlot(int index, ItemData item, DishQuality? quality)
    {
        if (!IsValidIndex(index))
        {
            return;
        }

        if (item != null)
        {
            if (!CanAssign(item))
            {
                Debug.Log($"Hotbar: {item.itemName} can't be put in the hotbar.");
                return;
            }

            if (!Inventory.TracksQuality(item))
            {
                quality = null;
            }

            int existing = IndexOf(item, quality);
            if (existing == index)
            {
                return;
            }

            if (existing >= 0)
            {
                slots[existing] = null;
                qualities[existing] = null;
            }
        }
        else
        {
            quality = null;
        }

        slots[index] = item;
        qualities[index] = quality;
        OnSlotsChanged?.Invoke();
    }

    public void ClearSlot(int index)
    {
        if (!IsValidIndex(index) || slots[index] == null)
        {
            return;
        }

        slots[index] = null;
        qualities[index] = null;
        OnSlotsChanged?.Invoke();
    }

    // สลับของสองช่อง (ใช้ตอนลากช่องหนึ่งไปวางอีกช่อง)
    public void SwapSlots(int a, int b)
    {
        if (!IsValidIndex(a) || !IsValidIndex(b) || a == b)
        {
            return;
        }

        ItemData temp = slots[a];
        slots[a] = slots[b];
        slots[b] = temp;

        DishQuality? tempQuality = qualities[a];
        qualities[a] = qualities[b];
        qualities[b] = tempQuality;
        OnSlotsChanged?.Invoke();
    }

    // ---------- เลือกช่อง ----------

    public void Select(int index)
    {
        if (!IsValidIndex(index) || index == selectedIndex)
        {
            return;
        }

        selectedIndex = index;
        OnSelectionChanged?.Invoke(selectedIndex);
    }

    // เลื่อนช่องที่เลือก (+1 = ขวา, -1 = ซ้าย) วนรอบได้
    public void SelectOffset(int offset)
    {
        int next = ((selectedIndex + offset) % SlotCount + SlotCount) % SlotCount;
        Select(next);
    }

    // ---------- เซฟ / โหลด ----------

    // รหัสไอเทมทุกช่อง (ช่องว่าง = "") ใช้เขียนลงเซฟ
    public List<string> CaptureSlotIds()
    {
        List<string> ids = new List<string>(SlotCount);
        for (int i = 0; i < SlotCount; i++)
        {
            ids.Add(slots[i] != null ? slots[i].itemId : "");
        }
        return ids;
    }

    // [v9] คุณภาพทุกช่อง (-1 = ไม่ระบุ / ไม่ใช่จาน / ช่องว่าง) ใช้เขียนลงเซฟ
    public List<int> CaptureSlotQualities()
    {
        List<int> list = new List<int>(SlotCount);
        for (int i = 0; i < SlotCount; i++)
        {
            list.Add(slots[i] != null && qualities[i].HasValue ? (int)qualities[i].Value : -1);
        }
        return list;
    }

    // ตั้งทุกช่องใหม่ (ใช้ตอนโหลดเซฟ) ช่องที่หาไอเทมไม่เจอจะว่าง / คุณภาพ = ไม่ระบุ
    public void SetAllSlots(IList<ItemData> items)
    {
        SetAllSlots(items, null);
    }

    // [v9] ตั้งทุกช่องใหม่พร้อมคุณภาพ (qualityValues null หรือ -1 = ไม่ระบุ เช่น เซฟ v8 ลงมา)
    public void SetAllSlots(IList<ItemData> items, IList<int> qualityValues)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            ItemData item = items != null && i < items.Count ? items[i] : null;

            // ข้ามของที่ใส่ Hotbar ไม่ได้แล้ว (เช่น แก้ตารางจนกินไม่ได้)
            slots[i] = CanAssign(item) ? item : null;

            int q = qualityValues != null && i < qualityValues.Count ? qualityValues[i] : -1;
            bool validQuality = q >= 0 && q < DishQualityUtil.Count;
            qualities[i] = slots[i] != null && validQuality && Inventory.TracksQuality(slots[i]) ? (DishQuality)q : (DishQuality?)null;
        }

        // กันช่องซ้ำ (ไอเทม + คุณภาพเดียวกัน เก็บช่องแรกที่เจอไว้)
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == null) continue;
            for (int j = i + 1; j < SlotCount; j++)
            {
                if (slots[j] == slots[i] && qualities[j] == qualities[i])
                {
                    slots[j] = null;
                    qualities[j] = null;
                }
            }
        }

        OnSlotsChanged?.Invoke();
    }

    private static bool IsValidIndex(int index)
    {
        return index >= 0 && index < SlotCount;
    }

    // ---------- Debug ----------

    // ใส่ของในกระเป๋าลง Hotbar ตามลำดับ (อาวุธ/เครื่องมือก่อน แล้วค่อยของกินได้)
    [ContextMenu("Debug: Fill Hotbar From Inventory")]
    private void DebugFillFromInventory()
    {
        if (inventory == null)
        {
            Debug.LogWarning("Hotbar: No Inventory.");
            return;
        }

        List<ItemData> picks = new List<ItemData>();
        foreach (ItemData item in inventory.Items.Keys)
        {
            if (item != null && (item.itemType == ItemType.Weapon || item.itemType == ItemType.Tool)) picks.Add(item);
        }
        foreach (ItemData item in inventory.Items.Keys)
        {
            if (item != null && item.IsEdible && !picks.Contains(item)) picks.Add(item);
        }

        SetAllSlots(picks);
        Debug.Log($"Hotbar: Filled {Mathf.Min(picks.Count, SlotCount)} slots from inventory.");
    }

#if UNITY_EDITOR
    // ให้ของทดสอบ: อาวุธและเครื่องมือทุกชิ้นในโปรเจกต์ 1 ชิ้น + ของกินได้ 6 ชนิด ชนิดละ 5 ชิ้น แล้วใส่ Hotbar
    [ContextMenu("Debug: Give Test Items + Fill Hotbar")]
    private void DebugGiveTestItems()
    {
        if (!Application.isPlaying || inventory == null)
        {
            Debug.LogWarning("Hotbar: Use this in Play mode on the Player.");
            return;
        }

        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
        int edibleGiven = 0;

        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null) continue;

            if (item.itemType == ItemType.Weapon || item.itemType == ItemType.Tool)
            {
                if (inventory.GetCount(item) == 0) inventory.AddItem(item, 1);
            }
            else if (item.IsEdible && edibleGiven < 6)
            {
                if (inventory.AddItem(item, 5)) edibleGiven++;
            }
        }

        DebugFillFromInventory();
    }

    // ให้ไอเทมตามรหัสในช่อง Debug Item Id (หาจาก ItemData ทุกตัวในโปรเจกต์ ไม่ต้องอยู่ใน ItemDatabase)
    [ContextMenu("Debug: Give Item By Id")]
    private void DebugGiveItemById()
    {
        if (!Application.isPlaying || inventory == null)
        {
            Debug.LogWarning("Hotbar: Use this in Play mode on the Player.");
            return;
        }

        string wanted = debugItemId != null ? debugItemId.Trim() : "";
        if (wanted.Length == 0)
        {
            Debug.LogWarning("Hotbar: Debug Item Id is empty.");
            return;
        }

        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null || !string.Equals(item.itemId, wanted, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int amount = Mathf.Max(1, debugAmount);
            if (inventory.AddItem(item, amount))
            {
                Debug.Log($"Hotbar: Gave {amount} x {item.itemName} ({item.itemId}).");
            }
            else
            {
                Debug.LogWarning($"Hotbar: Could not give {amount} x {item.itemName}. Bag stack cap is {inventory.StackCap}.");
            }
            return;
        }

        Debug.LogWarning($"Hotbar: No ItemData with itemId '{wanted}' found in the project.");
    }
#endif
}
