using System;
using UnityEngine;
using UnityEngine.InputSystem;

// ช่วงเวลาของวัน ใช้ตัดสินว่าตอนนี้ควรสำรวจหรือเปิดร้าน
public enum DayPeriod
{
    Morning,
    Afternoon,
    Evening
}

// นาฬิกาในเกม: นับเวลา แบ่งช่วงเช้า/บ่าย/เย็น และแจ้ง event ให้ระบบอื่นรู้
public class GameClock : MonoBehaviour
{
    // ให้สคริปต์อื่นเข้าถึงนาฬิกาได้ผ่าน GameClock.Instance
    public static GameClock Instance { get; private set; }

    [Header("Time Settings")]
    [Tooltip("ชั่วโมงที่วันเริ่มต้น (0-23)")]
    [SerializeField] private int startHour = 6;

    [Tooltip("ชั่วโมงที่วันสิ้นสุด (24 = เที่ยงคืน)")]
    [SerializeField] private int endHour = 24;

    [Tooltip("เวลาในเกมที่เดินต่อหนึ่งครั้ง (นาที)")]
    [SerializeField] private int minutesPerTick = 10;

    [Tooltip("จำนวนวินาทีจริงต่อหนึ่งครั้งที่เวลาเดิน")]
    [SerializeField] private float realSecondsPerTick = 7f;

    [Header("Period Boundaries (Hour)")]
    [SerializeField] private int afternoonStartHour = 12;
    [SerializeField] private int eveningStartHour = 17;

    [Header("Debug")]
    [Tooltip("พิมพ์เวลาลง Console ทุกครั้งที่เวลาเดิน")]
    [SerializeField] private bool logEveryTick = false;

    [Tooltip("ตัวคูณความเร็วตอนกด F ค้าง")]
    [SerializeField] private float fastForwardMultiplier = 10f;

    // ---------- สถานะปัจจุบัน ----------

    // วันที่ในเกม เริ่มที่ 1
    public int Day { get; private set; } = 1;

    // เวลาเก็บเป็น "นาทีนับจากเที่ยงคืน" เช่น 06:30 = 390
    public int TotalMinutes { get; private set; }

    // แปลงเป็นชั่วโมงและนาทีให้อ่านง่าย
    public int Hour => TotalMinutes / 60;
    public int Minute => TotalMinutes % 60;

    public DayPeriod CurrentPeriod { get; private set; }
    public bool IsPaused { get; private set; }

    // ---------- Events ----------

    // ส่ง (ชั่วโมง, นาที) ทุกครั้งที่เวลาเปลี่ยน
    public event Action<int, int> OnTimeChanged;

    // ส่งช่วงเวลาใหม่เมื่อเปลี่ยนช่วง เช่น บ่าย -> เย็น
    public event Action<DayPeriod> OnPeriodChanged;

    // ส่งหมายเลขวันเมื่อเริ่มวันใหม่
    public event Action<int> OnDayStarted;

    // ส่งหมายเลขวันที่เพิ่งจบ
    public event Action<int> OnDayEnded;

    // ตัวจับเวลาสะสมวินาทีจริงจนครบหนึ่ง tick
    private float tickTimer = 0f;

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
        endHour = Mathf.Clamp(endHour, startHour + 1, 24);
        minutesPerTick = Mathf.Max(1, minutesPerTick);
        realSecondsPerTick = Mathf.Max(0.01f, realSecondsPerTick);
        fastForwardMultiplier = Mathf.Max(1f, fastForwardMultiplier);
    }

    private void Update()
    {
        HandleDebugInput();

        if (IsPaused)
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

            // ถ้าวันเพิ่งจบใน tick นี้ ให้หยุดลูปไว้ก่อน
            if (TotalMinutes == startHour * 60)
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
        TotalMinutes += minutes;

        // ถึงเวลาจบวันแล้ว
        if (TotalMinutes >= endHour * 60)
        {
            EndDay();
            return;
        }

        RaiseTimeChanged();
        UpdatePeriod(false);
    }

    // เริ่มวันใหม่: ตั้งเวลาเป็นเวลาเริ่มต้นและแจ้ง event
    private void BeginDay(int day)
    {
        Day = day;
        TotalMinutes = startHour * 60;
        tickTimer = 0f;

        Debug.Log($"Day {Day} started.");
        OnDayStarted?.Invoke(Day);

        RaiseTimeChanged();
        UpdatePeriod(true);
    }

    // จบวันปัจจุบันแล้วเริ่มวันถัดไป (ชั่วคราว จนกว่าจะมีระบบนอน)
    private void EndDay()
    {
        Debug.Log($"Day {Day} ended.");
        OnDayEnded?.Invoke(Day);

        BeginDay(Day + 1);
    }

    // คำนวณช่วงเวลาจากชั่วโมงปัจจุบัน
    private DayPeriod CalculatePeriod(int hour)
    {
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

    // ข้ามไปช่วงถัดไป ถ้าเป็นช่วงเย็นแล้วจะจบวัน
    public void SkipToNextPeriod()
    {
        tickTimer = 0f;

        switch (CurrentPeriod)
        {
            case DayPeriod.Morning:
                TotalMinutes = afternoonStartHour * 60;
                break;
            case DayPeriod.Afternoon:
                TotalMinutes = eveningStartHour * 60;
                break;
            case DayPeriod.Evening:
                EndDay();
                return;
        }

        RaiseTimeChanged();
        UpdatePeriod(false);
    }

    // ตั้งเวลาตรงๆ (เตรียมไว้ให้ระบบโหลดเซฟ)
    public void SetTime(int day, int totalMinutes)
    {
        Day = Mathf.Max(1, day);
        TotalMinutes = Mathf.Clamp(totalMinutes, startHour * 60, endHour * 60 - 1);
        tickTimer = 0f;

        RaiseTimeChanged();
        UpdatePeriod(true);
    }

    // คืนข้อความเวลา เช่น "Day 1 - 06:00 (Morning)"
    public string GetTimeString()
    {
        return $"Day {Day} - {Hour:00}:{Minute:00} ({CurrentPeriod})";
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
            Debug.Log($"Current time: {GetTimeString()}{(IsPaused ? " [PAUSED]" : "")}");
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