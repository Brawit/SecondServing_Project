using System;
using System.Collections.Generic;
using UnityEngine;

// กระเป๋าวิเศษของผู้เล่น: ของคงสภาพเดิมเสมอ ไม่มีช่องให้จัด เก็บได้ทุกชนิด
// แต่ละชนิดเก็บได้ไม่เกิน "เพดานต่อชนิด" ตามระดับกระเป๋า (อัปเกรดได้)
// และจำไว้ว่าเคยเจอไอเทมไหนมาแล้วบ้าง (ใช้แสดงแบบ Collectibles)
// [2.5] จานอาหาร (ItemType.Dish) แยกนับตามคุณภาพ (OK / Good / Perfect) แต่ยังเป็นช่องเดียวกัน
// จำนวนรวม (GetCount) = ผลรวมทุกคุณภาพเสมอ / ของที่ไม่ระบุคุณภาพตอนเพิ่ม = Good / เอาออกแบบไม่ระบุ = เอาคุณภาพต่ำสุดออกก่อน
public class Inventory : MonoBehaviour
{
    [Header("Bag Levels")]
    [Tooltip("จำนวนสูงสุดต่อชนิดในแต่ละระดับกระเป๋า (ช่องแรก = ระดับ 1)")]
    [SerializeField] private int[] stackCapsByLevel = { 30, 99, 300, 999 };

    [Tooltip("ระดับกระเป๋าตอนเริ่มเกม (1 = ระดับแรก)")]
    [SerializeField] private int startingBagLevel = 1;

    [Header("Messages")]
    [Tooltip("ข้อความตอนเก็บเพิ่มไม่ได้เพราะเต็ม ({0} = ชื่อไอเทม, {1} = เพดาน)")]
    [SerializeField] private string fullMessageFormat = "{0} is full ({1}). Upgrade your bag to carry more.";

    // ของในกระเป๋า "ชนิดของ -> จำนวน" (ไม่เก็บชนิดที่เหลือ 0)
    private readonly Dictionary<ItemData, int> items = new Dictionary<ItemData, int>();

    // ไอเทมที่เคยเก็บได้อย่างน้อย 1 ครั้ง
    private readonly HashSet<ItemData> discovered = new HashSet<ItemData>();

    // [2.5] จำนวนจานแยกตามคุณภาพ "จาน -> [OK, Good, Perfect]" (เฉพาะ ItemType.Dish)
    private readonly Dictionary<ItemData, int[]> dishQualities = new Dictionary<ItemData, int[]>();

    private int bagLevel;

    // ---------- Events ----------

    // ส่งสัญญาณเมื่อจำนวนของ ระดับกระเป๋า หรือรายการที่เคยเจอเปลี่ยน
    public event Action OnChanged;

    // ส่งสัญญาณเมื่อเจอไอเทมชนิดใหม่เป็นครั้งแรก
    public event Action<ItemData> OnItemDiscovered;

    // ---------- ข้อมูลให้ระบบอื่นอ่าน ----------

    public IReadOnlyDictionary<ItemData, int> Items => items;
    public IReadOnlyCollection<ItemData> DiscoveredItems => discovered;

    // ระดับกระเป๋าปัจจุบัน (เริ่มที่ 1)
    public int BagLevel => bagLevel;
    public int MaxBagLevel => stackCapsByLevel != null && stackCapsByLevel.Length > 0 ? stackCapsByLevel.Length : 1;
    public bool CanUpgradeBag => bagLevel < MaxBagLevel;

    // จำนวนสูงสุดต่อชนิดในระดับปัจจุบัน
    public int StackCap => GetStackCapForLevel(bagLevel);

    private void Awake()
    {
        bagLevel = Mathf.Clamp(startingBagLevel, 1, MaxBagLevel);
    }

    private void OnValidate()
    {
        if (stackCapsByLevel == null || stackCapsByLevel.Length == 0)
        {
            stackCapsByLevel = new[] { 30 };
        }

        for (int i = 0; i < stackCapsByLevel.Length; i++)
        {
            stackCapsByLevel[i] = Mathf.Max(1, stackCapsByLevel[i]);
        }

        startingBagLevel = Mathf.Clamp(startingBagLevel, 1, stackCapsByLevel.Length);
    }

