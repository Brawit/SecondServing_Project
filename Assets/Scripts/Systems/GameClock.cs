using System;
using UnityEngine;
using UnityEngine.InputSystem;

// ช่วงเวลาของวัน ใช้ตัดสินว่าตอนนี้ควรสำรวจหรือเปิดร้าน
public enum DayPeriod
{
    Morning,
    Afternoon,
    Evening,
    Night
}

// นาฬิกาในเกม: นับเวลา แบ่งช่วงเช้า/บ่าย/เย็น/ดึก และแจ้ง event ให้ระบบอื่นรู้
// วันจะไม่จบเอง ต้องมีคนเรียก EndDay() (เช่น ตอนนอน) ถ้าเลยเวลาสลบ จะยิง OnPassedOut แล้วหยุดรอ
public class GameClock : MonoBehaviour
{
    // ให้สคริปต์อื่นเข้าถึงนาฬิกาได้ผ่าน GameClock.Instance
    public static GameClock Instance { get; private set; }

    [Header("Time Settings")]
    [Tooltip("ชั่วโมงที่วันเริ่มต้น (0-23)")]
    [SerializeField] private int startHour = 6;

    [Tooltip("ชั่วโมงที่ผู้เล่นสลบถ้ายังไม่นอน นับต่อจาก 24 (27 = ตี 3 ของคืนนั้น)")]
    [SerializeField] private int passOutHour = 27;

    [Tooltip("เวลาในเกมที่เดินต่อหนึ่งครั้ง (นาที)")]
    [SerializeField] private int minutesPerTick = 10;

    [Tooltip("จำนวนวินาทีจริงต่อหนึ่งครั้งที่เวลาเดิน")]
    [SerializeField] private float realSecondsPerTick = 7f;

    [Header("Period Boundaries (Hour)")]
    [SerializeField] private int afternoonStartHour = 12;
    [SerializeField] private int eveningStartHour = 17;

    [Tooltip("ชั่วโมงที่เริ่มช่วงดึก (24 = เที่ยงคืน)")]
    [SerializeField] private int nightStartHour = 24;

    [Header("Debug")]
    [Tooltip("พิมพ์เวลาลง Console ทุกครั้งที่เวลาเดิน")]
    [SerializeField] private bool logEveryTick = false;

    [Tooltip("ตัวคูณความเร็วตอนกด F ค้าง")]
    [SerializeField] private float fastForwardMultiplier = 10f;

    // ---------- สถานะปัจจุบัน ----------

    // วันที่ในเกม เริ่มที่ 1
    public int Day { get; private set; } = 1;

    // เวลาเก็บเป็น "นาทีนับจากเที่ยงคืนของวันนั้น" เช่น 06:30 = 390, ตี 1 ของคืนนั้น = 1500
    public int TotalMinutes { get; private set; }

    // ชั่วโมงแบบนับต่อ (หลังเที่ยงคืนจะเป็น 24, 25, 26) ใช้คำนวณ
    public int Hour => TotalMinutes / 60;
    public int Minute => TotalMinutes % 60;

    // ชั่วโมงสำหรับแสดงผล (0-23) เช่น 25 -> 1
    public int DisplayHour => Hour % 24;

    public DayPeriod CurrentPeriod { get; private set; }
    public bool IsPaused { get; private set; }

    // true = ถึงเวลาสลบแล้ว นาฬิกาหยุดเดินและรอให้มีคนเรียก EndDay()
    public bool IsWaitingForDayEnd { get; private set; }

    // ---------- Events ----------

    // ส่ง (ชั่วโมงแบบนับต่อ, นาที) ทุกครั้งที่เวลาเปลี่ยน ถ้าจะแสดงผลให้ใช้ hour % 24
    public event Action<int, int> OnTimeChanged;

    // ส่งช่วงเวลาใหม่เมื่อเปลี่ยนช่วง เช่น บ่าย -> เย็น
    public event Action<DayPeriod> OnPeriodChanged;

    // ส่งหมายเลขวันเมื่อเริ่มวันใหม่
    public event Action<int> OnDayStarted;

    // ส่งหมายเลขวันที่เพิ่งจบ (ยิงก่อนเริ่มวันใหม่ ระบบเซฟฟังตัวนี้)
    public event Action<int> OnDayEnded;

    // ส่งหมายเลขวันเมื่อถึงเวลาสลบ (ผู้เล่นไม่ได้นอน)
    public event Action<int> OnPassedOut;

    // ตัวจับเวลาสะสมวินาทีจริงจนครบหนึ่ง tick
    private float tickTimer = 0f;

    // true = นาฬิกาเริ่มวันแรกไปแล้ว (หลัง Start)
    private bool hasStarted = false;

