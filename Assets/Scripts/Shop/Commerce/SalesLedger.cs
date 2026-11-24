using System;
using System.Collections.Generic;
using UnityEngine;

// [2.6b] ของหนึ่งรายการในกล่องขายข้ามคืน (ของที่ไม่ใช่จาน = คุณภาพ Good เสมอ)
[Serializable]
public struct BoxEntry
{
    public ItemData item;
    public DishQuality quality;
    public int amount;
}

// [2.6b] สมุดบัญชีการขายของผู้เล่น (singleton วางบนวัตถุ SalesLedger ในฉาก)
// - ราคาขาย = 100% ของ ItemData.sellPrice / จานคูณตามคุณภาพ (OK 1.0 / Good 1.1 / Perfect 1.25) ปัดลงต่อชิ้น
// - ขายตรง (ร้านของชำ) = หักของ + ได้เงินทันที
// - กล่องขายข้ามคืน = ย้ายของออกจากกระเป๋าทันที เอาคืนได้จนกว่าจะนอน / ขายจริงตอนจบวัน (DayEndManager เรียก SettleDay)
// - ชื่อเสียงจากการขายจาน: จานที่ขายวันนี้ (ร้าน + กล่องรวมกัน) ทุก 5 จาน = +2 สูงสุด +10 ต่อวัน ไม่สะสมข้ามวัน / วัตถุดิบไม่นับ
// - ตัวนับรายวันรีเซ็ตตอนเริ่มวันใหม่ (GameClock.OnDayStarted) / ไม่มีข้อมูลในเซฟ เพราะเกมเซฟตอนจบวันหลังปิดยอดแล้ว
// สร้างและผูกอัตโนมัติได้จาก Tools > Second Serving > Build Shops
[DefaultExecutionOrder(-50)]
public class SalesLedger : MonoBehaviour
{
    public static SalesLedger Instance { get; private set; }

    // ตัวคูณราคาตามคุณภาพจาน (ใช้เมื่อยังไม่มี SalesLedger ในฉาก)
    private static readonly float[] DefaultQualityMultipliers = { 1f, 1.1f, 1.25f };

    [Header("References")]
    [Tooltip("กระเป๋าของผู้เล่น (เว้นว่าง = หาเองจาก Player)")]
    [SerializeField] private Inventory inventory;

    [Header("Prices")]
    [Tooltip("ตัวคูณราคาขายจานตามคุณภาพ เรียงตาม enum DishQuality: OK, Good, Perfect")]
    [SerializeField] private float[] qualityMultipliers = { 1f, 1.1f, 1.25f };

    [Header("Reputation From Sales")]
    [Tooltip("ขายจานครบกี่จานต่อวันจึงได้ชื่อเสียง 1 ขั้น")]
    [Min(1)] [SerializeField] private int dishesPerStep = 5;

    [Tooltip("ชื่อเสียงที่ได้ต่อ 1 ขั้น")]
    [Min(0)] [SerializeField] private int reputationPerStep = 2;

    [Tooltip("ชื่อเสียงจากการขายสูงสุดต่อวัน")]
    [Min(0)] [SerializeField] private int maxReputationPerDay = 10;

    // ของในกล่องวันนี้ (ยังไม่ขาย)
    private readonly List<BoxEntry> box = new List<BoxEntry>();

    // ---------- Events ----------

    // ส่งสัญญาณเมื่อกล่อง ยอดขาย หรือตัวนับเปลี่ยน
    public event Action OnChanged;

    // ---------- ข้อมูลให้ระบบอื่นอ่าน ----------

    public IReadOnlyList<BoxEntry> Box => box;

    // เงินที่ได้จากการขายตรงวันนี้
    public int ShopIncomeToday { get; private set; }

    // จานที่ขายตรงวันนี้
    public int DishesSoldDirectToday { get; private set; }

