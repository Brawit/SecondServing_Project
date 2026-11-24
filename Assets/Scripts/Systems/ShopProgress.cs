using System;
using System.Collections.Generic;
using UnityEngine;

// [2.6] ข้อมูล 1 เลเวลของร้าน: ขนาดหลอดชื่อเสียงของเลเวลนี้ + โบนัสรวมที่ได้เมื่อร้านอยู่เลเวลนี้
[Serializable]
public class ShopLevelInfo
{
    [Tooltip("ขนาดหลอดชื่อเสียงของเลเวลนี้ (เต็ม = จ้างช่างอัปเกรดได้ / เลเวลสูงสุด = ใช้เป็นเงื่อนไขจดหมายอย่างเดียว)")]
    [Min(1)] public int barSize = 500;

    [Tooltip("ช่องเมนูประจำวันที่ได้เพิ่ม (รวมทั้งหมดที่เลเวลนี้)")]
    [Min(0)] public int bonusMenuSlots;

    [Tooltip("จำนวนเมนูนิยมต่อวันที่ได้เพิ่ม (รวมทั้งหมดที่เลเวลนี้)")]
    [Min(0)] public int bonusPopular;

    [Tooltip("โอกาสลูกค้าชอบมากที่ได้เพิ่ม เช่น 0.05 = +5% (รวมทั้งหมดที่เลเวลนี้)")]
    [Range(0f, 1f)] public float bonusLovedChance;

    [Tooltip("ข้อความบอกสิ่งที่ปลดล็อกเมื่ออัปมาเลเวลนี้ (แสดงในหน้าต่างช่าง)")]
    public string unlockText;
}

// [2.5c → 2.6] ความก้าวหน้าของร้าน: เลเวลร้าน + ชื่อเสียงแบบหลอดทีละเลเวล
// - ชื่อเสียงเข้าหลอดของเลเวลปัจจุบัน เต็มแล้วหยุด (ดองไม่ได้) / ชื่อเสียงรวมตลอดเกมนับทุกแต้ม (งานครบรอบร้านเฟส 3)
// - จ้างช่างอัปเกรดร้าน = SpendBar (หลอดกลับ 0 + จำว่าหลอดเลเวลนี้เต็มแล้ว) → สร้างเสร็จ = LevelUp
// - เลเวลเปลี่ยน/โหลดเซฟ = ใส่โบนัส (ช่องเมนู, เมนูนิยม, โอกาสชอบมาก) ให้ระบบร้าน
// - จดหมายเชฟใช้ HasReached(level, reputation)
// ใส่คอมโพเนนต์นี้บนวัตถุ ShopManager / เซฟใน SaveData v7–v8
public class ShopProgress : MonoBehaviour
{
    public static ShopProgress Instance { get; private set; }

    [Header("Levels (index 0 = Level 1)")]
    [Tooltip("ตารางเลเวลร้าน (ว่างไว้ = ใช้ค่าตั้งต้น 4 เลเวล ตอนเริ่ม)")]
    [SerializeField] private List<ShopLevelInfo> levels = new List<ShopLevelInfo>();

    [Header("Debug")]
    [Tooltip("ค่าที่ใช้กับเมนู Debug: Apply Values")]
    [Min(1)] [SerializeField] private int debugLevel = 1;

    [Tooltip("ค่าที่ใช้กับเมนู Debug: Apply Values")]
    [Min(0)] [SerializeField] private int debugReputation = 0;

    // เลเวลร้าน (1..MaxLevel)
    public int Level { get; private set; } = 1;

    // ชื่อเสียงในหลอดของเลเวลปัจจุบัน (0..BarSize)
    public int Reputation { get; private set; }

    // ชื่อเสียงรวมตลอดเกม (นับทุกแต้ม รวมตอนหลอดเต็ม)
    public int LifetimeReputation { get; private set; }

    // จ่ายหลอดของเลเวลนี้ไปเริ่มอัปเกรดแล้ว (รอช่างสร้างเสร็จ) = นับว่าหลอดเลเวลนี้เคยเต็ม
    public bool BarSpent { get; private set; }

    public int MaxLevel => levels.Count;
    public int BarSize => GetInfo(Level) != null ? GetInfo(Level).barSize : 1;
    public bool IsBarFull => Reputation >= BarSize;
    public bool IsMaxLevel => Level >= MaxLevel;

    // ส่งสัญญาณเมื่อเลเวลหรือชื่อเสียงเปลี่ยน
    public event Action OnProgressChanged;