    public int GetStackCapForLevel(int level)
    {
        if (stackCapsByLevel == null || stackCapsByLevel.Length == 0)
        {
            return 30;
        }

        int index = Mathf.Clamp(level, 1, stackCapsByLevel.Length) - 1;
        return stackCapsByLevel[index];
    }

    // ---------- เพิ่ม / ลด ----------

    // เก็บเพิ่มได้อีกกี่ชิ้นก่อนชนเพดาน
    public int GetSpaceFor(ItemData item)
    {
        if (item == null)
        {
            return 0;
        }

        return Mathf.Max(0, StackCap - GetCount(item));
    }

    // เพิ่มของเข้ากระเป๋า เพิ่มได้ทั้งหมดหรือไม่เพิ่มเลย คืนค่า true ถ้าสำเร็จ (จานอาหาร = คุณภาพ Good)
    public bool AddItem(ItemData item, int amount = 1)
    {
        return AddItem(item, amount, DishQuality.Good);
    }

    // [2.5] เพิ่มของพร้อมคุณภาพ (คุณภาพมีผลเฉพาะจานอาหาร)
    public bool AddItem(ItemData item, int amount, DishQuality quality)
    {
        if (item == null || amount <= 0) return false;

        if (GetSpaceFor(item) < amount)
        {
            Debug.Log($"Inventory: {item.itemName} is full ({StackCap}).");
            NotificationUI.Post(string.Format(fullMessageFormat, item.itemName, StackCap));
            return false;
        }

        items.TryGetValue(item, out int current);
        items[item] = current + amount;

        if (TracksQuality(item))
        {
            GetQualityArray(item)[(int)quality] += amount;
        }

        MarkDiscovered(item, true);

        OnChanged?.Invoke();
        return true;
    }

    // เอาของออกจากกระเป๋า คืนค่า true ถ้ามีพอและเอาออกสำเร็จ
    // จานอาหาร: เอาคุณภาพต่ำสุดออกก่อน (เก็บจานดีไว้ เช่น ตอนกินจาก Hotbar)
    public bool RemoveItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;
        if (!items.TryGetValue(item, out int current) || current < amount) return false;

        if (TracksQuality(item))
        {
            int[] counts = GetQualityArray(item);
            int left = amount;
            for (int q = 0; q < DishQualityUtil.Count && left > 0; q++)
            {
                int take = Mathf.Min(counts[q], left);
                counts[q] -= take;
                left -= take;
            }
        }