    // จานในกล่องที่ปิดยอดไปแล้ววันนี้ (ปกติ 0 จนกว่าจะจบวัน / Debug: Sell Box Now)
    public int DishesShippedToday { get; private set; }

    // ชื่อเสียงจากการขายที่ให้ไปแล้ววันนี้ (กันให้ซ้ำ)
    public int ReputationGrantedToday { get; private set; }

    public int DishesPerStep => Mathf.Max(1, dishesPerStep);
    public int MaxReputationPerDay => Mathf.Max(0, maxReputationPerDay);

    // จำนวนจานที่ต้องขายให้ได้ชื่อเสียงสูงสุดของวัน (เช่น 25)
    public int DishesForMaxReputation
    {
        get
        {
            if (reputationPerStep <= 0) return 0;
            int steps = Mathf.CeilToInt((float)MaxReputationPerDay / reputationPerStep);
            return steps * DishesPerStep;
        }
    }

    // มูลค่าของทั้งหมดในกล่องตอนนี้
    public int BoxTotal
    {
        get
        {
            int total = 0;
            foreach (BoxEntry entry in box)
            {
                total += UnitSellPrice(entry.item, entry.quality) * entry.amount;
            }
            return total;
        }
    }

    // จำนวนจานในกล่องตอนนี้
    public int DishesInBox
    {
        get
        {
            int count = 0;
            foreach (BoxEntry entry in box)
            {
                if (Inventory.TracksQuality(entry.item)) count += entry.amount;
            }
            return count;
        }
    }

    // จานที่นับชื่อเสียงวันนี้ (ขายตรง + ปิดยอดกล่องแล้ว + ยังอยู่ในกล่อง)
    public int DishesCountedToday => DishesSoldDirectToday + DishesShippedToday + DishesInBox;

    // ชื่อเสียงที่จะได้ตอนจบวันถ้ายังเป็นแบบนี้ (ยังไม่หักส่วนที่ให้ไปแล้ว)
    public int PendingReputation => ReputationFor(DishesCountedToday);

