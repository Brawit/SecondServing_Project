using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// ตัวจัดการลูกค้า: ปล่อยลูกค้าเข้าร้านระหว่างเปิดร้าน ให้ลูกค้าเลือกเมนู และนับว่าใครสั่งอะไรไว้
// - ปล่อยลูกค้าเป็นระยะ เมื่อร้านยังรับลูกค้า มีที่นั่งว่าง และจานที่เหลือยังพอสำหรับลูกค้าที่จะเข้ามา
// - ลูกค้าสั่งได้เฉพาะเมนูประจำวันที่ยังเหลือ (หักจานที่ลูกค้าคนอื่นสั่งรอไว้แล้ว) หมดทั้งร้าน = ลูกค้าเดินออก
// - ลูกค้าที่ได้อาหารแล้ว ไม่นับเป็นลูกค้าที่ร้านต้องรอ: ร้านปิดได้ทันทีหลังเสิร์ฟคนสุดท้าย คนที่กินอยู่จะกินเสร็จแล้วเดินออกเอง
// - ร้านปิดตอนยังมีคนรออาหาร (เช่น กด F8) = คนนั้นเดินออก / เริ่มวันใหม่แล้วยังมีลูกค้าค้าง = เก็บออกทันที
// - [2.4b] เมนูนิยมรายวัน (PopularDishes): เมนูนิยมที่มีขายมีโอกาสถูกสั่งมากกว่า
//   เมนูนิยมที่ร้านไม่มีให้ (ไม่ได้ใส่ หรือหมดแล้ว) = ลูกค้าบางคนถามหา → ผู้เล่นกด E แนะนำเมนูวันนี้ → ลูกค้าเปลี่ยนจาน
//   ลูกค้าที่เปลี่ยนจาน กินเสร็จแล้วสุ่มอารมณ์ ชอบมาก / อร่อยดี / เฉยๆ (โอกาสชอบมากเพิ่มได้จากการอัปเกรดร้าน)
//   ไม่มีลูกค้าเดินออกเพราะเรื่องเมนูนิยม (ยกเว้นรอนานเกินไป)
// - [2.4b] ความพอใจต่อลูกค้า (Customer.Satisfaction) รวมเป็นสถิติรายวัน ให้ขั้น 2.6 ใช้คิดเงิน/ชื่อเสียง
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

    [Header("Popular Dishes")]
    [Tooltip("เมนูนิยมที่มีขาย มีโอกาสถูกสั่งมากกว่าเมนูปกติกี่เท่า")]
    [Min(1f)] [SerializeField] private float popularOrderWeight = 3f;

    [Tooltip("ถ้ามีเมนูนิยมที่ร้านไม่มีให้ (ไม่ได้ใส่ในเมนู หรือหมดแล้ว) โอกาสที่ลูกค้าแต่ละคนจะถามหา 0–1")]
    [Range(0f, 1f)] [SerializeField] private float askForPopularChance = 0.25f;

    [Header("Mood after a substitute dish (customer asked for a popular dish, then tried today's menu)")]
    [Tooltip("โอกาสตั้งต้นที่จะชอบมาก 0–1 (อัปเกรดร้านเพิ่มได้ผ่าน SetLovedChanceBonus) ส่วนที่เหลือแบ่งเท่ากันให้ อร่อยดี / เฉยๆ")]
    [Range(0f, 1f)] [SerializeField] private float baseLovedChance = 0.2f;

    [Header("Dish quality effect on mood (added to the chances above)")]
    [Tooltip("จาน Perfect: เพิ่มโอกาสชอบมาก")]
    [Range(-1f, 1f)] [SerializeField] private float perfectLovedBonus = 0.3f;

    [Tooltip("จาน Perfect: เพิ่ม/ลดโอกาสเฉยๆ (ติดลบ = เฉยๆ น้อยลง)")]
    [Range(-1f, 1f)] [SerializeField] private float perfectMehBonus = -0.2f;

    [Tooltip("จาน OK: เพิ่ม/ลดโอกาสชอบมาก (ติดลบ = ชอบมากน้อยลง)")]
    [Range(-1f, 1f)] [SerializeField] private float okLovedBonus = -0.1f;

    [Tooltip("จาน OK: เพิ่มโอกาสเฉยๆ")]
    [Range(-1f, 1f)] [SerializeField] private float okMehBonus = 0.3f;

    [Header("Satisfaction (step 2.6 will use these for money / reputation)")]
    [Tooltip("อารมณ์ อร่อยดี: จานปกติ หรือเปลี่ยนจานแล้วอร่อยดี")]
    [Min(0f)] [SerializeField] private float normalSatisfaction = 1f;

    [Tooltip("อารมณ์ ชอบมาก: ได้กินเมนูนิยม หรือเปลี่ยนจานแล้วชอบมาก")]
    [Min(0f)] [SerializeField] private float popularSatisfaction = 1.5f;

    [Tooltip("อารมณ์ เฉยๆ: เปลี่ยนจานแล้วรู้สึกเฉยๆ")]
    [FormerlySerializedAs("substituteSatisfaction")]
    [Min(0f)] [SerializeField] private float mehSatisfaction = 0.7f;

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

    // [2.4b] จำนวนลูกค้าที่ได้กินเมนูนิยม / ที่ถามหาเมนูนิยมที่ไม่มี / ที่เปลี่ยนจานแล้วได้กิน
    public int PopularServedToday { get; private set; }
    public int AskedForPopularToday { get; private set; }
    public int SubstituteServedToday { get; private set; }

    // [2.4b] อารมณ์หลังกินของลูกค้าทุกคนที่ได้กินวันนี้
    public int LovedToday { get; private set; }
    public int GoodToday { get; private set; }
    public int MehToday { get; private set; }

    // โอกาสชอบมากที่ได้เพิ่มจากการอัปเกรดร้าน
    private float lovedChanceBonus;

    // โอกาสชอบมากตอนนี้ (หลังเปลี่ยนจาน)
    public float LovedChance => Mathf.Clamp01(baseLovedChance + lovedChanceBonus);

    // [2.4b] ความพอใจรวมของลูกค้าที่ได้อาหารวันนี้ (ลูกค้าที่เดินออกโดยไม่ได้กิน = 0 ไม่ถูกบวก)
    public float SatisfactionTotalToday { get; private set; }

    // ความพอใจเฉลี่ยต่อลูกค้าที่เข้าร้านแล้วจบ (ได้กิน + เดินออกแบบไม่พอใจ) 0 = ยังไม่มีลูกค้า
    public float AverageSatisfactionToday
    {
        get
        {
            int count = ServedToday + LeftUnhappyToday;
            return count > 0 ? SatisfactionTotalToday / count : 0f;
        }
    }

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
        PopularServedToday = 0;
        AskedForPopularToday = 0;
        SubstituteServedToday = 0;
        LovedToday = 0;
        GoodToday = 0;
        MehToday = 0;
        SatisfactionTotalToday = 0f;
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

        Debug.Log($"CustomerManager: Shop closed. Served {ServedToday} ({PopularServedToday} popular), left unhappy {LeftUnhappyToday}, " +
                  $"(asked for popular {AskedForPopularToday}, substitutes {SubstituteServedToday}), " +
                  $"moods loved {LovedToday} / good {GoodToday} / meh {MehToday}, earned {EarnedToday}, " +
                  $"avg satisfaction {AverageSatisfactionToday:0.00}. " +
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

    // ลูกค้าเลือกเมนู (เรียกตอนลูกค้านั่งแล้วคิดเสร็จ)
    // 1) ถ้ามีเมนูนิยมที่ร้านไม่มีให้ (ไม่ได้อยู่ในเมนู หรือหมดแล้ว) = มีโอกาส askForPopularChance ที่ลูกค้าจะถามหา (Asking)
    //    ลูกค้าจะรอให้ผู้เล่นกด E แนะนำเมนูวันนี้ แล้วค่อยเรียก TryPickSubstitute
    // 2) ไม่ถามหา = สั่งจากจานที่ยังเหลือ เมนูนิยมที่มีขายมีน้ำหนัก popularOrderWeight เท่า
    // wanted = เมนูนิยมที่ถามหา (เฉพาะ Asking) / recipe = จานที่สั่งจริง
    // คืนค่า None = ไม่มีอะไรเหลือให้สั่ง
    public CustomerOrderKind TryPickOrder(Customer customer, out RecipeData recipe, out RecipeData wanted)
    {
        recipe = null;
        wanted = null;

        DailyMenu menu = DailyMenu.Instance;
        if (menu == null || !menu.IsActive)
        {
            MarkOrdered(customer);
            return CustomerOrderKind.None;
        }

        PopularDishes popular = PopularDishes.Instance;
        List<RecipeData> available = GetAvailableDishes(menu);

        if (available.Count == 0)
        {
            MarkOrdered(customer);
            return CustomerOrderKind.None;
        }

        // เมนูนิยมที่ร้านไม่มีให้ตอนนี้
        List<RecipeData> missingPopular = new List<RecipeData>();
        if (popular != null)
        {
            foreach (RecipeData dish in popular.Today)
            {
                if (dish != null && !available.Contains(dish))
                {
                    missingPopular.Add(dish);
                }
            }
        }

        if (missingPopular.Count > 0 && UnityEngine.Random.value < askForPopularChance)
        {
            // ยังไม่นับว่าสั่งแล้ว: จานที่เผื่อไว้ให้ลูกค้าคนนี้ยังจองอยู่ จนกว่าจะเปลี่ยนจานหรือเดินออก
            wanted = missingPopular[UnityEngine.Random.Range(0, missingPopular.Count)];
            AskedForPopularToday++;
            if (verboseLog) Debug.Log($"CustomerManager: {customer?.name} is asking for popular {wanted.recipeName} (not available).");
            return CustomerOrderKind.Asking;
        }

        MarkOrdered(customer);
        recipe = PickWeighted(available, popular);
        if (recipe == null)
        {
            return CustomerOrderKind.None;
        }

        AddPending(recipe);
        return popular != null && popular.IsPopular(recipe) ? CustomerOrderKind.Popular : CustomerOrderKind.Normal;
    }

    // ลูกค้าที่ถามหาเมนูนิยม ยอมเปลี่ยนมากินเมนูวันนี้ (เรียกตอนผู้เล่นกด E แนะนำ) คืนค่า None ถ้าไม่มีอะไรเหลือแล้ว
    public CustomerOrderKind TryPickSubstitute(Customer customer, out RecipeData recipe)
    {
        recipe = null;
        MarkOrdered(customer);

        DailyMenu menu = DailyMenu.Instance;
        if (menu == null || !menu.IsActive)
        {
            return CustomerOrderKind.None;
        }

        recipe = PickWeighted(GetAvailableDishes(menu), PopularDishes.Instance);
        if (recipe == null)
        {
            return CustomerOrderKind.None;
        }

        AddPending(recipe);
        return CustomerOrderKind.Substitute;
    }

    // จานในเมนูที่ยังเหลือ (หักจานที่ลูกค้าคนอื่นสั่งรอไว้แล้ว)
    private List<RecipeData> GetAvailableDishes(DailyMenu menu)
    {
        List<RecipeData> available = new List<RecipeData>();
        foreach (DailyMenu.MenuEntry entry in menu.Entries)
        {
            if (entry == null || entry.IsEmpty)
            {
                continue;
            }

            pendingOrders.TryGetValue(entry.Recipe, out int pending);
            if (entry.Remaining - pending > 0)
            {
                available.Add(entry.Recipe);
            }
        }
        return available;
    }

    // ลูกค้าคนนี้ตัดสินใจแล้ว (สั่ง หรือไม่มีอะไรให้สั่ง) เลิกเผื่อจานไว้ให้
    private void MarkOrdered(Customer customer)
    {
        if (customer != null && !customer.HasOrdered && notYetOrdered > 0)
        {
            notYetOrdered--;
        }
    }

    private void AddPending(RecipeData recipe)
    {
        pendingOrders.TryGetValue(recipe, out int current);
        pendingOrders[recipe] = current + 1;
    }

    // สุ่ม 1 จานตามน้ำหนัก (เมนูนิยม = popularOrderWeight, ปกติ = 1)
    private RecipeData PickWeighted(List<RecipeData> options, PopularDishes popular)
    {
        if (options == null || options.Count == 0)
        {
            return null;
        }

        float total = 0f;
        foreach (RecipeData option in options)
        {
            total += WeightOf(option, popular);
        }

        float roll = UnityEngine.Random.value * total;
        foreach (RecipeData option in options)
        {
            roll -= WeightOf(option, popular);
            if (roll < 0f)
            {
                return option;
            }
        }

        return options[options.Count - 1];
    }

    private float WeightOf(RecipeData recipe, PopularDishes popular)
    {
        return popular != null && popular.IsPopular(recipe) ? popularOrderWeight : 1f;
    }

    // ---------- อารมณ์และความพอใจ ----------

    // อารมณ์หลังกิน (ลูกค้าเรียกตอนได้อาหาร) คุณภาพจาน Good = ไม่เปลี่ยนโอกาส
    public CustomerMood DecideMood(CustomerOrderKind kind)
    {
        return DecideMood(kind, DishQuality.Good);
    }

    // [2.5] อารมณ์หลังกินตามแบบการสั่ง + คุณภาพจาน
    // โอกาสพื้นฐาน: เมนูนิยม = ชอบมาก 100% / จานปกติ = อร่อยดี 100% / เปลี่ยนจาน = ชอบมาก LovedChance ที่เหลือแบ่งเท่ากัน
    // แล้วบวกผลของคุณภาพ: Perfect = ชอบมากเพิ่ม เฉยๆ ลด / OK = ชอบมากลด เฉยๆ เพิ่ม
    public CustomerMood DecideMood(CustomerOrderKind kind, DishQuality quality)
    {
        float loved;
        float meh;
        switch (kind)
        {
            case CustomerOrderKind.Popular:
                loved = 1f;
                meh = 0f;
                break;

            case CustomerOrderKind.Substitute:
                loved = LovedChance;
                meh = (1f - loved) * 0.5f;
                break;

            default:
                loved = 0f;
                meh = 0f;
                break;
        }

        if (quality == DishQuality.Perfect)
        {
            loved += perfectLovedBonus;
            meh += perfectMehBonus;
        }
        else if (quality == DishQuality.Ok)
        {
            loved += okLovedBonus;
            meh += okMehBonus;
        }

        loved = Mathf.Clamp01(loved);
        meh = Mathf.Clamp(meh, 0f, 1f - loved);

        float roll = UnityEngine.Random.value;
        if (roll < loved) return CustomerMood.Loved;
        if (roll < loved + meh) return CustomerMood.Meh;
        return CustomerMood.Good;
    }

    // ความพอใจตามอารมณ์
    public float GetSatisfaction(CustomerMood mood)
    {
        switch (mood)
        {
            case CustomerMood.Loved: return popularSatisfaction;
            case CustomerMood.Good: return normalSatisfaction;
            case CustomerMood.Meh: return mehSatisfaction;
            default: return 0f;
        }
    }

    // ตั้งโอกาสชอบมากที่ได้เพิ่มจากการอัปเกรดร้าน (เช่น 0.1 = +10%)
    public void SetLovedChanceBonus(float bonus)
    {
        lovedChanceBonus = Mathf.Max(0f, bonus);
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
        SatisfactionTotalToday += customer.Satisfaction;
        if (customer.OrderKind == CustomerOrderKind.Popular)
        {
            PopularServedToday++;
        }
        else if (customer.OrderKind == CustomerOrderKind.Substitute)
        {
            SubstituteServedToday++;
        }

        switch (customer.Mood)
        {
            case CustomerMood.Loved: LovedToday++; break;
            case CustomerMood.Good: GoodToday++; break;
            case CustomerMood.Meh: MehToday++; break;
        }

        // ได้อาหารแล้ว: ร้านไม่ต้องรอคนนี้ (หมดเวลาแล้วและเสิร์ฟครบ = ร้านปิดได้เลย)
        ReleaseServiceSlot(customer);

        if (verboseLog) Debug.Log($"CustomerManager: Served {recipe.recipeName} ({DishQualityUtil.Label(customer.ServedQuality)}) to {customer.name} " +
                                  $"({customer.OrderKind}, mood {customer.Mood}, satisfaction {customer.Satisfaction:0.00}).");
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

    // จำนวนลูกค้าที่นั่งแล้วและรอผู้เล่นอยู่ = รออาหาร หรือถามหาเมนูนิยม (ใช้แสดงบนหน้าจอ)
    public int SeatedWaitingCount
    {
        get
        {
            int count = 0;
            foreach (Customer customer in customers)
            {
                if (customer != null && (customer.State == CustomerState.WaitingForFood || customer.State == CustomerState.Asking)) count++;
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
            if (customer == null)
            {
                continue;
            }

            // ลูกค้าที่ถามหาเมนูนิยม: แนะนำเมนูวันนี้ก่อน แล้วค่อยเสิร์ฟ
            if (customer.State == CustomerState.Asking)
            {
                customer.RecommendTodaysMenu();
            }

            if (customer.State == CustomerState.WaitingForFood)
            {
                customer.TryServe();
            }
        }
    }

    [ContextMenu("Debug: Print Status")]
    private void DebugPrint()
    {
        Debug.Log($"CustomerManager: {customers.Count} inside, {seats.Count} seats, {FreeServings} free servings, " +
                  $"served {ServedToday} ({PopularServedToday} popular), unhappy {LeftUnhappyToday} " +
                  $"(asked {AskedForPopularToday}, substitutes {SubstituteServedToday}), loved {LovedToday} / good {GoodToday} / meh {MehToday}, " +
                  $"loved chance {LovedChance:P0}, earned {EarnedToday}, avg satisfaction {AverageSatisfactionToday:0.00}.");
    }
}
