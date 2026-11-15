using TMPro;
using UnityEngine;

// แสดงวัน เวลา และช่วงเวลาบนหน้าจอ โดยฟัง event จาก GameClock
public class ClockUI : MonoBehaviour
{
    [Header("Text References")]
    [Tooltip("ข้อความแสดงวัน เช่น Day 1")]
    [SerializeField] private TMP_Text dayText;

    [Tooltip("ข้อความแสดงเวลา เช่น 06:00")]
    [SerializeField] private TMP_Text timeText;

    [Tooltip("ข้อความแสดงช่วงเวลา เช่น Morning")]
    [SerializeField] private TMP_Text periodText;

    [Header("Period Colors")]
    [SerializeField] private Color morningColor = new Color(1f, 0.85f, 0.4f);
    [SerializeField] private Color afternoonColor = new Color(1f, 0.6f, 0.3f);
    [SerializeField] private Color eveningColor = new Color(0.6f, 0.65f, 1f);

    // นาฬิกาที่กำลังฟังอยู่
    private GameClock clock;

    // สมัครฟัง event แล้วหรือยัง
    private bool isSubscribed = false;

    private void Start()
    {
        // เตือนถ้าลืมลากข้อความใส่ช่องใน Inspector
        if (dayText == null || timeText == null || periodText == null)
        {
            Debug.LogWarning("ClockUI: Some text references are not assigned in the Inspector.");
        }

        TrySubscribe();
    }

    private void Update()
    {
        // ถ้าตอน Start ยังหานาฬิกาไม่เจอ ให้ลองใหม่เรื่อยๆ
        if (!isSubscribed)
        {
            TrySubscribe();
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    // ---------- การสมัคร / ยกเลิกฟัง event ----------

    private void TrySubscribe()
    {
        clock = GameClock.Instance;
        if (clock == null)
        {
            return;
        }

        clock.OnTimeChanged += HandleTimeChanged;
        clock.OnPeriodChanged += HandlePeriodChanged;
        clock.OnDayStarted += HandleDayStarted;
        isSubscribed = true;

        // วาดค่าปัจจุบันทันที ไม่ต้องรอ event ถัดไป
        RefreshAll();
        Debug.Log("ClockUI: Connected to GameClock.");
    }

    private void Unsubscribe()
    {
        // ต้องยกเลิกเสมอ ไม่งั้นนาฬิกาจะเรียกหา UI ที่ถูกลบไปแล้ว
        if (clock != null && isSubscribed)
        {
            clock.OnTimeChanged -= HandleTimeChanged;
            clock.OnPeriodChanged -= HandlePeriodChanged;
            clock.OnDayStarted -= HandleDayStarted;
        }
        isSubscribed = false;
    }

    // ---------- ตัวรับ event ----------

    private void HandleTimeChanged(int hour, int minute)
    {
        UpdateTimeText(hour, minute);
    }

    private void HandlePeriodChanged(DayPeriod period)
    {
        UpdatePeriodText(period);
    }

    private void HandleDayStarted(int day)
    {
        UpdateDayText(day);
    }

    // ---------- การอัปเดตข้อความ ----------

    // อัปเดตทุกช่องพร้อมกันจากค่าปัจจุบันของนาฬิกา
    private void RefreshAll()
    {
        if (clock == null)
        {
            return;
        }

        UpdateDayText(clock.Day);
        UpdateTimeText(clock.Hour, clock.Minute);
        UpdatePeriodText(clock.CurrentPeriod);
    }

    private void UpdateDayText(int day)
    {
        if (dayText != null)
        {
            dayText.text = $"Day {day}";
        }
    }

    private void UpdateTimeText(int hour, int minute)
    {
        if (timeText != null)
        {
            // :00 คือเติมเลข 0 ข้างหน้าให้ครบ 2 หลัก เช่น 6 -> 06
            timeText.text = $"{hour:00}:{minute:00}";
        }
    }

    private void UpdatePeriodText(DayPeriod period)
    {
        if (periodText == null)
        {
            return;
        }

        periodText.text = period.ToString();
        periodText.color = GetPeriodColor(period);
    }

    // เลือกสีตามช่วงเวลา
    private Color GetPeriodColor(DayPeriod period)
    {
        switch (period)
        {
            case DayPeriod.Morning:
                return morningColor;
            case DayPeriod.Afternoon:
                return afternoonColor;
            case DayPeriod.Evening:
                return eveningColor;
            default:
                return Color.white;
        }
    }
}