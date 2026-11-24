using System;
using UnityEngine;

// สาเหตุที่วันจบ
public enum DayEndReason
{
    Slept,     // ไปนอนที่เตียงเอง
    PassedOut  // ไม่ได้นอนจนถึงเวลาสลบ
}

// ข้อมูลที่ส่งให้หน้าสรุปวัน
public class DaySummaryData
{
    public int Day;
    public DayEndReason Reason;
    public bool OpenedShop;

    // [2.6] ร้าน (เงินเข้ากระเป๋าไปแล้วตอนขาย หน้าสรุปแค่แสดง)
    public int DishesSold;
    public int CustomersLeft;
    public int ShopIncome;      // ราคา + ทิป
    public int Tips;

    // [2.6] ชื่อเสียง
    public int ReputationGained;
    public int Reputation;
    public int BarSize;
    public int ShopLevel;
    public bool BarFull;

    // [2.6b] รายได้ข้ามคืน (กล่องขาย) เข้ากระเป๋าตอนสรุป / ตอนนี้ 0
    public int OvernightIncome;

    // [2.6] เงินรวมหลังสรุป
    public int Money;
}

// ตัวจัดการการจบวัน: นอน/สลบ -> หน้าสรุป -> เซฟ (ผ่าน OnDayEnded) -> วันใหม่ 06:00 -> ย้ายผู้เล่นไปจุดตื่น
public class DayEndManager : MonoBehaviour
{
    public static DayEndManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("ตัวควบคุมการเดินของผู้เล่น (ลาก Player มาใส่)")]
    [SerializeField] private PlayerController playerController;

    [Tooltip("ตัววาร์ปของผู้เล่น (ลาก Player มาใส่)")]
    [SerializeField] private PlayerTeleporter playerTeleporter;

    [Tooltip("หน้าสรุปวัน (เว้นว่างได้ ถ้าว่างจะจบวันทันทีโดยไม่แสดงสรุป)")]
    [SerializeField] private DaySummaryUI summaryUI;

    [Header("Wake Up After Sleeping")]
    [Tooltip("จุดที่ผู้เล่นตื่นหลังนอน (วัตถุเปล่าข้างเตียง)")]
    [SerializeField] private Transform bedWakePoint;

    [Tooltip("ขอบกล้องของพื้นที่ที่มีเตียง (CameraBounds_Shop)")]
    [SerializeField] private Collider2D bedCameraBounds;

    [Header("Wake Up After Passing Out")]
    [Tooltip("จุดที่ผู้เล่นตื่นหลังสลบ (โรงพยาบาล ยังย้ายไปวางที่อื่นทีหลังได้)")]
    [SerializeField] private Transform hospitalWakePoint;

    [Tooltip("ขอบกล้องของพื้นที่โรงพยาบาล")]
    [SerializeField] private Collider2D hospitalCameraBounds;

    [Header("Game Start")]
    [Tooltip("ตอนเปิดเกม/โหลดเซฟ ให้ผู้เล่นเริ่มที่ข้างเตียง")]
    [SerializeField] private bool startAtBed = true;

    [Header("Messages")]
    [SerializeField] private string cannotSleepWhileServingMessage = "You can't sleep while the shop is open.";

    // กำลังอยู่ในขั้นตอนจบวัน (หน้าสรุปเปิดอยู่)
    public bool IsDayEnding { get; private set; }

    // ยิงตอนเริ่มขั้นตอนจบวัน และตอนวันใหม่เริ่มเสร็จแล้ว
    public event Action<DayEndReason> OnDayEndStarted;
    public event Action OnDayEndFinished;

    // สาเหตุของรอบจบวันปัจจุบัน
    private DayEndReason currentReason;

    // จำไว้ว่าระบบนี้เป็นคนหยุดนาฬิกาเอง
    private bool pausedClockByThis = false;
    private bool isSubscribed = false;

    private void Awake()
    {
        // ป้องกันไม่ให้มีตัวจัดการจบวันซ้ำสองตัวในฉาก
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("DayEndManager: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (playerController == null) Debug.LogWarning("DayEndManager: Player Controller is not assigned.", this);
        if (playerTeleporter == null) Debug.LogWarning("DayEndManager: Player Teleporter is not assigned.", this);
        if (summaryUI == null) Debug.LogWarning("DayEndManager: Summary UI is not assigned. The day will end without a summary.", this);
        if (bedWakePoint == null) Debug.LogWarning("DayEndManager: Bed Wake Point is not assigned.", this);
        if (hospitalWakePoint == null) Debug.LogWarning("DayEndManager: Hospital Wake Point is not assigned.", this);
    }

    private void Start()
    {
        Subscribe();

        // เริ่มเกมที่ข้างเตียงเสมอ (เหมือนตื่นนอนตอนเช้า)
        if (startAtBed)
        {
            MovePlayerTo(bedWakePoint, bedCameraBounds);
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (Instance == this)
        {
            Instance = null;
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
            Debug.LogWarning("DayEndManager: GameClock.Instance not found. Day end will not work.", this);
            return;
        }

        GameClock.Instance.OnPassedOut += HandlePassedOut;
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
            GameClock.Instance.OnPassedOut -= HandlePassedOut;
        }
        isSubscribed = false;
    }

    private void HandlePassedOut(int day)
    {
        // ถ้ากำลังเปิดร้านอยู่ (นาฬิกาควรหยุดอยู่ แต่กันไว้) ให้ปิดร้านก่อน
        if (ShopManager.Instance != null && ShopManager.Instance.IsServing)
        {
            ShopManager.Instance.ForceCloseShop();
        }

        StartDayEnd(DayEndReason.PassedOut);
    }

    // ---------- การนอน ----------

    // ตอนนี้นอนได้หรือไม่ ถ้าไม่ได้จะคืนเหตุผลมาด้วย
    public bool CanSleep(out string reason)
    {
        if (IsDayEnding)
        {
            reason = string.Empty;
            return false;
        }

        if (ShopManager.Instance != null && ShopManager.Instance.IsServing)
        {
            reason = cannotSleepWhileServingMessage;
            return false;
        }

        reason = string.Empty;
        return true;
    }

    // เรียกจากเตียง คืนค่า true ถ้าเริ่มนอนสำเร็จ
    public bool RequestSleep()
    {
        if (!CanSleep(out string reason))
        {
            NotificationUI.Post(reason);
            return false;
        }

        StartDayEnd(DayEndReason.Slept);
        return true;
    }

    // ---------- ขั้นตอนจบวัน ----------

    private void StartDayEnd(DayEndReason reason)
    {
        if (IsDayEnding)
        {
            return;
        }

        IsDayEnding = true;
        currentReason = reason;
        Debug.Log($"DayEndManager: Day end started ({reason}).");

        // ล็อกการเดิน
        if (playerController != null)
        {
            playerController.SetCanMove(false);
        }

        // หยุดนาฬิกาไว้ระหว่างดูสรุป (ตอนสลบนาฬิกาหยุดรออยู่แล้ว)
        GameClock clock = GameClock.Instance;
        if (clock != null && !clock.IsPaused)
        {
            clock.Pause();
            pausedClockByThis = true;
        }

        OnDayEndStarted?.Invoke(reason);

        DaySummaryData data = new DaySummaryData();
        data.Day = clock != null ? clock.Day : 0;
        data.Reason = reason;
        data.OpenedShop = ShopManager.Instance != null && ShopManager.Instance.OpenedToday;

        // [2.6] สถิติร้าน + ชื่อเสียง + เงิน (สถิติลูกค้ารีเซ็ตตอนเริ่มวันใหม่ จึงยังเป็นของวันนี้)
        CustomerManager customers = CustomerManager.Instance;
        if (customers != null)
        {
            data.DishesSold = customers.ServedToday;
            data.CustomersLeft = customers.LeftUnhappyToday;
            data.ShopIncome = customers.EarnedToday;
            data.Tips = customers.TipsToday;
            data.ReputationGained = customers.ReputationToday;
        }
        ShopProgress progress = ShopProgress.Instance;
        if (progress != null)
        {
            data.Reputation = progress.Reputation;
            data.BarSize = progress.BarSize;
            data.ShopLevel = progress.Level;
            data.BarFull = progress.IsBarFull && !progress.IsMaxLevel;
        }
        data.OvernightIncome = 0; // [2.6b] กล่องขายข้ามคืน: เพิ่มเงินเข้ากระเป๋าตรงนี้แล้วใส่ยอด
        data.Money = PlayerWallet.Instance != null ? PlayerWallet.Instance.Money : 0;

        if (summaryUI != null)
        {
            summaryUI.Show(data, FinishDayEnd);
        }
        else
        {
            FinishDayEnd();
        }
    }

    // ผู้เล่นปิดหน้าสรุปแล้ว: เซฟ -> วันใหม่ -> ย้ายผู้เล่น -> ปลดล็อก
    private void FinishDayEnd()
    {
        if (!IsDayEnding)
        {
            return;
        }

        GameClock clock = GameClock.Instance;

        // EndDay จะยิง OnDayEnded (SaveManager เซฟตรงนี้) แล้วเริ่มวันใหม่ 06:00
        if (clock != null)
        {
            clock.EndDay();
        }

        // ย้ายผู้เล่นไปจุดตื่น
        if (currentReason == DayEndReason.PassedOut)
        {
            MovePlayerTo(hospitalWakePoint, hospitalCameraBounds);
        }
        else
        {
            MovePlayerTo(bedWakePoint, bedCameraBounds);
        }

        // ให้นาฬิกาเดินต่อ เฉพาะกรณีที่ระบบนี้เป็นคนหยุดไว้
        if (pausedClockByThis && clock != null)
        {
            clock.Resume();
        }
        pausedClockByThis = false;

        // ปลดล็อกการเดิน
        if (playerController != null)
        {
            playerController.SetCanMove(true);
        }

        IsDayEnding = false;
        Debug.Log("DayEndManager: Day end finished.");
        OnDayEndFinished?.Invoke();
    }

    private void MovePlayerTo(Transform point, Collider2D cameraBounds)
    {
        if (point == null || playerTeleporter == null)
        {
            return;
        }

        playerTeleporter.TeleportTo(point.position, cameraBounds);
    }
}
