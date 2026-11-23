using System;
using System.Collections.Generic;
using UnityEngine;

// เมนูประจำวันของร้าน (ข้อมูล) ช่องเมนูเริ่มต้น 3 ช่อง อัปเกรดเพิ่มได้
// แต่ละช่อง = สูตร 1 สูตร + จำนวนจานที่จะขาย
// - จำนวนจานนับจาก "จานที่ทำไว้แล้วในกระเป๋า" + "จานที่วัตถุดิบในกระเป๋าพอทำ" (หักวัตถุดิบที่ช่องอื่นจองไว้แล้ว)
// - ของยังอยู่ในกระเป๋า (จองไว้เฉยๆ) [2.5 รอบ 2] วัตถุดิบหักตอนทำที่เตา (CookingSession) จานในกระเป๋าหักตอนเสิร์ฟ
// - [2.5 รอบ 2] เสิร์ฟได้ 2 ทาง: จานในมือ (TryTakeServingFromHand) หรือจานที่ทำไว้ในกระเป๋า (TryTakeServing)
//   ไม่มีจานเลย = เสิร์ฟไม่ได้ ต้องไปทำที่เตาก่อน (เลิกทางลัดหักวัตถุดิบตอนเสิร์ฟแล้ว)
// - ที่เตาทำเพิ่มได้ไม่เกิน Remaining − จานนั้นในกระเป๋า − จานในมือ (GetCookRoom)
// - ปิดร้านแล้ว ของที่ขายไม่หมดก็อยู่ในกระเป๋าเหมือนเดิม และเมนูนี้จะเป็นค่าเริ่มต้นของวันถัดไป
// ช่วงที่ "ล็อก" ของที่จองไว้ = ตั้งแต่เปิดร้านจนปิดร้าน ซึ่งกระเป๋าและ Hotbar ใช้ไม่ได้อยู่แล้ว
// วางบน GameObject ระบบ (ควรมีตัวเดียวในฉาก)
public class DailyMenu : MonoBehaviour
{
    public static DailyMenu Instance { get; private set; }

    // ข้อมูลของช่องเมนู 1 ช่อง
    public class MenuEntry
    {
        // สูตรในช่องนี้ (null = ช่องว่าง)
        public RecipeData Recipe;

        // จำนวนจานที่ตั้งไว้ (ใช้เป็นค่าเริ่มต้นของวันถัดไปด้วย)
        public int Planned;

        // จานที่ยังเหลือขายระหว่างเปิดร้าน
        public int Remaining;

        // จานที่ขายไปแล้ววันนี้
        public int Sold;

        public bool IsEmpty => Recipe == null;
    }

    [Header("References")]
    [Tooltip("กระเป๋าของผู้เล่น (คอมโพเนนต์ Inventory บนตัว Player)")]
    [SerializeField] private Inventory inventory;

    [Tooltip("สมุดสูตรของผู้เล่น (คอมโพเนนต์ RecipeBook บนตัว Player)")]
    [SerializeField] private RecipeBook recipeBook;

    [Header("Slots")]
    [Tooltip("จำนวนช่องเมนูตอนเริ่มเกม")]
    [Min(1)] [SerializeField] private int baseSlotCount = 3;

    [Tooltip("จำนวนจานสูงสุดต่อเมนู (กันตัวเลขยาวเกินไปใน UI)")]
    [Min(1)] [SerializeField] private int maxServingsPerDish = 99;

    [Header("Messages")]
    [Tooltip("ข้อความตอนเมนูเต็ม")]
    [SerializeField] private string menuFullMessage = "The menu is full. Remove a dish first.";

    [Tooltip("ข้อความตอนสูตรนี้อยู่ในเมนูแล้ว ({0} = ชื่อสูตร)")]
    [SerializeField] private string alreadyOnMenuFormat = "{0} is already on the menu.";

    [Tooltip("ข้อความตอนของไม่พอทำสูตรนี้ ({0} = ชื่อสูตร)")]
    [SerializeField] private string notEnoughFormat = "Not enough ingredients for {0}.";

    [Tooltip("ข้อความตอนกดเปิดร้านแต่ยังไม่มีจานในเมนู")]
    [SerializeField] private string emptyMenuMessage = "Add at least one dish to the menu first.";

    // ช่องเมนู (จำนวนช่อง = baseSlotCount + bonusSlots)
    private readonly List<MenuEntry> entries = new List<MenuEntry>();

