using System;
using UnityEngine;
using UnityEngine.InputSystem;

// สถานะร้านในหนึ่งวัน
public enum ShopState
{
    BeforeOpening, // ยังไม่ถึงเวลาเปิดร้าน
    CanOpen,       // อยู่ในช่วงที่เปิดร้านได้ (เดินไปกด E ที่เคาน์เตอร์)
    Serving,       // กำลังเปิดร้านขายอยู่ (นาฬิกาหยุด)
    ClosedForDay   // ปิดร้านแล้ว หรือเปิดไม่ทัน วันนี้เปิดอีกไม่ได้
}

// ตัวจัดการร้าน: ช่วงเวลาเปิดร้าน ตัวจับเวลารอบขาย และจำนวนลูกค้าในร้าน
// ระบบร้านอื่นๆ (เมนู ลูกค้า ทำอาหาร) ให้ฟัง event จากที่นี่
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("Opening Window (Hour)")]
    [Tooltip("ชั่วโมงที่เริ่มเปิดร้านได้ และขึ้นแจ้งเตือน")]
    [SerializeField] private int openHour = 17;

    [Tooltip("ชั่วโมงสุดท้ายที่ยังเปิดร้านได้ (ถึงเวลานี้แล้วยังไม่เปิด = วันนี้ไม่เปิด)")]
    [SerializeField] private int lastOpenHour = 19;

    [Tooltip("ปิดร้านแล้วข้ามเวลาไปชั่วโมงนี้เสมอ")]
    [SerializeField] private int closeHour = 22;

    [Header("Service")]
    [Tooltip("ระยะเวลารอบขาย (วินาทีจริง) หมดแล้วลูกค้าใหม่จะไม่เข้า แต่ร้านยังเปิดจนลูกค้าในร้านหมด")]
    [SerializeField] private float serviceDurationSeconds = 180f;

    [Header("Messages")]
    [SerializeField] private string canOpenMessage = "The shop can open now! Go to the counter.";
    [SerializeField] private string missedMessage = "Too late to open the shop today.";
    [SerializeField] private string openedMessage = "The shop is open!";
    [SerializeField] private string lastCallMessage = "Last call! No more new customers.";
    [SerializeField] private string closedMessage = "The shop is closed for the night.";
    [SerializeField] private string tooEarlyMessage = "The shop opens at 17:00.";
    [SerializeField] private string alreadyClosedMessage = "The shop is closed for today.";

    [Header("Debug")]
    [Tooltip("กด F8 ระหว่างเปิดร้านเพื่อปิดร้านทันที (ใช้ทดสอบ)")]
    [SerializeField] private bool enableDebugKeys = true;

    // ---------- สถานะ ----------

    public ShopState State { get; private set; } = ShopState.BeforeOpening;
    public bool IsServing => State == ShopState.Serving;

    // วันนี้ได้เปิดร้านหรือไม่ (ใช้ในหน้าสรุปวัน)
    public bool OpenedToday { get; private set; }

    // หมดเวลารอบขายแล้วหรือยัง (หมดแล้ว = ไม่รับลูกค้าใหม่)
    public bool IsServiceTimeUp { get; private set; }

    // ร้านรับลูกค้าใหม่อยู่หรือไม่ (ระบบลูกค้าเช็กก่อนส่งลูกค้าเข้าร้าน)
    public bool IsAcceptingCustomers => IsServing && !IsServiceTimeUp;

    // จำนวนลูกค้าที่อยู่ในร้านตอนนี้
    public int ActiveCustomers { get; private set; }

    // เวลารอบขายที่เหลือ (วินาที)
    public float ServiceTimeRemaining { get; private set; }

    // ---------- Events ----------

    public event Action<ShopState> OnStateChanged;
    public event Action OnShopOpened;
    public event Action OnServiceTimeUp;
    public event Action OnShopClosed;

    // จำไว้ว่าร้านเป็นคนหยุดนาฬิกาเอง จะได้ไม่ไปเดินนาฬิกาที่ระบบอื่นหยุดไว้
    private bool pausedClockByThis = false;
    private bool isSubscribed = false;

    private void Awake()
    {
        // ป้องกันไม่ให้มีตัวจัดการร้านซ้ำสองตัวในฉาก
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("ShopManager: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        Subscribe();

        // ไม่รู้ว่านาฬิกาเริ่มวันแรกไปก่อนหรือยัง ให้ตรวจเวลาปัจจุบันเองหนึ่งครั้ง
        EvaluateTime();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        openHour = Mathf.Clamp(openHour, 0, 26);
        lastOpenHour = Mathf.Clamp(lastOpenHour, openHour + 1, 27);
        closeHour = Mathf.Clamp(closeHour, lastOpenHour, 27);
        serviceDurationSeconds = Mathf.Max(1f, serviceDurationSeconds);
    }

    private void Update()
    {
        HandleDebugInput();

        if (!IsServing)
        {
            return;
        }

        // นับเวลารอบขาย
        if (!IsServiceTimeUp)
        {
            ServiceTimeRemaining -= Time.deltaTime;
            if (ServiceTimeRemaining <= 0f)
            {
                ServiceTimeRemaining = 0f;
                IsServiceTimeUp = true;
                Debug.Log($"ShopManager: Service time is up. Waiting for {ActiveCustomers} customer(s) to leave.");
                NotificationUI.Post(lastCallMessage);
                OnServiceTimeUp?.Invoke();
            }
        }

        // หมดเวลาแล้ว และลูกค้าในร้านหมดแล้ว = ปิดร้าน
        if (IsServiceTimeUp && ActiveCustomers <= 0)
        {
            CloseShop();
        }
    }

    // ---------- Events ของนาฬิกา ----------

    private void Subscribe()
    {
        if (isSubscribed)
        {
            return;
        }

        if (GameClock.Instance == null)
        {
            Debug.LogWarning("ShopManager: GameClock.Instance not found. Shop schedule will not work.", this);
            return;
        }

        GameClock.Instance.OnDayStarted += HandleDayStarted;
        GameClock.Instance.OnTimeChanged += HandleTimeChanged;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed)
        {
            return;
        }

        if (GameClock.Instance != null)
        {
            GameClock.Instance.OnDayStarted -= HandleDayStarted;
            GameClock.Instance.OnTimeChanged -= HandleTimeChanged;
        }
        isSubscribed = false;
    }

    // วันใหม่: รีเซ็ตร้านกลับเป็นยังไม่เปิด
    private void HandleDayStarted(int day)
    {
        // ถ้าวันจบระหว่างเปิดร้าน (ไม่ควรเกิด แต่กันไว้) ให้ปล่อยนาฬิกาที่ร้านหยุดไว้
        ReleaseClock();

        OpenedToday = false;
        IsServiceTimeUp = false;
        ActiveCustomers = 0;
        ServiceTimeRemaining = 0f;
        SetState(ShopState.BeforeOpening);

        EvaluateTime();
    }

    private void HandleTimeChanged(int hour, int minute)
    {
        EvaluateTime();
    }

    // ตรวจเวลาปัจจุบันแล้วเปลี่ยนสถานะร้านตามช่วงเวลา
    private void EvaluateTime()
    {
        GameClock clock = GameClock.Instance;
        if (clock == null)
        {
            return;
        }

        int now = clock.TotalMinutes;
        int openMinutes = openHour * 60;
        int lastOpenMinutes = lastOpenHour * 60;

        switch (State)
        {
            case ShopState.BeforeOpening:
                if (now >= lastOpenMinutes)
                {
                    // ข้ามช่วงเปิดร้านไปทั้งช่วง (เช่น กด N ข้ามเวลา)
                    SetState(ShopState.ClosedForDay);
                    NotificationUI.Post(missedMessage);
                }
                else if (now >= openMinutes)
                {
                    SetState(ShopState.CanOpen);
                    NotificationUI.Post(canOpenMessage, 5f);
                }
                break;

            case ShopState.CanOpen:
                if (now >= lastOpenMinutes)
                {
                    SetState(ShopState.ClosedForDay);
                    NotificationUI.Post(missedMessage);
                }
                break;
        }
    }

    // ---------- เปิด / ปิดร้าน ----------

    // เรียกจากเคาน์เตอร์ คืนค่า true ถ้าเปิดร้านสำเร็จ
    public bool TryOpenShop()
    {
        switch (State)
        {
            case ShopState.BeforeOpening:
                NotificationUI.Post(tooEarlyMessage);
                return false;

            case ShopState.ClosedForDay:
                NotificationUI.Post(alreadyClosedMessage);
                return false;

            case ShopState.Serving:
                return false;
        }

        // State == CanOpen
        OpenedToday = true;
        IsServiceTimeUp = false;
        ActiveCustomers = 0;
        ServiceTimeRemaining = serviceDurationSeconds;

        // หยุดนาฬิการะหว่างขาย เฉพาะกรณีที่นาฬิกายังเดินอยู่
        GameClock clock = GameClock.Instance;
        if (clock != null && !clock.IsPaused)
        {
            clock.Pause();
            pausedClockByThis = true;
        }

        SetState(ShopState.Serving);
        Debug.Log($"ShopManager: Shop opened at {(clock != null ? clock.GetTimeString() : "?")}. Service time {serviceDurationSeconds}s.");
        NotificationUI.Post(openedMessage);
        OnShopOpened?.Invoke();
        return true;
    }

    // ปิดร้าน: ปล่อยนาฬิกา แล้วข้ามเวลาไปเวลาปิดร้าน
    private void CloseShop()
    {
        if (!IsServing)
        {
            return;
        }

        ActiveCustomers = 0;
        ServiceTimeRemaining = 0f;
        SetState(ShopState.ClosedForDay);

        ReleaseClock();

        GameClock clock = GameClock.Instance;
        if (clock != null && clock.TotalMinutes < closeHour * 60)
        {
            clock.AdvanceTo(closeHour * 60);
        }

        Debug.Log("ShopManager: Shop closed.");
        NotificationUI.Post(closedMessage);
        OnShopClosed?.Invoke();
    }

    // ปิดร้านทันทีโดยไม่รอลูกค้า (ใช้ทดสอบ หรือเหตุการณ์พิเศษในอนาคต)
    public void ForceCloseShop()
    {
        if (!IsServing)
        {
            return;
        }

        Debug.Log("ShopManager: Force closing the shop.");
        IsServiceTimeUp = true;
        CloseShop();
    }

    // ให้นาฬิกาเดินต่อ เฉพาะกรณีที่ร้านเป็นคนหยุดไว้
    private void ReleaseClock()
    {
        if (pausedClockByThis && GameClock.Instance != null)
        {
            GameClock.Instance.Resume();
        }
        pausedClockByThis = false;
    }

    private void SetState(ShopState newState)
    {
        if (State == newState)
        {
            return;
        }

        State = newState;
        Debug.Log($"ShopManager: State -> {State}");
        OnStateChanged?.Invoke(State);
    }

    // ---------- ลูกค้า (ให้ระบบลูกค้าในอนาคตเรียกใช้) ----------

    // ลูกค้าเข้าร้าน คืนค่า false ถ้าร้านไม่รับลูกค้าใหม่แล้ว
    public bool RegisterCustomer()
    {
        if (!IsAcceptingCustomers)
        {
            return false;
        }

        ActiveCustomers++;
        return true;
    }

    // ลูกค้าออกจากร้าน (กินเสร็จ หรือรอไม่ไหวแล้วเดินออก)
    public void UnregisterCustomer()
    {
        if (ActiveCustomers > 0)
        {
            ActiveCustomers--;
        }
    }

    // ---------- Debug ----------

    private void HandleDebugInput()
    {
        if (!enableDebugKeys)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        // F8 = ปิดร้านทันที
        if (keyboard.f8Key.wasPressedThisFrame)
        {
            ForceCloseShop();
        }
    }
}