    // ส่งสัญญาณเมื่อร้านเลเวลขึ้น (เลเวลใหม่)
    public event Action<int> OnLevelUp;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("ShopProgress: Duplicate instance found, destroying this one.");
            Destroy(this);
            return;
        }
        Instance = this;

        EnsureDefaultLevels();
        Level = 1;
        Reputation = 0;
        LifetimeReputation = 0;
        BarSpent = false;
    }

    private void Start()
    {
        // เกมใหม่ (ไม่มีเซฟ) ก็ต้องใส่โบนัสของเลเวล 1 / มีเซฟ = SetAll ใส่ให้แล้ว ใส่ซ้ำไม่เป็นไร
        ApplyBonuses();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ค่าตั้งต้นตามแบบร่าง 2.6 (ผู้ใช้ตั้งหลอด 500 / 1,500 / 3,000)
    private void EnsureDefaultLevels()
    {
        if (levels.Count > 0)
        {
            return;
        }

        levels.Add(new ShopLevelInfo { barSize = 500, unlockText = "" });
        levels.Add(new ShopLevelInfo { barSize = 1500, bonusMenuSlots = 1, unlockText = "+1 menu slot" });
        levels.Add(new ShopLevelInfo { barSize = 3000, bonusMenuSlots = 1, bonusPopular = 1, bonusLovedChance = 0.05f, unlockText = "+1 popular dish, +5% chance customers love their meal" });
        levels.Add(new ShopLevelInfo { barSize = 3000, bonusMenuSlots = 2, bonusPopular = 1, bonusLovedChance = 0.10f, unlockText = "+1 menu slot, +5% chance customers love their meal" });
    }

    public ShopLevelInfo GetInfo(int level)
    {
        int index = level - 1;
        return index >= 0 && index < levels.Count ? levels[index] : null;
    }

    // ---------- ชื่อเสียง ----------

    // เพิ่มชื่อเสียง: เข้าหลอดได้ไม่เกิน BarSize (ส่วนเกินหาย) ชื่อเสียงรวมนับเต็ม คืนจำนวนที่เข้าหลอดจริง
    public int AddReputation(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        LifetimeReputation += amount;

        int before = Reputation;
        Reputation = Mathf.Min(BarSize, Reputation + amount);
        int gained = Reputation - before;

        if (gained > 0 && Reputation >= BarSize && before < BarSize)
        {
            Debug.Log($"ShopProgress: Reputation bar full ({Reputation}/{BarSize}) at level {Level}.");
        }

        OnProgressChanged?.Invoke();
        return gained;
    }

    // จ่ายหลอดเพื่อเริ่มอัปเกรด (ConstructionManager เรียก)
    public void SpendBar()
    {
        Reputation = 0;
        BarSpent = true;
        OnProgressChanged?.Invoke();
    }

    // สร้างเสร็จ: เลเวลขึ้น หลอดใหม่เริ่มที่ชื่อเสียงที่สะสมระหว่างก่อสร้าง (ไม่เกินหลอดใหม่)
    public void LevelUp()
    {
        if (IsMaxLevel)
        {
            Debug.LogWarning("ShopProgress: Already at max level.");
            return;
        }

        Level++;
        BarSpent = false;
        Reputation = Mathf.Min(Reputation, BarSize);
        ApplyBonuses();
        Debug.Log($"ShopProgress: Level up! Now level {Level}.");
        OnLevelUp?.Invoke(Level);
        OnProgressChanged?.Invoke();
    }

    // ถึงเกณฑ์ (เลเวล n + ชื่อเสียง x ในหลอดเลเวล n) แล้วหรือยัง (จดหมายเชฟ)
    public bool HasReached(int level, int reputation)
    {
        if (Level > level) return true;
        if (Level < level) return false;
        return Reputation >= reputation || BarSpent;
    }

    // ตั้งทุกค่าพร้อมกัน (ใช้ตอนโหลดเซฟ) แล้วใส่โบนัส
    public void SetAll(int level, int reputation, int lifetime, bool barSpent)
    {
        Level = Mathf.Clamp(level, 1, Mathf.Max(1, MaxLevel));
        Reputation = Mathf.Clamp(reputation, 0, BarSize);
        LifetimeReputation = Mathf.Max(lifetime, Reputation);
        BarSpent = barSpent;
        ApplyBonuses();
        OnProgressChanged?.Invoke();
    }

    // ใส่โบนัสของเลเวลปัจจุบันให้ระบบร้าน
    public void ApplyBonuses()
    {
        ShopLevelInfo info = GetInfo(Level);
        if (info == null)
        {
            return;
        }

        if (DailyMenu.Instance != null) DailyMenu.Instance.SetBonusSlots(info.bonusMenuSlots);
        if (PopularDishes.Instance != null) PopularDishes.Instance.SetBonusCount(info.bonusPopular);
        if (CustomerManager.Instance != null) CustomerManager.Instance.SetLovedChanceBonus(info.bonusLovedChance);
    }

    // ---------- Debug (คลิกขวาที่หัวคอมโพเนนต์ระหว่าง Play) ----------

    [ContextMenu("Debug: Reputation +100")]
    private void DebugReputationUp()
    {
        AddReputation(100);
        DebugPrint();
    }

    [ContextMenu("Debug: Fill Reputation Bar")]
    private void DebugFillBar()
    {
        AddReputation(Mathf.Max(0, BarSize - Reputation));
        DebugPrint();
    }

    [ContextMenu("Debug: Level +1")]
    private void DebugLevelUp()
    {
        LevelUp();
        DebugPrint();
    }

    [ContextMenu("Debug: Apply Values")]
    private void DebugApplyValues()
    {
        SetAll(debugLevel, debugReputation, Mathf.Max(LifetimeReputation, debugReputation), false);
        DebugPrint();
    }

    [ContextMenu("Debug: Reset Progress")]
    private void DebugResetProgress()
    {
        SetAll(1, 0, 0, false);
        DebugPrint();
    }

    [ContextMenu("Debug: Print Progress")]
    private void DebugPrint()
    {
        Debug.Log($"ShopProgress: Level {Level}/{MaxLevel}, Reputation {Reputation}/{BarSize}{(BarSpent ? " (bar spent, building)" : "")}, Lifetime {LifetimeReputation}");
    }
}