    // ช่องที่ได้เพิ่มจากการอัปเกรด (ร้านค้า/ของสวมใส่ ขั้นถัดๆ ไป)
    private int bonusSlots;

    private bool isSubscribed;

    // ---------- Events ----------

    // ส่งสัญญาณเมื่อเมนูเปลี่ยน (ใส่/เอาออก/เปลี่ยนจำนวน/เริ่มขาย/ปิดร้าน)
    public event Action OnMenuChanged;

    // ส่งสัญญาณเมื่อเสิร์ฟไป 1 จาน (ส่งสูตร)
    public event Action<RecipeData> OnServingTaken;

    // ---------- ข้อมูลให้ระบบอื่นอ่าน ----------

    public Inventory Inventory => inventory;
    public RecipeBook RecipeBook => recipeBook;

    public int SlotCount => Mathf.Max(1, baseSlotCount + bonusSlots);
    public int MaxServingsPerDish => maxServingsPerDish;
    public IReadOnlyList<MenuEntry> Entries => entries;

    // กำลังขายอยู่ (เปิดร้านด้วยเมนูนี้แล้ว) ระหว่างนี้แก้เมนูไม่ได้
    public bool IsActive { get; private set; }

    // จำนวนจานที่ตั้งไว้ทั้งหมด
    public int TotalPlanned
    {
        get
        {
            int total = 0;
            foreach (MenuEntry entry in entries)
            {
                if (!entry.IsEmpty) total += entry.Planned;
            }
            return total;
        }
    }