    private void Awake()
    {
        // ป้องกันไม่ให้มีนาฬิกาซ้ำสองตัวในฉาก
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("GameClock: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // เริ่มวันแรกใน Start เพื่อให้สคริปต์อื่นที่สมัคร event ใน OnEnable/Awake ได้รับ event ด้วย
        // (ถ้ามีการโหลดเซฟ SaveManager จะตั้ง Day ไว้ก่อนแล้วผ่าน SetStartDay)
        hasStarted = true;
        BeginDay(Day);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ตรวจค่าที่กรอกใน Inspector ไม่ให้ผิดจนเกมพัง
    private void OnValidate()
    {
        startHour = Mathf.Clamp(startHour, 0, 23);
        passOutHour = Mathf.Clamp(passOutHour, startHour + 1, startHour + 24);
        minutesPerTick = Mathf.Max(1, minutesPerTick);
        realSecondsPerTick = Mathf.Max(0.01f, realSecondsPerTick);
        fastForwardMultiplier = Mathf.Max(1f, fastForwardMultiplier);
    }

    private void Update()
    {
        HandleDebugInput();

        // หยุดอยู่ หรือรอจบวันอยู่ เวลาไม่เดิน
        if (IsPaused || IsWaitingForDayEnd)
        {
            return;
        }

        // ถ้ากด F ค้างไว้ เวลาจะเดินเร็วขึ้น
        float speed = IsFastForwardHeld() ? fastForwardMultiplier : 1f;
        tickTimer += Time.deltaTime * speed;

        // ใช้ while เผื่อเฟรมช้ามากหรือเร่งเวลาแรง จะได้ไม่ตก tick
        while (tickTimer >= realSecondsPerTick)
        {
            tickTimer -= realSecondsPerTick;
            AdvanceMinutes(minutesPerTick);

            // ถ้าเพิ่งถึงเวลาสลบ หรือมีระบบอื่นสั่งหยุด/จบวันระหว่าง tick ให้หยุดลูป
            if (IsWaitingForDayEnd || IsPaused)
            {
                tickTimer = 0f;
                break;
            }
        }
    }

    // ---------- การเดินของเวลา ----------

    // เพิ่มเวลาตามจำนวนนาทีที่กำหนด
    private void AdvanceMinutes(int minutes)
    {
        int passOutMinutes = passOutHour * 60;
        TotalMinutes = Mathf.Min(TotalMinutes + minutes, passOutMinutes);

        RaiseTimeChanged();
        UpdatePeriod(false);

        // ถึงเวลาสลบแล้ว
        if (TotalMinutes >= passOutMinutes)
        {
            TriggerPassOut();
        }
    }

    // เริ่มวันใหม่: ตั้งเวลาเป็นเวลาเริ่มต้นและแจ้ง event
    private void BeginDay(int day)
    {
        Day = day;
        TotalMinutes = startHour * 60;
        tickTimer = 0f;
        IsWaitingForDayEnd = false;

        Debug.Log($"Day {Day} started.");
        OnDayStarted?.Invoke(Day);

        RaiseTimeChanged();
        UpdatePeriod(true);
    }

    // ถึงเวลาสลบ: หยุดเวลาแล้วแจ้งระบบจบวัน
    private void TriggerPassOut()
    {
        if (IsWaitingForDayEnd)
        {
            return;
        }

        IsWaitingForDayEnd = true;
        tickTimer = 0f;
        Debug.Log($"Day {Day}: Player passed out ({GetTimeString()}).");

        // ถ้าไม่มีระบบไหนรับเรื่องสลบเลย ให้จบวันเองทันที เกมจะได้ไม่ค้าง
        if (OnPassedOut == null)
        {
            Debug.LogWarning("GameClock: No listener for OnPassedOut. Ending the day automatically.");
            EndDay();
            return;
        }

        OnPassedOut.Invoke(Day);
    }

    // คำนวณช่วงเวลาจากชั่วโมงแบบนับต่อ
    private DayPeriod CalculatePeriod(int hour)
    {
        if (hour >= nightStartHour)
        {
            return DayPeriod.Night;
        }
        if (hour >= eveningStartHour)
        {
            return DayPeriod.Evening;
        }
        if (hour >= afternoonStartHour)
        {
            return DayPeriod.Afternoon;
        }
        return DayPeriod.Morning;
    }

    // อัปเดตช่วงเวลา ถ้าเปลี่ยนจะแจ้ง event (forceNotify = แจ้งแม้ช่วงไม่เปลี่ยน ใช้ตอนเริ่มวัน)
    private void UpdatePeriod(bool forceNotify)
    {
        DayPeriod newPeriod = CalculatePeriod(Hour);

        if (newPeriod != CurrentPeriod || forceNotify)
        {
            CurrentPeriod = newPeriod;
            Debug.Log($"Period changed: {CurrentPeriod} ({GetTimeString()})");
            OnPeriodChanged?.Invoke(CurrentPeriod);
        }
    }

    private void RaiseTimeChanged()
    {
        if (logEveryTick)
        {
            Debug.Log(GetTimeString());
        }
        OnTimeChanged?.Invoke(Hour, Minute);
    }

    // ---------- เมธอดสาธารณะให้ระบบอื่นใช้ ----------

    // หยุดเวลา (เช่น ตอนเปิดเมนูหรือคุยกับ NPC)
    public void Pause()
    {
        IsPaused = true;
        Debug.Log("Clock paused.");
    }

    // ให้เวลาเดินต่อ
    public void Resume()
    {
        IsPaused = false;
        Debug.Log("Clock resumed.");
    }

    // จบวันปัจจุบันแล้วเริ่มวันถัดไปที่เวลาเริ่มต้น (เรียกจากระบบนอน/สลบ)
    // ลำดับ: OnDayEnded (ระบบเซฟทำงานตรงนี้) -> OnDayStarted ของวันใหม่
    public void EndDay()
    {
        Debug.Log($"Day {Day} ended.");
        OnDayEnded?.Invoke(Day);

        BeginDay(Day + 1);
    }

    // ข้ามเวลาไปข้างหน้าภายในวันเดียวกัน (เช่น ปิดร้านแล้วข้ามไป 22:00)
    // ยิง OnPeriodChanged เฉพาะตอนช่วงเปลี่ยนจริง ไม่ยิงซ้ำ
    public void AdvanceTo(int targetTotalMinutes)
    {
        int passOutMinutes = passOutHour * 60;
        targetTotalMinutes = Mathf.Min(targetTotalMinutes, passOutMinutes);

        if (targetTotalMinutes <= TotalMinutes)
        {
            Debug.LogWarning($"GameClock: AdvanceTo({targetTotalMinutes}) is not ahead of current time ({TotalMinutes}). Ignored.");
            return;
        }

        TotalMinutes = targetTotalMinutes;
        tickTimer = 0f;

        RaiseTimeChanged();
        UpdatePeriod(false);

        if (TotalMinutes >= passOutMinutes)
        {
            TriggerPassOut();
        }
    }

    // ข้ามไปช่วงถัดไป ถ้าเป็นช่วงดึกแล้วจะข้ามไปเวลาสลบ
    public void SkipToNextPeriod()
    {
        if (IsWaitingForDayEnd)
        {
            return;
        }

        switch (CurrentPeriod)
        {
            case DayPeriod.Morning:
                AdvanceTo(afternoonStartHour * 60);
                break;
            case DayPeriod.Afternoon:
                AdvanceTo(eveningStartHour * 60);
                break;
            case DayPeriod.Evening:
                AdvanceTo(nightStartHour * 60);
                break;
            case DayPeriod.Night:
                AdvanceTo(passOutHour * 60);
                break;
        }
    }

    // ตั้งวันที่จะเริ่มเล่น ใช้ตอนโหลดเซฟเท่านั้น ต้องเรียกก่อน Start ของนาฬิกา (ไม่ยิง event ใดๆ)
    public void SetStartDay(int day)
    {
        if (hasStarted)
        {
            Debug.LogWarning("GameClock: SetStartDay was called after the clock started. Ignored.");
            return;
        }

        Day = Mathf.Max(1, day);
    }

    // ตั้งเวลาตรงๆ (เตรียมไว้ให้ระบบโหลดเซฟ)
    public void SetTime(int day, int totalMinutes)
    {
        Day = Mathf.Max(1, day);
        TotalMinutes = Mathf.Clamp(totalMinutes, startHour * 60, passOutHour * 60 - 1);
        tickTimer = 0f;
        IsWaitingForDayEnd = false;

        RaiseTimeChanged();
        UpdatePeriod(true);
    }

    // คืนข้อความเวลา เช่น "Day 1 - 06:00 (Morning)"
    public string GetTimeString()
    {
        return $"Day {Day} - {DisplayHour:00}:{Minute:00} ({CurrentPeriod})";
    }

    // ---------- Debug Input ----------

    private void HandleDebugInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        // T = พิมพ์เวลาปัจจุบัน
        if (keyboard.tKey.wasPressedThisFrame)
        {
            Debug.Log($"Current time: {GetTimeString()}{(IsPaused ? " [PAUSED]" : "")}{(IsWaitingForDayEnd ? " [WAITING FOR DAY END]" : "")}");
        }

        // P = สลับหยุด/เดิน
        if (keyboard.pKey.wasPressedThisFrame)
        {
            if (IsPaused) Resume();
            else Pause();
        }

        // N = ข้ามไปช่วงถัดไป
        if (keyboard.nKey.wasPressedThisFrame)
        {
            SkipToNextPeriod();
        }
    }

    private bool IsFastForwardHeld()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.fKey.isPressed;
    }
}
