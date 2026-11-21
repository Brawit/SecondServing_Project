using System;
using System.Collections.Generic;
using UnityEngine;

// ช่องสวมใส่บนตัวละคร 8 ช่อง (ลำดับนี้ใช้ในไฟล์เซฟด้วย)
// หมายเหตุ: เพิ่มค่าใหม่ต่อท้ายเท่านั้น ห้ามสลับลำดับ
public enum EquipSlot
{
    // ช่องเพิ่มค่าสถานะ
    Shoes,
    Ring1,
    Ring2,
    Necklace,

    // ช่องแฟชัน (แค่หน้าตา ไม่มีผลกับค่าสถานะ)
    Head,
    Top,
    Bottom,
    Back
}

// ของที่ผู้เล่นสวมอยู่ (ข้อมูล) วางบนตัว Player คู่กับ Inventory
// - สวม = ย้ายของ 1 ชิ้นออกจากกระเป๋ามาไว้ที่ช่อง
// - ถอด = ย้ายกลับเข้ากระเป๋า (ถ้ากระเป๋าเต็มเพดานของชนิดนั้น ถอดไม่ได้)
// - สวมทับช่องที่มีของอยู่ = ของเดิมกลับเข้ากระเป๋า
// ตอนนี้ยังไม่มีผลกับค่าสถานะ (เฟส 3 จะอ่านช่องเพิ่มค่าสถานะแล้วเรียก PlayerStats.SetBonusMax / SetManaRegenSource)
public class PlayerEquipment : MonoBehaviour
{
    // จำนวนช่องทั้งหมด
    public const int SlotCount = 8;

    [Header("References")]
    [Tooltip("กระเป๋าของผู้เล่น (เว้นว่างไว้ จะหาบน GameObject เดียวกัน)")]
    [SerializeField] private Inventory inventory;

    [Header("Messages")]
    [Tooltip("ข้อความตอนของชิ้นนั้นใส่ช่องนี้ไม่ได้ ({0} = ชื่อไอเทม, {1} = ชื่อช่อง)")]
    [SerializeField] private string wrongSlotMessageFormat = "{0} can't go in the {1} slot.";

    [Tooltip("ข้อความตอนไม่มีของชิ้นนั้นในกระเป๋า ({0} = ชื่อไอเทม)")]
    [SerializeField] private string notInBagMessageFormat = "You don't have {0} in your bag.";

    [Tooltip("ข้อความตอนถอดไม่ได้เพราะกระเป๋าเต็ม ({0} = ชื่อไอเทม)")]
    [SerializeField] private string bagFullMessageFormat = "No room in your bag for {0}.";

    private readonly ItemData[] slots = new ItemData[SlotCount];

    // ---------- Events ----------

    // ส่งสัญญาณเมื่อของที่สวมเปลี่ยน (สวม/ถอด/สลับ/โหลดเซฟ)
    public event Action OnChanged;

    // ---------- ข้อมูลให้ระบบอื่นอ่าน ----------

    public Inventory Inventory => inventory;

    private void Awake()
    {
        if (inventory == null)
        {
            inventory = GetComponent<Inventory>();
        }

        if (inventory == null)
        {
            Debug.LogWarning("PlayerEquipment: No Inventory found on this GameObject.", this);
        }
    }

    public ItemData GetSlot(EquipSlot slot)
    {
        int index = (int)slot;
        return IsValidIndex(index) ? slots[index] : null;
    }