    // จำนวนจานที่ยังเหลือขาย (ระหว่างเปิดร้าน)
    public int TotalRemaining
    {
        get
        {
            int total = 0;
            foreach (MenuEntry entry in entries)
            {
                if (!entry.IsEmpty) total += entry.Remaining;
            }
            return total;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("DailyMenu: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (inventory == null) Debug.LogWarning("DailyMenu: Inventory is not assigned.", this);
        if (recipeBook == null) Debug.LogWarning("DailyMenu: Recipe Book is not assigned.", this);

        EnsureSlotCount();
    }

    private void Start()
    {
        Subscribe();
    }

    private void OnEnable()
    {
        if (Instance == this && ShopManager.Instance != null)
        {
            Subscribe();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Subscribe()
    {
        if (isSubscribed || ShopManager.Instance == null)
        {
            return;
        }

        ShopManager.Instance.OnShopClosed += HandleShopClosed;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed)
        {
            return;
        }

        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.OnShopClosed -= HandleShopClosed;
        }
        isSubscribed = false;
    }

    // ---------- ช่องเมนู ----------

    // ปรับจำนวนช่องให้ตรงกับ SlotCount (ช่องที่เกินจะถูกตัดทิ้ง)
    private void EnsureSlotCount()
    {
        while (entries.Count < SlotCount)
        {
            entries.Add(new MenuEntry());
        }
        while (entries.Count > SlotCount)
        {
            entries.RemoveAt(entries.Count - 1);
        }
    }

    // เพิ่มช่องเมนูจากการอัปเกรด
    public void SetBonusSlots(int bonus)
    {
        bonusSlots = Mathf.Max(0, bonus);
        EnsureSlotCount();
        OnMenuChanged?.Invoke();
    }

    public MenuEntry GetEntry(int index)
    {
        return index >= 0 && index < entries.Count ? entries[index] : null;
    }

    // สูตรนี้อยู่ช่องไหน (-1 = ไม่อยู่ในเมนู)
    public int IndexOf(RecipeData recipe)
    {
        if (recipe == null)
        {
            return -1;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Recipe == recipe)
            {
                return i;
            }
        }
        return -1;
    }

    // ใส่สูตรลงช่องว่างช่องแรก ตั้งจำนวนเริ่มต้น 1 จาน คืนค่า true ถ้าสำเร็จ
    public bool TryAddRecipe(RecipeData recipe)
    {
        if (recipe == null || IsActive)
        {
            return false;
        }

        if (IndexOf(recipe) >= 0)
        {
            NotificationUI.Post(string.Format(alreadyOnMenuFormat, recipe.recipeName));
            return false;
        }

        int emptyIndex = -1;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].IsEmpty)
            {
                emptyIndex = i;
                break;
            }
        }

        if (emptyIndex < 0)
        {
            NotificationUI.Post(menuFullMessage);
            return false;
        }

        MenuEntry entry = entries[emptyIndex];
        entry.Recipe = recipe;
        entry.Planned = 0;
        entry.Remaining = 0;
        entry.Sold = 0;

        if (GetMaxServings(emptyIndex) <= 0)
        {
            // ยังไม่มีของพอ: ไม่ใส่
            entry.Recipe = null;
            NotificationUI.Post(string.Format(notEnoughFormat, recipe.recipeName));
            return false;
        }

        entry.Planned = 1;
        Debug.Log($"DailyMenu: Added {recipe.recipeName} to slot {emptyIndex + 1}.");
        OnMenuChanged?.Invoke();
        return true;
    }

    // เอาสูตรออกจากช่องนี้
    public void ClearEntry(int index)
    {
        MenuEntry entry = GetEntry(index);
        if (entry == null || entry.IsEmpty || IsActive)
        {
            return;
        }

        Debug.Log($"DailyMenu: Removed {entry.Recipe.recipeName} from slot {index + 1}.");
        entry.Recipe = null;
        entry.Planned = 0;
        entry.Remaining = 0;
        entry.Sold = 0;
        OnMenuChanged?.Invoke();
    }

    // ตั้งจำนวนจาน (ถูกจำกัดไม่เกินที่ทำได้) คืนค่าจำนวนที่ตั้งได้จริง
    public int SetPlanned(int index, int servings)
    {
        MenuEntry entry = GetEntry(index);
        if (entry == null || entry.IsEmpty || IsActive)
        {
            return 0;
        }

        int clamped = Mathf.Clamp(servings, 0, GetMaxServings(index));
        if (clamped != entry.Planned)
        {
            entry.Planned = clamped;
            OnMenuChanged?.Invoke();
        }
        return clamped;
    }

    // เพิ่ม/ลดจำนวนจาน
    public int ChangePlanned(int index, int delta)
    {
        MenuEntry entry = GetEntry(index);
        if (entry == null || entry.IsEmpty)
        {
            return 0;
        }
        return SetPlanned(index, entry.Planned + delta);
    }

    // ---------- คำนวณจำนวนจาน ----------

    // จำนวนจานของสูตรนี้ที่ทำไว้แล้วในกระเป๋า
    public int GetDishesInBag(RecipeData recipe)
    {
        if (recipe == null || recipe.dishItem == null || inventory == null)
        {
            return 0;
        }
        return inventory.GetCount(recipe.dishItem);
    }

    // ช่องนี้ตั้งได้สูงสุดกี่จาน (จานในกระเป๋า + ทำเพิ่มจากวัตถุดิบที่ช่องอื่นยังไม่ได้จอง)
    public int GetMaxServings(int index)
    {
        MenuEntry entry = GetEntry(index);
        if (entry == null || entry.IsEmpty || inventory == null)
        {
            return 0;
        }

        Dictionary<ItemData, int> reservedByOthers = new Dictionary<ItemData, int>();
        for (int i = 0; i < entries.Count; i++)
        {
            if (i != index)
            {
                AddReservedIngredients(entries[i], UsesRemaining(), reservedByOthers);
            }
        }

        return Mathf.Min(maxServingsPerDish, GetDishesInBag(entry.Recipe) + GetCookable(entry.Recipe, reservedByOthers));
    }

    // สูตรนี้ (ยังไม่อยู่ในเมนู) ใส่แล้วจะทำได้กี่จาน โดยหักของที่เมนูตอนนี้จองไว้ (ใช้แสดงในรายการสูตร)
    public int GetAvailableServings(RecipeData recipe)
    {
        if (recipe == null || inventory == null)
        {
            return 0;
        }

        int index = IndexOf(recipe);
        if (index >= 0)
        {
            return GetMaxServings(index);
        }

        Dictionary<ItemData, int> reserved = new Dictionary<ItemData, int>();
        foreach (MenuEntry entry in entries)
        {
            AddReservedIngredients(entry, UsesRemaining(), reserved);
        }

        return Mathf.Min(maxServingsPerDish, GetDishesInBag(recipe) + GetCookable(recipe, reserved));
    }

    // จำนวนวัตถุดิบนี้ที่เมนูจองไว้ตอนนี้ (ก่อนเปิดร้าน = ตามที่ตั้งไว้, ระหว่างเปิดร้าน = ตามที่เหลือขาย)
    public int GetReservedCount(ItemData item)
    {
        if (item == null)
        {
            return 0;
        }

        Dictionary<ItemData, int> reserved = new Dictionary<ItemData, int>();
        foreach (MenuEntry entry in entries)
        {
            AddReservedIngredients(entry, UsesRemaining(), reserved);
        }

        reserved.TryGetValue(item, out int count);

        // จานที่ทำไว้แล้วก็นับเป็นของที่จองด้วย
        foreach (MenuEntry entry in entries)
        {
            if (!entry.IsEmpty && entry.Recipe.dishItem == item)
            {
                count += Mathf.Min(UsesRemaining() ? entry.Remaining : entry.Planned, GetDishesInBag(entry.Recipe));
            }
        }

        return count;
    }

    // ระหว่างเปิดร้านใช้จำนวนที่เหลือขาย ก่อนเปิดใช้จำนวนที่ตั้งไว้
    private bool UsesRemaining()
    {
        return IsActive;
    }

    // ทำสูตรนี้เพิ่มจากวัตถุดิบได้กี่จาน โดยหักของที่จองไว้แล้ว
    private int GetCookable(RecipeData recipe, Dictionary<ItemData, int> reserved)
    {
        Dictionary<ItemData, int> required = recipe.GetRequiredCounts();
        if (required.Count == 0)
        {
            return 0;
        }

        int max = int.MaxValue;
        foreach (KeyValuePair<ItemData, int> pair in required)
        {
            reserved.TryGetValue(pair.Key, out int used);
            int available = Mathf.Max(0, inventory.GetCount(pair.Key) - used);
            max = Mathf.Min(max, available / pair.Value);
        }
        return max == int.MaxValue ? 0 : max;
    }

    // บวกวัตถุดิบที่ช่องนี้ต้องใช้ลงใน result (เฉพาะจานที่ต้องทำใหม่ ไม่นับจานที่ทำไว้แล้ว)
    private void AddReservedIngredients(MenuEntry entry, bool useRemaining, Dictionary<ItemData, int> result)
    {
        if (entry == null || entry.IsEmpty)
        {
            return;
        }

        int servings = useRemaining ? entry.Remaining : entry.Planned;

        // [2.5 รอบ 2] จานในมือทำเสร็จแล้ว (วัตถุดิบหักไปแล้ว) ไม่ต้องจองวัตถุดิบเพิ่ม
        int held = useRemaining ? HeldCount(entry.Recipe) : 0;
        int toCook = Mathf.Max(0, servings - GetDishesInBag(entry.Recipe) - held);
        if (toCook <= 0)
        {
            return;
        }

        foreach (KeyValuePair<ItemData, int> pair in entry.Recipe.GetRequiredCounts())
        {
            result.TryGetValue(pair.Key, out int current);
            result[pair.Key] = current + pair.Value * toCook;
        }
    }

    // ตรวจทุกช่องใหม่ตามของในกระเป๋าตอนนี้ (เรียกตอนเปิดหน้าต่างเมนู)
    // ช่องที่สูตรไม่รู้แล้วจะถูกล้าง / จำนวนที่เกินของที่มีจะถูกลดลง ไล่จากช่องบนลงล่าง
    public void Revalidate()
    {
        if (IsActive)
        {
            return;
        }

        EnsureSlotCount();

        // ล้างทุกช่องก่อน แล้วค่อยใส่กลับทีละช่อง เพื่อให้ช่องบนได้ของก่อน
        List<int> wanted = new List<int>(entries.Count);
        foreach (MenuEntry entry in entries)
        {
            wanted.Add(entry.Planned);
            if (!entry.IsEmpty && recipeBook != null && !recipeBook.Knows(entry.Recipe))
            {
                entry.Recipe = null;
            }
            entry.Planned = 0;
            entry.Remaining = 0;
            entry.Sold = 0;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (!entries[i].IsEmpty)
            {
                entries[i].Planned = Mathf.Clamp(wanted[i], 0, GetMaxServings(i));
            }
        }

        OnMenuChanged?.Invoke();
    }

    // ---------- เปิดร้าน / เสิร์ฟ / ปิดร้าน ----------

    // ยืนยันเมนูแล้วเปิดร้าน คืนค่า true ถ้าเปิดสำเร็จ (หน้าต่างเมนูเรียก หลังปิดตัวเองแล้ว)
    public bool TryConfirmAndOpenShop()
    {
        if (IsActive)
        {
            return false;
        }

        Revalidate();

        if (TotalPlanned <= 0)
        {
            NotificationUI.Post(emptyMenuMessage);
            return false;
        }

        if (ShopManager.Instance == null || !ShopManager.Instance.TryOpenShop())
        {
            return false;
        }

        foreach (MenuEntry entry in entries)
        {
            entry.Remaining = entry.IsEmpty ? 0 : entry.Planned;
            entry.Sold = 0;
        }

        IsActive = true;
        Debug.Log($"DailyMenu: Shop opened with {TotalPlanned} servings. {DescribeMenu()}");
        OnMenuChanged?.Invoke();
        return true;
    }

    // สูตรนี้ยังมีให้เสิร์ฟไหม (ระบบลูกค้าใช้เลือกว่าจะสั่งอะไร)
    public bool CanServe(RecipeData recipe)
    {
        MenuEntry entry = GetEntry(IndexOf(recipe));
        return IsActive && entry != null && entry.Remaining > 0;
    }

    // เสิร์ฟ 1 จาน: ใช้จานที่ทำไว้แล้วในกระเป๋าก่อน (คุณภาพดีที่สุด) ถ้าไม่มีหักวัตถุดิบ คืนค่า true ถ้าสำเร็จ
    public bool TryTakeServing(RecipeData recipe)
    {
        return TryTakeServing(recipe, null, out _);
    }

    // [2.5] เสิร์ฟ 1 จานจากกระเป๋าโดยเลือกคุณภาพ (null = ดีที่สุดที่มี) served = คุณภาพที่เสิร์ฟจริง
    // [2.5 รอบ 2] ไม่มีจานในกระเป๋า = เสิร์ฟไม่ได้ (ต้องทำที่เตาก่อน)
    public bool TryTakeServing(RecipeData recipe, DishQuality? quality, out DishQuality served)
    {
        served = DishQuality.Good;

        if (!CanServe(recipe) || inventory == null)
        {
            return false;
        }

        if (GetDishesInBag(recipe) <= 0)
        {
            Debug.Log($"DailyMenu: No {recipe.recipeName} in the bag. Cook it at the stove first.");
            return false;
        }

        // คุณภาพที่เลือกหมดแล้ว (เช่น ถูกเสิร์ฟไปก่อน) = ใช้คุณภาพดีที่สุดที่เหลือ
        DishQuality pick;
        if (quality.HasValue && inventory.GetCount(recipe.dishItem, quality.Value) > 0)
        {
            pick = quality.Value;
        }
        else
        {
            inventory.TryGetBestQuality(recipe.dishItem, out pick);
        }

        if (!inventory.RemoveItem(recipe.dishItem, 1, pick))
        {
            Debug.LogWarning($"DailyMenu: Could not remove {recipe.recipeName} ({DishQualityUtil.Label(pick)}) from the bag.");
            return false;
        }

        served = pick;
        CountServing(recipe, pick, "bag");
        return true;
    }

    // [2.5 รอบ 2] เสิร์ฟจานที่ถืออยู่ในมือ (จานไม่ได้อยู่ในกระเป๋า แค่นับยอด) Customer เรียกแล้วค่อยเคลียร์มือ
    public bool TryTakeServingFromHand(RecipeData recipe, DishQuality quality)
    {
        if (!CanServe(recipe))
        {
            return false;
        }

        CountServing(recipe, quality, "hand");
        return true;
    }

    // นับว่าขายไป 1 จาน
    private void CountServing(RecipeData recipe, DishQuality quality, string source)
    {
        MenuEntry entry = entries[IndexOf(recipe)];
        entry.Remaining--;
        entry.Sold++;
        Debug.Log($"DailyMenu: Served {recipe.recipeName} ({DishQualityUtil.Label(quality)}, from {source}, {entry.Remaining} left).");
        OnServingTaken?.Invoke(recipe);
        OnMenuChanged?.Invoke();
    }

    // ---------- [2.5 รอบ 2] ทำที่เตาระหว่างเปิดร้าน ----------

    // จานนี้อยู่ในมือผู้เล่นไหม (นับเป็น 1 จานที่ทำแล้ว)
    private int HeldCount(RecipeData recipe)
    {
        return CarriedDish.Instance != null && CarriedDish.Instance.IsHolding(recipe) ? 1 : 0;
    }

    // ระหว่างเปิดร้าน สูตรนี้ทำที่เตาได้อีกกี่จาน
    // = Remaining − จานนั้นในกระเป๋า − จานในมือ และวัตถุดิบต้องพอ (หักวัตถุดิบที่เมนูอื่นยังจองไว้)
    public int GetCookRoom(RecipeData recipe)
    {
        int index = IndexOf(recipe);
        MenuEntry entry = GetEntry(index);
        if (!IsActive || entry == null || entry.IsEmpty || inventory == null)
        {
            return 0;
        }

        int need = entry.Remaining - GetDishesInBag(recipe) - HeldCount(recipe);
        if (need <= 0)
        {
            return 0;
        }

        Dictionary<ItemData, int> reservedByOthers = new Dictionary<ItemData, int>();
        for (int i = 0; i < entries.Count; i++)
        {
            if (i != index)
            {
                AddReservedIngredients(entries[i], true, reservedByOthers);
            }
        }

        return Mathf.Min(need, GetCookable(recipe, reservedByOthers));
    }

    // ปิดร้าน: เลิกจอง ของที่เหลืออยู่ในกระเป๋าตามเดิม เมนูที่ตั้งไว้จำไว้ใช้วันถัดไป
    private void HandleShopClosed()
    {
        if (!IsActive)
        {
            return;
        }

        int sold = 0;
        int left = 0;
        foreach (MenuEntry entry in entries)
        {
            if (entry.IsEmpty) continue;
            sold += entry.Sold;
            left += entry.Remaining;
            entry.Remaining = 0;
        }

        IsActive = false;
        Debug.Log($"DailyMenu: Shop closed. Sold {sold}, unsold {left} (stays in the bag).");
        OnMenuChanged?.Invoke();
    }

    // จำนวนที่ขายได้วันนี้ (ใช้ในหน้าสรุปวันขั้น 2.7)
    public int GetTotalSold()
    {
        int sold = 0;
        foreach (MenuEntry entry in entries)
        {
            if (!entry.IsEmpty) sold += entry.Sold;
        }
        return sold;
    }

    // ---------- เซฟ / โหลด ----------

    // รหัสสูตรและจำนวนที่ตั้งไว้ของทุกช่อง (ช่องว่าง = "" และ 0)
    public void CaptureMenu(List<string> recipeIds, List<int> servings)
    {
        recipeIds.Clear();
        servings.Clear();

        foreach (MenuEntry entry in entries)
        {
            recipeIds.Add(entry.IsEmpty ? "" : entry.Recipe.recipeId);
            servings.Add(entry.IsEmpty ? 0 : entry.Planned);
        }
    }

    // ตั้งเมนูจากเซฟ (ยังไม่ตรวจของในกระเป๋า จะตรวจตอนเปิดหน้าต่างเมนู)
    public void RestoreMenu(IList<RecipeData> recipes, IList<int> servings)
    {
        EnsureSlotCount();

        for (int i = 0; i < entries.Count; i++)
        {
            RecipeData recipe = recipes != null && i < recipes.Count ? recipes[i] : null;

            // กันสูตรซ้ำ
            if (recipe != null && IndexOf(recipe) >= 0 && IndexOf(recipe) < i)
            {
                recipe = null;
            }

            entries[i].Recipe = recipe;
            entries[i].Planned = recipe != null && servings != null && i < servings.Count ? Mathf.Max(0, servings[i]) : 0;
            entries[i].Remaining = 0;
            entries[i].Sold = 0;
        }

        OnMenuChanged?.Invoke();
    }

    // ---------- Debug ----------

    private string DescribeMenu()
    {
        List<string> parts = new List<string>();
        foreach (MenuEntry entry in entries)
        {
            if (!entry.IsEmpty)
            {
                parts.Add($"{entry.Recipe.recipeName} x{(IsActive ? entry.Remaining : entry.Planned)}");
            }
        }
        return parts.Count > 0 ? string.Join(", ", parts) : "(empty)";
    }

    [ContextMenu("Debug: Print Menu")]
    private void DebugPrint()
    {
        Debug.Log($"DailyMenu: {(IsActive ? "Serving" : "Planned")} — {DescribeMenu()}");
    }

    // จำลองลูกค้า: เสิร์ฟเมนูละ 1 จาน (ใช้ระหว่างเปิดร้าน ก่อนมีระบบลูกค้า)
    [ContextMenu("Debug: Serve One Of Each")]
    private void DebugServeOneOfEach()
    {
        if (!IsActive)
        {
            Debug.LogWarning("DailyMenu: Open the shop first.");
            return;
        }

        foreach (MenuEntry entry in entries.ToArray())
        {
            if (!entry.IsEmpty && entry.Remaining > 0)
            {
                TryTakeServing(entry.Recipe);
            }
        }
    }

    [ContextMenu("Debug: Add Menu Slot")]
    private void DebugAddSlot()
    {
        SetBonusSlots(bonusSlots + 1);
        Debug.Log($"DailyMenu: Menu slots = {SlotCount}.");
    }
}
