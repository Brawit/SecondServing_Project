using System;
using System.Collections.Generic;
using UnityEngine;

// ตัวจัดการลูกค้า: ปล่อยลูกค้าเข้าร้านระหว่างเปิดร้าน ให้ลูกค้าเลือกเมนู และนับว่าใครสั่งอะไรไว้
// - ปล่อยลูกค้าเป็นระยะ เมื่อร้านยังรับลูกค้า มีที่นั่งว่าง และจานที่เหลือยังพอสำหรับลูกค้าที่จะเข้ามา
// - ลูกค้าสั่งได้เฉพาะเมนูประจำวันที่ยังเหลือ (หักจานที่ลูกค้าคนอื่นสั่งรอไว้แล้ว) หมดทั้งร้าน = ลูกค้าเดินออก
// - ลูกค้าที่ได้อาหารแล้ว ไม่นับเป็นลูกค้าที่ร้านต้องรอ: ร้านปิดได้ทันทีหลังเสิร์ฟคนสุดท้าย คนที่กินอยู่จะกินเสร็จแล้วเดินออกเอง
// - ร้านปิดตอนยังมีคนรออาหาร (เช่น กด F8) = คนนั้นเดินออก / เริ่มวันใหม่แล้วยังมีลูกค้าค้าง = เก็บออกทันที
// ขั้น 2.4b จะเพิ่มเมนูนิยมรายวัน (ลูกค้ามีโอกาสสั่งเมนูนิยมมากกว่า)
// ควรมีตัวเดียวในฉาก
public class CustomerManager : MonoBehaviour
{
    public static CustomerManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("Prefab ลูกค้า")]
    [SerializeField] private Customer customerPrefab;

    [Tooltip("จุดที่ลูกค้าเกิด (ในร้าน หน้าประตู เช่น ShopEntryFront)")]
    [SerializeField] private Transform entryPoint;

    [Tooltip("จุดที่ลูกค้าเดินไปแล้วหายไป (เว้นว่างไว้ = จุดเกิด)")]
    [SerializeField] private Transform exitPoint;

    [Tooltip("วัตถุแม่ของลูกค้าที่สร้าง (เว้นว่างได้ จัดระเบียบ Hierarchy)")]
    [SerializeField] private Transform customerParent;

    [Header("Spawning (real seconds)")]
    [Tooltip("ลูกค้าคนแรกเข้ามาหลังเปิดร้านกี่วินาที")]
    [Min(0f)] [SerializeField] private float firstSpawnDelay = 3f;

    [Tooltip("ช่วงห่างระหว่างลูกค้า ต่ำสุด")]
    [Min(0.5f)] [SerializeField] private float minSpawnInterval = 8f;

    [Tooltip("ช่วงห่างระหว่างลูกค้า สูงสุด")]
    [Min(0.5f)] [SerializeField] private float maxSpawnInterval = 14f;

    [Tooltip("ลูกค้าในร้านพร้อมกันได้สูงสุด (0 = เท่าจำนวนที่นั่ง)")]
    [Min(0)] [SerializeField] private int maxConcurrentCustomers = 0;

    [Header("Debug")]
    [Tooltip("แสดง Log ทุกครั้งที่ลูกค้าเข้า/ออก")]
    [SerializeField] private bool verboseLog = true;

    // ที่นั่งทั้งหมดในฉาก (หาใหม่ตอนเปิดร้าน)
    private readonly List<Seat> seats = new List<Seat>();

    // ลูกค้าที่อยู่ในร้านตอนนี้
    private readonly List<Customer> customers = new List<Customer>();

    // จานที่ลูกค้าสั่งรอไว้แล้ว "สูตร -> จำนวน" (ยังไม่ได้เสิร์ฟ)
    private readonly Dictionary<RecipeData, int> pendingOrders = new Dictionary<RecipeData, int>();

    // ลูกค้าที่ร้านยังต้องรอ (ลงทะเบียนกับ ShopManager อยู่) = ยังไม่ได้อาหาร
    private readonly HashSet<Customer> waitingForService = new HashSet<Customer>();

    // ลูกค้าที่เข้ามาแล้วแต่ยังไม่ได้สั่ง (เผื่อจานไว้ให้)
    private int notYetOrdered;

    private float spawnTimer;
    private bool isSubscribed;
    private int spawnedCount;

    // ---------- สถิติของวัน (ใช้ในสรุปวัน 2.7 / เงินและชื่อเสียง 2.6) ----------

    public int ServedToday { get; private set; }
    public int LeftUnhappyToday { get; private set; }
    public int EarnedToday { get; private set; }

    // ---------- Events ----------

    public event Action<Customer> OnCustomerSpawned;
    public event Action<Customer, RecipeData> OnCustomerServed;
    public event Action<Customer, int> OnCustomerPaid;
    public event Action<Customer, CustomerLeaveReason> OnCustomerLeft;

    public IReadOnlyList<Customer> Customers => customers;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("CustomerManager: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (customerPrefab == null) Debug.LogWarning("CustomerManager: Customer Prefab is not assigned.", this);
        if (entryPoint == null) Debug.LogWarning("CustomerManager: Entry Point is not assigned.", this);
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

        ShopManager.Instance.OnShopOpened += HandleShopOpened;
        ShopManager.Instance.OnShopClosed += HandleShopClosed;
        if (GameClock.Instance != null)
        {
            GameClock.Instance.OnDayStarted += HandleDayStarted;
        }
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
            ShopManager.Instance.OnShopOpened -= HandleShopOpened;
            ShopManager.Instance.OnShopClosed -= HandleShopClosed;
        }
        if (GameClock.Instance != null)
        {
            GameClock.Instance.OnDayStarted -= HandleDayStarted;
        }
        isSubscribed = false;
    }

    private void Update()
    {
        ShopManager shop = ShopManager.Instance;
        if (shop == null || !shop.IsAcceptingCustomers)
        {
            return;
        }

        // ขายหมดทุกเมนูแล้ว: ไม่ต้องรอหมดเวลา หยุดรับลูกค้าแล้วปิดร้านเมื่อลูกค้าในร้านออกหมด
        DailyMenu menu = DailyMenu.Instance;
        if (menu != null && menu.IsActive && menu.TotalRemaining <= 0)
        {
            shop.EndServiceBySellOut();
            return;
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f)
        {
            return;
        }

        // ลองใหม่ทุก 1 วินาทีถ้ายังปล่อยไม่ได้ (เช่น ที่นั่งเต็ม)
        if (TrySpawnCustomer())
        {
            spawnTimer = UnityEngine.Random.Range(Mathf.Min(minSpawnInterval, maxSpawnInterval), Mathf.Max(minSpawnInterval, maxSpawnInterval));
        }
        else
        {
            spawnTimer = 1f;
        }
    }

    // ---------- เปิด / ปิดร้าน ----------

    private void HandleDayStarted(int day)
    {
        // ลูกค้าที่ยังค้างอยู่จากเมื่อวาน (เช่น นอนเร็วตอนลูกค้ายังเดินออกไม่หมด) เก็บออกทันที
        for (int i = customers.Count - 1; i >= 0; i--)
        {
            if (customers[i] != null)
            {
                customers[i].RemoveImmediately();
            }
        }
        customers.Clear();
        waitingForService.Clear();
        foreach (Seat seat in seats)
        {
            if (seat != null) seat.ForceRelease();
        }

        ServedToday = 0;
        LeftUnhappyToday = 0;
        EarnedToday = 0;
    }

    private void HandleShopOpened()
    {
        RefreshSeats();
        pendingOrders.Clear();
        notYetOrdered = 0;
        spawnTimer = firstSpawnDelay;

        // เฟอร์นิเจอร์อาจถูกย้าย ตรวจช่องเดินได้ใหม่
        if (GridPathfinder.Instance != null)
        {
            GridPathfinder.Instance.ClearCache();
        }
        else
        {
            Debug.LogWarning("CustomerManager: No GridPathfinder in the scene. Customers will walk in straight lines.", this);
        }

        if (seats.Count == 0)
        {
            Debug.LogWarning("CustomerManager: No Seat found in the scene. Customers can't come in. Place a Table prefab in the shop.", this);
        }

        Debug.Log($"CustomerManager: Shop opened. {seats.Count} seats.");
    }

    private void HandleShopClosed()
    {
        // ลูกค้าที่ยังรออาหารอยู่ (เช่น กด F8) ให้เดินออก / คนที่ได้อาหารแล้วกินต่อจนเสร็จแล้วเดินออกเอง
        int stillEating = 0;
        foreach (Customer customer in customers.ToArray())
        {
            if (customer == null)
            {
                continue;
            }

            if (customer.NeedsService)
            {
                customer.LeaveBecauseShopClosed();
            }
            else
            {
                stillEating++;
            }
        }

        // ร้านรีเซ็ตจำนวนลูกค้าเป็น 0 ตอนปิดแล้ว ไม่ต้องแจ้งออกซ้ำ
        waitingForService.Clear();
        pendingOrders.Clear();
        notYetOrdered = 0;

        Debug.Log($"CustomerManager: Shop closed. Served {ServedToday}, left unhappy {LeftUnhappyToday}, earned {EarnedToday}. " +
                  $"{stillEating} customer(s) still finishing their meal.");
    }

    // หาที่นั่งทุกตัวในฉาก
    public void RefreshSeats()
    {
        seats.Clear();
        seats.AddRange(FindObjectsByType<Seat>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
    }

    // ---------- ปล่อยลูกค้า ----------

    // จานที่ยังเหลือให้ลูกค้าใหม่ (หักจานที่สั่งรอไว้ และเผื่อลูกค้าที่ยังไม่ได้สั่ง)
    public int FreeServings
    {
        get
        {
            DailyMenu menu = DailyMenu.Instance;
            if (menu == null || !menu.IsActive)
            {
                return 0;
            }

            int pending = 0;
            foreach (int count in pendingOrders.Values)
            {
                pending += count;
            }

            return menu.TotalRemaining - pending - notYetOrdered;
        }
    }

    private int MaxCustomers => maxConcurrentCustomers > 0 ? maxConcurrentCustomers : seats.Count;

    // ปล่อยลูกค้า 1 คน คืนค่า false ถ้ายังปล่อยไม่ได้
    private bool TrySpawnCustomer()
    {
        if (customerPrefab == null || entryPoint == null)
        {
            return false;
        }

        if (customers.Count >= MaxCustomers || FreeServings <= 0)
        {
            return false;
        }

        Seat seat = PickFreeSeat();
        if (seat == null)
        {
            return false;
        }

        // ต้องลงทะเบียนกับร้านก่อน (ร้านปิดเมื่อหมดเวลาและไม่มีลูกค้าเหลือ)
        if (ShopManager.Instance == null || !ShopManager.Instance.RegisterCustomer())
        {
            return false;
        }

        Customer customer = Instantiate(customerPrefab, entryPoint.position, Quaternion.identity, customerParent);
        spawnedCount++;
        customer.name = $"Customer_{spawnedCount}";

        seat.TryReserve(customer);
        customers.Add(customer);
        waitingForService.Add(customer);
        notYetOrdered++;

        Vector3 exit = exitPoint != null ? exitPoint.position : entryPoint.position;
        customer.Init(this, seat, exit);

        if (verboseLog) Debug.Log($"CustomerManager: {customer.name} came in ({customers.Count} inside, {FreeServings} free servings).");
        OnCustomerSpawned?.Invoke(customer);
        return true;
    }

    private Seat PickFreeSeat()
    {
        List<Seat> free = new List<Seat>();
        foreach (Seat seat in seats)
        {
            if (seat != null && seat.IsFree && seat.isActiveAndEnabled)
            {
                free.Add(seat);
            }
        }

        return free.Count > 0 ? free[UnityEngine.Random.Range(0, free.Count)] : null;
    }

    // ---------- สั่งอาหาร ----------

    // ลูกค้าเลือกเมนู: เลือกจากเมนูประจำวันที่ยังเหลือ (หักจานที่คนอื่นสั่งรอไว้) คืนค่า false ถ้าไม่มีอะไรเหลือ
    public bool TryPickOrder(Customer customer, out RecipeData recipe)
    {
        recipe = null;

        if (customer != null && !customer.HasOrdered && notYetOrdered > 0)
        {
            notYetOrdered--;
        }

        DailyMenu menu = DailyMenu.Instance;
        if (menu == null || !menu.IsActive)
        {
            return false;
        }

        List<RecipeData> options = new List<RecipeData>();
        foreach (DailyMenu.MenuEntry entry in menu.Entries)
        {
            if (entry == null || entry.IsEmpty)
            {
                continue;
            }

            pendingOrders.TryGetValue(entry.Recipe, out int pending);
            if (entry.Remaining - pending > 0)
            {
                options.Add(entry.Recipe);
            }
        }

        if (options.Count == 0)
        {
            return false;
        }

        // ตอนนี้สุ่มเท่าๆ กัน (ขั้น 2.4b จะให้น้ำหนักเมนูนิยม)
        recipe = options[UnityEngine.Random.Range(0, options.Count)];

        pendingOrders.TryGetValue(recipe, out int current);
        pendingOrders[recipe] = current + 1;
        return true;
    }

    // ลูกค้าไม่ได้รอจานนี้แล้ว (เสิร์ฟแล้ว หรือเดินออก)
    public void ReleasePending(RecipeData recipe)
    {
        if (recipe == null || !pendingOrders.TryGetValue(recipe, out int current))
        {
            return;
        }

        if (current <= 1) pendingOrders.Remove(recipe);
        else pendingOrders[recipe] = current - 1;
    }

    // ---------- รายงานจากลูกค้า ----------

    public void ReportServed(Customer customer, RecipeData recipe)
    {
        ServedToday++;

        // ได้อาหารแล้ว: ร้านไม่ต้องรอคนนี้ (หมดเวลาแล้วและเสิร์ฟครบ = ร้านปิดได้เลย)
        ReleaseServiceSlot(customer);

        if (verboseLog) Debug.Log($"CustomerManager: Served {recipe.recipeName} to {customer.name}.");
        OnCustomerServed?.Invoke(customer, recipe);
    }

    public void ReportPaid(Customer customer, int amount)
    {
        EarnedToday += Mathf.Max(0, amount);
        if (verboseLog) Debug.Log($"CustomerManager: {customer.name} paid {amount} (money system comes in step 2.6).");
        OnCustomerPaid?.Invoke(customer, amount);
    }

    public void ReportLeft(Customer customer, CustomerLeaveReason reason)
    {
        if (!customers.Remove(customer))
        {
            return;
        }

        if (!customer.HasOrdered && notYetOrdered > 0)
        {
            notYetOrdered--;
        }

        if (reason == CustomerLeaveReason.NoFoodLeft || reason == CustomerLeaveReason.OutOfPatience)
        {
            LeftUnhappyToday++;
        }

        ReleaseServiceSlot(customer);

        if (verboseLog) Debug.Log($"CustomerManager: {customer.name} left ({reason}). {customers.Count} inside.");
        OnCustomerLeft?.Invoke(customer, reason);
    }

    // แจ้งร้านว่าไม่ต้องรอลูกค้าคนนี้แล้ว (เรียกได้ครั้งเดียวต่อคน)
    private void ReleaseServiceSlot(Customer customer)
    {
        if (!waitingForService.Remove(customer))
        {
            return;
        }

        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.UnregisterCustomer();
        }
    }

    // จำนวนลูกค้าที่ร้านยังต้องรอ (ยังไม่ได้อาหาร รวมคนที่กำลังเดินไปนั่ง) ใช้ตัดสินว่าปิดร้านได้หรือยัง
    public int WaitingCount => waitingForService.Count;

    // จำนวนลูกค้าที่นั่งแล้วและสั่งแล้ว รออาหารอยู่ = เสิร์ฟได้ตอนนี้ (ใช้แสดงบนหน้าจอ)
    public int SeatedWaitingCount
    {
        get
        {
            int count = 0;
            foreach (Customer customer in customers)
            {
                if (customer != null && customer.State == CustomerState.WaitingForFood) count++;
            }
            return count;
        }
    }

    // ---------- Debug (คลิกขวาที่หัวคอมโพเนนต์ระหว่าง Play) ----------

    [ContextMenu("Debug: Spawn Customer Now")]
    private void DebugSpawnNow()
    {
        if (ShopManager.Instance == null || !ShopManager.Instance.IsAcceptingCustomers)
        {
            Debug.LogWarning("CustomerManager: Open the shop first.");
            return;
        }

        if (!TrySpawnCustomer())
        {
            Debug.LogWarning($"CustomerManager: Can't spawn ({customers.Count}/{MaxCustomers} inside, {FreeServings} free servings, {seats.Count} seats).");
        }
    }

    [ContextMenu("Debug: Serve All Waiting")]
    private void DebugServeAll()
    {
        foreach (Customer customer in customers.ToArray())
        {
            if (customer != null && customer.State == CustomerState.WaitingForFood)
            {
                customer.TryServe();
            }
        }
    }

    [ContextMenu("Debug: Print Status")]
    private void DebugPrint()
    {
        Debug.Log($"CustomerManager: {customers.Count} inside, {seats.Count} seats, {FreeServings} free servings, " +
                  $"served {ServedToday}, unhappy {LeftUnhappyToday}, earned {EarnedToday}.");
    }
}