    // ---------- Unity ----------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("SalesLedger: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (inventory == null)
        {
            PlayerInteractor player = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
            if (player != null) inventory = player.GetComponent<Inventory>();
        }
        if (inventory == null) Debug.LogWarning("SalesLedger: Inventory not found. Selling won't work.", this);
    }

    private void Start()
    {
        if (GameClock.Instance != null) GameClock.Instance.OnDayStarted += HandleDayStarted;
    }

    private void OnDestroy()
    {
        if (GameClock.Instance != null) GameClock.Instance.OnDayStarted -= HandleDayStarted;
        if (Instance == this) Instance = null;
    }

    private void OnValidate()
    {
        if (qualityMultipliers == null || qualityMultipliers.Length != DishQualityUtil.Count)
        {
            float[] fixedArray = (float[])DefaultQualityMultipliers.Clone();
            if (qualityMultipliers != null)
            {
                for (int i = 0; i < Mathf.Min(qualityMultipliers.Length, fixedArray.Length); i++) fixedArray[i] = qualityMultipliers[i];
            }
            qualityMultipliers = fixedArray;
        }
    }

    // ---------- ราคา ----------

    // ขายได้ไหม (ราคาขายต้องมากกว่า 0)
    public static bool CanSell(ItemData item)
    {
        return item != null && item.sellPrice > 0;
    }

    public static float QualityMultiplier(DishQuality quality)
    {
        float[] source = Instance != null && Instance.qualityMultipliers != null && Instance.qualityMultipliers.Length == DishQualityUtil.Count
            ? Instance.qualityMultipliers
            : DefaultQualityMultipliers;
        int index = Mathf.Clamp((int)quality, 0, source.Length - 1);
        return Mathf.Max(0f, source[index]);
    }

    // ราคาขายต่อชิ้น (จาน = คูณคุณภาพแล้วปัดลง / อย่างอื่น = ราคาเต็ม)
    public static int UnitSellPrice(ItemData item, DishQuality quality)
    {
        if (!CanSell(item)) return 0;
        if (!Inventory.TracksQuality(item)) return item.sellPrice;
        return Mathf.FloorToInt(item.sellPrice * QualityMultiplier(quality) + 0.0001f);
    }

    // ชื่อเสียงจากจำนวนจานที่ขายในวันเดียว
    public int ReputationFor(int dishes)
    {
        if (dishes <= 0) return 0;
        int steps = dishes / DishesPerStep;
        return Mathf.Min(MaxReputationPerDay, steps * Mathf.Max(0, reputationPerStep));
    }

    // ---------- ขายตรง (ร้านของชำ) ----------

    // มีของ (คุณภาพนี้) พอขายไหม
    public bool HasStock(ItemData item, int amount, DishQuality quality)
    {
        if (inventory == null || item == null || amount <= 0) return false;
        return inventory.GetCount(item, NormalizeQuality(item, quality)) >= amount;
    }

    // ขายของให้ร้านทันที: หักของ + ได้เงิน คืนค่า true ถ้าสำเร็จ
    public bool SellNow(ItemData item, int amount, DishQuality quality)
    {
        if (!CanSell(item) || amount <= 0 || inventory == null) return false;

        quality = NormalizeQuality(item, quality);
        if (!HasStock(item, amount, quality)) return false;
        if (!inventory.RemoveItem(item, amount, quality)) return false;

        int total = UnitSellPrice(item, quality) * amount;
        if (PlayerWallet.Instance != null) PlayerWallet.Instance.Add(total);
        else Debug.LogWarning("SalesLedger: No PlayerWallet in the scene. Money was not added.");

        ShopIncomeToday += total;
        if (Inventory.TracksQuality(item)) DishesSoldDirectToday += amount;

        Debug.Log($"SalesLedger: Sold {item.itemName} ({DishQualityUtil.Label(quality)}) x{amount} for {total}. Dishes counted today: {DishesCountedToday}.");
        OnChanged?.Invoke();
        return true;
    }

    // ---------- กล่องขายข้ามคืน ----------

    // ใส่ของลงกล่อง (ย้ายออกจากกระเป๋าทันที) คืนค่า true ถ้าสำเร็จ
    public bool PutInBox(ItemData item, int amount, DishQuality quality)
    {
        if (!CanSell(item) || amount <= 0 || inventory == null) return false;

        quality = NormalizeQuality(item, quality);
        if (!HasStock(item, amount, quality)) return false;
        if (!inventory.RemoveItem(item, amount, quality)) return false;

        int index = FindEntry(item, quality);
        if (index >= 0)
        {
            BoxEntry entry = box[index];
            entry.amount += amount;
            box[index] = entry;
        }
        else
        {
            box.Add(new BoxEntry { item = item, quality = quality, amount = amount });
        }

        Debug.Log($"SalesLedger: Put {item.itemName} ({DishQualityUtil.Label(quality)}) x{amount} in the shipping box. Box total {BoxTotal}.");
        OnChanged?.Invoke();
        return true;
    }

    // เอาของคืนจากกล่อง คืนจำนวนที่ย้ายกลับได้จริง (กระเป๋าเต็ม = ได้เท่าที่ใส่ได้ ที่เหลืออยู่ในกล่อง)
    public int TakeBack(int index, int amount)
    {
        if (inventory == null || index < 0 || index >= box.Count || amount <= 0) return 0;

        BoxEntry entry = box[index];
        int moved = Mathf.Min(amount, entry.amount, inventory.GetSpaceFor(entry.item));
        if (moved <= 0)
        {
            // AddItem แจ้งเตือนกระเป๋าเต็มให้เอง
            inventory.AddItem(entry.item, Mathf.Min(amount, entry.amount), entry.quality);
            return 0;
        }

        if (!inventory.AddItem(entry.item, moved, entry.quality)) return 0;

        entry.amount -= moved;
        if (entry.amount <= 0) box.RemoveAt(index);
        else box[index] = entry;

        Debug.Log($"SalesLedger: Took back {entry.item.itemName} x{moved} from the shipping box.");
        OnChanged?.Invoke();
        return moved;
    }

    // ---------- จบวัน ----------

    // ขายทุกอย่างในกล่อง + ให้ชื่อเสียงจากการขาย (DayEndManager เรียกตอนจบวัน ก่อนอ่านเงิน/ชื่อเสียง)
    public void SettleDay(out int income, out int reputation)
    {
        income = BoxTotal;
        int shippedDishes = DishesInBox;

        box.Clear();
        DishesShippedToday += shippedDishes;

        if (income > 0)
        {
            if (PlayerWallet.Instance != null) PlayerWallet.Instance.Add(income);
            else Debug.LogWarning("SalesLedger: No PlayerWallet in the scene. Overnight income was not added.");
        }

        // ให้เฉพาะส่วนที่ยังไม่เคยให้วันนี้ (กันซ้ำถ้าปิดยอดหลายครั้ง)
        int target = ReputationFor(DishesSoldDirectToday + DishesShippedToday);
        reputation = Mathf.Max(0, target - ReputationGrantedToday);
        ReputationGrantedToday += reputation;

        if (reputation > 0)
        {
            if (ShopProgress.Instance != null) ShopProgress.Instance.AddReputation(reputation);
            else Debug.LogWarning("SalesLedger: No ShopProgress in the scene. Sales reputation was not added.");
        }

        Debug.Log($"SalesLedger: Day settled. Overnight income {income}, sales reputation +{reputation} (dishes sold today {DishesSoldDirectToday + DishesShippedToday}).");
        OnChanged?.Invoke();
    }

    private void HandleDayStarted(int day)
    {
        ShopIncomeToday = 0;
        DishesSoldDirectToday = 0;
        DishesShippedToday = 0;
        ReputationGrantedToday = 0;

        if (box.Count > 0)
        {
            Debug.LogWarning($"SalesLedger: Day {day} started with {box.Count} entries still in the box. They will be sold tonight.");
        }

        OnChanged?.Invoke();
    }

    // ---------- ตัวช่วย ----------

    // ของที่ไม่ใช่จาน = Good เสมอ (ตามกติกาของ Inventory)
    private static DishQuality NormalizeQuality(ItemData item, DishQuality quality)
    {
        return Inventory.TracksQuality(item) ? quality : DishQuality.Good;
    }

    private int FindEntry(ItemData item, DishQuality quality)
    {
        for (int i = 0; i < box.Count; i++)
        {
            if (box[i].item == item && box[i].quality == quality) return i;
        }
        return -1;
    }

    // ---------- Debug ----------

    [ContextMenu("Debug: Print Status")]
    private void DebugPrintStatus()
    {
        Debug.Log($"SalesLedger: Shop income today {ShopIncomeToday}, dishes direct {DishesSoldDirectToday}, shipped {DishesShippedToday}, in box {DishesInBox}, " +
                  $"pending reputation {PendingReputation} (granted {ReputationGrantedToday}), box total {BoxTotal}.");
        foreach (BoxEntry entry in box)
        {
            string name = entry.item != null ? entry.item.itemName : "(missing)";
            Debug.Log($"  Box: {name} ({DishQualityUtil.Label(entry.quality)}) x{entry.amount} = {UnitSellPrice(entry.item, entry.quality) * entry.amount}");
        }
    }

    [ContextMenu("Debug: Sell Box Now")]
    private void DebugSellBoxNow()
    {
        SettleDay(out int income, out int reputation);
        NotificationUI.Post($"[Debug] Shipping box sold: {PlayerWallet.Format(income)}, reputation +{reputation}.");
    }
}