    // สวมไอเทมนี้อยู่กี่ชิ้น (แหวนวงเดียวกันใส่ได้ 2 ช่องถ้ามี 2 วง)
    public int CountEquipped(ItemData item)
    {
        if (item == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == item)
            {
                count++;
            }
        }
        return count;
    }

    // ช่องนี้เป็นช่องเพิ่มค่าสถานะหรือไม่ (false = ช่องแฟชัน)
    public static bool IsStatSlot(EquipSlot slot)
    {
        return slot == EquipSlot.Shoes || slot == EquipSlot.Ring1 || slot == EquipSlot.Ring2 || slot == EquipSlot.Necklace;
    }

    // ประเภทของที่ช่องนี้รับ
    public static EquipType GetAcceptedType(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.Shoes: return EquipType.Shoes;
            case EquipSlot.Ring1:
            case EquipSlot.Ring2: return EquipType.Ring;
            case EquipSlot.Necklace: return EquipType.Necklace;
            case EquipSlot.Head: return EquipType.Head;
            case EquipSlot.Top: return EquipType.Top;
            case EquipSlot.Bottom: return EquipType.Bottom;
            case EquipSlot.Back: return EquipType.Back;
            default: return EquipType.None;
        }
    }

    // ชื่อช่องที่แสดงในเกม
    public static string GetSlotLabel(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.Ring1: return "Ring";
            case EquipSlot.Ring2: return "Ring";
            default: return slot.ToString();
        }
    }

    // ไอเทมนี้ใส่ช่องนี้ได้หรือไม่ (null = ได้ หมายถึงช่องว่าง)
    public static bool CanEquipInSlot(ItemData item, EquipSlot slot)
    {
        if (item == null)
        {
            return true;
        }

        return item.IsEquippable && item.equipType == GetAcceptedType(slot);
    }

    // หาช่องแรกที่รับไอเทมนี้ได้ (เลือกช่องว่างก่อน) คืนค่า false ถ้าสวมไม่ได้เลย
    public bool TryFindSlotFor(ItemData item, out EquipSlot result)
    {
        result = EquipSlot.Shoes;
        bool found = false;

        for (int i = 0; i < SlotCount; i++)
        {
            EquipSlot slot = (EquipSlot)i;
            if (item == null || !CanEquipInSlot(item, slot))
            {
                continue;
            }

            if (slots[i] == null)
            {
                result = slot;
                return true;
            }

            if (!found)
            {
                result = slot;
                found = true;
            }
        }

        return found;
    }

    // ---------- สวม / ถอด / สลับ ----------

    // สวมไอเทมจากกระเป๋าลงช่องนี้ ถ้าช่องมีของอยู่ ของเดิมจะกลับเข้ากระเป๋า คืนค่า true ถ้าสำเร็จ
    public bool TryEquip(EquipSlot slot, ItemData item)
    {
        int index = (int)slot;
        if (!IsValidIndex(index) || item == null || inventory == null)
        {
            return false;
        }

        if (!CanEquipInSlot(item, slot))
        {
            NotificationUI.Post(string.Format(wrongSlotMessageFormat, item.itemName, GetSlotLabel(slot)));
            Debug.Log($"PlayerEquipment: {item.itemName} ({item.equipType}) can't go in slot {slot}.");
            return false;
        }

        ItemData old = slots[index];
        if (old == item)
        {
            // ใส่ชิ้นเดิมอยู่แล้ว ไม่ต้องทำอะไร
            return true;
        }

        if (inventory.GetCount(item) <= 0)
        {
            NotificationUI.Post(string.Format(notInBagMessageFormat, item.itemName));
            Debug.Log($"PlayerEquipment: No {item.itemName} in the bag to equip.");
            return false;
        }

        // ของเดิมต้องกลับเข้ากระเป๋าได้ก่อน ถึงจะสวมของใหม่
        if (old != null && inventory.GetSpaceFor(old) < 1)
        {
            NotificationUI.Post(string.Format(bagFullMessageFormat, old.itemName));
            Debug.Log($"PlayerEquipment: Bag is full for {old.itemName}. Can't swap.");
            return false;
        }

        if (!inventory.RemoveItem(item, 1))
        {
            return false;
        }

        if (old != null)
        {
            inventory.AddItem(old, 1);
        }

        slots[index] = item;
        Debug.Log(old != null
            ? $"PlayerEquipment: Equipped {item.itemName} in {slot} (returned {old.itemName} to bag)."
            : $"PlayerEquipment: Equipped {item.itemName} in {slot}.");

        OnChanged?.Invoke();
        return true;
    }

    // ถอดของในช่องนี้กลับเข้ากระเป๋า คืนค่า true ถ้าสำเร็จ (ช่องว่างอยู่แล้ว = false)
    public bool TryUnequip(EquipSlot slot)
    {
        int index = (int)slot;
        if (!IsValidIndex(index) || slots[index] == null || inventory == null)
        {
            return false;
        }

        ItemData item = slots[index];

        if (inventory.GetSpaceFor(item) < 1)
        {
            NotificationUI.Post(string.Format(bagFullMessageFormat, item.itemName));
            Debug.Log($"PlayerEquipment: Bag is full for {item.itemName}. Can't unequip.");
            return false;
        }

        inventory.AddItem(item, 1);
        slots[index] = null;
        Debug.Log($"PlayerEquipment: Unequipped {item.itemName} from {slot}.");

        OnChanged?.Invoke();
        return true;
    }

    // สลับของสองช่อง (เช่น แหวนช่อง 1 กับช่อง 2) ทั้งสองช่องต้องรับของของอีกฝั่งได้
    public bool TrySwap(EquipSlot a, EquipSlot b)
    {
        int ia = (int)a;
        int ib = (int)b;
        if (!IsValidIndex(ia) || !IsValidIndex(ib) || ia == ib)
        {
            return false;
        }

        ItemData itemA = slots[ia];
        ItemData itemB = slots[ib];

        if (!CanEquipInSlot(itemA, b) || !CanEquipInSlot(itemB, a))
        {
            ItemData blocked = !CanEquipInSlot(itemA, b) ? itemA : itemB;
            EquipSlot target = !CanEquipInSlot(itemA, b) ? b : a;
            NotificationUI.Post(string.Format(wrongSlotMessageFormat, blocked.itemName, GetSlotLabel(target)));
            return false;
        }

        slots[ia] = itemB;
        slots[ib] = itemA;
        OnChanged?.Invoke();
        return true;
    }

    // ---------- เซฟ / โหลด ----------

    // รหัสไอเทมทุกช่องตามลำดับ EquipSlot (ช่องว่าง = "") ใช้เขียนลงเซฟ
    public List<string> CaptureSlotIds()
    {
        List<string> ids = new List<string>(SlotCount);
        for (int i = 0; i < SlotCount; i++)
        {
            ids.Add(slots[i] != null ? slots[i].itemId : "");
        }
        return ids;
    }

    // ตั้งทุกช่องใหม่ (ใช้ตอนโหลดเซฟ) ไม่แตะกระเป๋า เพราะของที่สวมอยู่ไม่ได้อยู่ในกระเป๋าตั้งแต่ตอนเซฟ
    // ของที่ใส่ช่องนั้นไม่ได้แล้ว (เช่น เปลี่ยน Equip Type) จะกลับเข้ากระเป๋าแทน
    public void SetAllSlots(IList<ItemData> items)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            ItemData item = items != null && i < items.Count ? items[i] : null;

            if (item != null && !CanEquipInSlot(item, (EquipSlot)i))
            {
                Debug.LogWarning($"PlayerEquipment: {item.itemName} can no longer go in {(EquipSlot)i}. Returned to bag.");
                if (inventory != null)
                {
                    inventory.AddItem(item, 1);
                }
                item = null;
            }

            slots[i] = item;
        }

        OnChanged?.Invoke();
    }

    private static bool IsValidIndex(int index)
    {
        return index >= 0 && index < SlotCount;
    }

    // ---------- Debug (คลิกขวาที่หัวคอมโพเนนต์ใน Inspector ระหว่าง Play) ----------

    [ContextMenu("Debug: Print Equipment")]
    private void DebugPrint()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            EquipSlot slot = (EquipSlot)i;
            string kind = IsStatSlot(slot) ? "stat" : "fashion";
            Debug.Log($"PlayerEquipment: {slot} ({kind}) = {(slots[i] != null ? slots[i].itemName : "(empty)")}");
        }
    }

    [ContextMenu("Debug: Unequip All")]
    private void DebugUnequipAll()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            TryUnequip((EquipSlot)i);
        }
    }

#if UNITY_EDITOR
    // ให้ของสวมใส่ทุกชิ้นในโปรเจกต์ชิ้นละ 1 (แหวนชิ้นละ 2) เข้ากระเป๋า
    [ContextMenu("Debug: Give All Equippable Items")]
    private void DebugGiveAllEquippable()
    {
        if (!Application.isPlaying || inventory == null)
        {
            Debug.LogWarning("PlayerEquipment: Use this in Play mode on the Player.");
            return;
        }

        int given = 0;
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null || !item.IsEquippable)
            {
                continue;
            }

            int amount = item.equipType == EquipType.Ring ? 2 : 1;
            if (inventory.AddItem(item, amount))
            {
                given++;
            }
        }

        Debug.Log($"PlayerEquipment: Gave {given} equippable item types.");
    }
#endif
}