        SetTotal(item, current - amount);
        OnChanged?.Invoke();
        return true;
    }

    // [2.5] เอาจานคุณภาพที่ระบุออก คืนค่า true ถ้ามีพอ (ของที่ไม่ใช่จาน = เหมือน RemoveItem ปกติ)
    public bool RemoveItem(ItemData item, int amount, DishQuality quality)
    {
        if (item == null || amount <= 0) return false;
        if (!TracksQuality(item)) return RemoveItem(item, amount);
        if (!items.TryGetValue(item, out int current) || current < amount) return false;

        int[] counts = GetQualityArray(item);
        if (counts[(int)quality] < amount) return false;

        counts[(int)quality] -= amount;
        SetTotal(item, current - amount);
        OnChanged?.Invoke();
        return true;
    }

    // ---------- [2.5] คุณภาพจาน ----------

    // ไอเทมนี้แยกนับตามคุณภาพไหม (เฉพาะจานอาหาร)
    public static bool TracksQuality(ItemData item)
    {
        return item != null && item.itemType == ItemType.Dish;
    }

    // จำนวนจานคุณภาพนี้ (ของที่ไม่ใช่จาน: ถือว่าเป็น Good ทั้งหมด)
    public int GetCount(ItemData item, DishQuality quality)
    {
        if (item == null)
        {
            return 0;
        }

        if (!TracksQuality(item))
        {
            return quality == DishQuality.Good ? GetCount(item) : 0;
        }

        return dishQualities.TryGetValue(item, out int[] counts) ? counts[(int)quality] : 0;
    }

    // คุณภาพดีที่สุดที่มีอยู่ คืนค่า false ถ้าไม่มีเลย
    public bool TryGetBestQuality(ItemData item, out DishQuality quality)
    {
        foreach (DishQuality q in DishQualityUtil.BestFirst)
        {
            if (GetCount(item, q) > 0)
            {
                quality = q;
                return true;
            }
        }

        quality = DishQuality.Good;
        return false;
    }

    // ใส่รายการคุณภาพที่มีอยู่ (เรียงดีสุดก่อน) ลงใน result คืนค่าจำนวนที่ใส่
    public int GetAvailableQualities(ItemData item, List<DishQuality> result)
    {
        result.Clear();
        foreach (DishQuality q in DishQualityUtil.BestFirst)
        {
            if (GetCount(item, q) > 0)
            {
                result.Add(q);
            }
        }
        return result.Count;
    }

    // ตั้งจำนวนแยกตามคุณภาพของจาน (ใช้ตอนโหลดเซฟ) ผลรวมต้องเท่าจำนวนในกระเป๋า ไม่งั้นไม่เปลี่ยน
    public void SetDishQualities(ItemData item, int ok, int good, int perfect)
    {
        if (!TracksQuality(item))
        {
            return;
        }

        int total = GetCount(item);
        if (ok < 0 || good < 0 || perfect < 0 || ok + good + perfect != total)
        {
            Debug.LogWarning($"Inventory: Quality counts for {item.itemName} ({ok}/{good}/{perfect}) don't match {total}. Keeping them as Good.");
            return;
        }

        int[] counts = GetQualityArray(item);
        counts[(int)DishQuality.Ok] = ok;
        counts[(int)DishQuality.Good] = good;
        counts[(int)DishQuality.Perfect] = perfect;
        OnChanged?.Invoke();
    }

    private int[] GetQualityArray(ItemData item)
    {
        if (!dishQualities.TryGetValue(item, out int[] counts))
        {
            counts = new int[DishQualityUtil.Count];
            dishQualities[item] = counts;
        }
        return counts;
    }

    // ตั้งจำนวนรวม (0 = ลบออกจากกระเป๋า รวมถึงตัวนับคุณภาพ)
    private void SetTotal(ItemData item, int total)
    {
        if (total <= 0)
        {
            items.Remove(item);
            dishQualities.Remove(item);
        }
        else
        {
            items[item] = total;
        }
    }

    // ดูว่ามีของชนิดนี้กี่ชิ้น
    public int GetCount(ItemData item)
    {
        if (item == null)
        {
            return 0;
        }

        return items.TryGetValue(item, out int count) ? count : 0;
    }

    // เคยเก็บไอเทมนี้มาแล้วหรือยัง
    public bool IsDiscovered(ItemData item)
    {
        return item != null && discovered.Contains(item);
    }

    // นับว่าเจอแล้วกี่ชนิดในรายการที่ส่งมา (ใช้แสดงความคืบหน้า เช่น 12/83)
    public int CountDiscovered(IEnumerable<ItemData> pool)
    {
        int count = 0;
        if (pool == null)
        {
            return count;
        }

        foreach (ItemData item in pool)
        {
            if (IsDiscovered(item))
            {
                count++;
            }
        }
        return count;
    }

    // บันทึกว่าเจอไอเทมนี้แล้ว (raiseEvent = ยิง OnItemDiscovered ถ้าเพิ่งเจอครั้งแรก)
    private void MarkDiscovered(ItemData item, bool raiseEvent)
    {
        if (item == null || !discovered.Add(item))
        {
            return;
        }

        if (raiseEvent)
        {
            Debug.Log($"Inventory: Discovered {item.itemName}!");
            OnItemDiscovered?.Invoke(item);
        }
    }

    // ---------- ระดับกระเป๋า ----------

    // อัปเกรดกระเป๋าขึ้น 1 ระดับ คืนค่า true ถ้าสำเร็จ
    public bool UpgradeBag()
    {
        if (!CanUpgradeBag)
        {
            Debug.Log("Inventory: Bag is already at max level.");
            return false;
        }

        bagLevel++;
        Debug.Log($"Inventory: Bag upgraded to level {bagLevel} (max {StackCap} per item).");
        OnChanged?.Invoke();
        return true;
    }

    // ---------- โหลดเซฟ ----------

    // ล้างกระเป๋าแล้วใส่ของชุดใหม่ทั้งหมด (ใช้ตอนโหลดเซฟ) แจ้ง OnChanged ครั้งเดียว
    // ของในเซฟที่เกินเพดานยังเก็บไว้ครบ (ไม่ตัดทิ้ง) แค่เก็บเพิ่มไม่ได้จนกว่าจะใช้ไป
    public void SetItems(IReadOnlyDictionary<ItemData, int> newItems)
    {
        items.Clear();
        dishQualities.Clear();

        if (newItems != null)
        {
            foreach (KeyValuePair<ItemData, int> pair in newItems)
            {
                if (pair.Key == null || pair.Value <= 0) continue;

                items[pair.Key] = pair.Value;

                // [2.5] จานอาหาร: ตั้งเป็น Good ทั้งหมดก่อน (SaveManager ตั้งคุณภาพจริงทีหลังด้วย SetDishQualities)
                if (TracksQuality(pair.Key))
                {
                    GetQualityArray(pair.Key)[(int)DishQuality.Good] = pair.Value;
                }

                // ของที่มีอยู่ในกระเป๋า ถือว่าเคยเจอแล้ว (รองรับเซฟเก่าที่ไม่มีรายการที่เคยเจอ)
                MarkDiscovered(pair.Key, false);
            }
        }

        OnChanged?.Invoke();
    }

    // ตั้งรายการไอเทมที่เคยเจอ (ใช้ตอนโหลดเซฟ) ของเดิมในกระเป๋ายังถือว่าเคยเจอ
    public void SetDiscovered(IEnumerable<ItemData> discoveredItems)
    {
        discovered.Clear();

        if (discoveredItems != null)
        {
            foreach (ItemData item in discoveredItems)
            {
                MarkDiscovered(item, false);
            }
        }

        foreach (ItemData item in items.Keys)
        {
            MarkDiscovered(item, false);
        }

        OnChanged?.Invoke();
    }

    // ตั้งระดับกระเป๋า (ใช้ตอนโหลดเซฟ)
    public void SetBagLevel(int level)
    {
        bagLevel = Mathf.Clamp(level, 1, MaxBagLevel);
        OnChanged?.Invoke();
    }

    // ---------- Debug ----------

    // พิมพ์รายการในกระเป๋าลง Console
    public void PrintInventory()
    {
        if (items.Count == 0)
        {
            Debug.Log("Inventory: (empty)");
            return;
        }

        foreach (KeyValuePair<ItemData, int> pair in items)
        {
            if (TracksQuality(pair.Key))
            {
                Debug.Log($"Inventory: {pair.Key.itemName} x{pair.Value} (OK {GetCount(pair.Key, DishQuality.Ok)}, " +
                          $"Good {GetCount(pair.Key, DishQuality.Good)}, Perfect {GetCount(pair.Key, DishQuality.Perfect)})");
            }
            else
            {
                Debug.Log($"Inventory: {pair.Key.itemName} x{pair.Value}");
            }
        }
    }

    // คลิกขวาที่หัวคอมโพเนนต์ใน Inspector ระหว่าง Play เพื่ออัปเกรดกระเป๋า
    [ContextMenu("Debug: Upgrade Bag")]
    private void DebugUpgradeBag()
    {
        UpgradeBag();
    }

    [ContextMenu("Debug: Print Inventory")]
    private void DebugPrintInventory()
    {
        PrintInventory();
    }

    // [2.5] ให้จานของทุกสูตรที่รู้ คุณภาพละ 2 จาน (OK / Good / Perfect) ไว้ทดสอบการเสิร์ฟแบบเลือกคุณภาพ
    [ContextMenu("Debug: Give Known Dishes (2 of each quality)")]
    private void DebugGiveMixedDishes()
    {
        RecipeBook book = GetComponent<RecipeBook>();
        if (book == null)
        {
            Debug.LogWarning("Inventory: No RecipeBook on this object.");
            return;
        }

        int given = 0;
        foreach (RecipeData recipe in book.KnownRecipes)
        {
            if (recipe == null || recipe.dishItem == null)
            {
                continue;
            }

            for (int q = 0; q < DishQualityUtil.Count; q++)
            {
                if (AddItem(recipe.dishItem, 2, (DishQuality)q)) given += 2;
            }
        }

        Debug.Log($"Inventory: Gave {given} dishes (2 of each quality per known recipe).");
    }
}
