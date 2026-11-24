using System;
using UnityEngine;

// [2.5c] ความก้าวหน้าของร้าน: เลเวลร้าน + ชื่อเสียง
// - ตอนนี้ยังไม่มีอะไรเพิ่มค่าเอง ใช้เมนู Debug ปรับ (คลิกขวาที่หัวคอมโพเนนต์ระหว่าง Play)
// - [2.6] จะเพิ่มชื่อเสียงจากความพอใจลูกค้า และอัปเกรดเลเวลร้านจากที่นี่
// - ระบบจดหมายเชฟ (MailSystem) ใช้ค่าทั้งสองเป็นเงื่อนไขส่งจดหมาย / เซฟใน SaveData v7
// ใส่คอมโพเนนต์นี้บนวัตถุ ShopManager (Tools > Second Serving > Build Mail System ใส่ให้)
public class ShopProgress : MonoBehaviour
{
    public static ShopProgress Instance { get; private set; }

    [Header("Start Values")]
    [Tooltip("เลเวลร้านตอนเริ่มเกมใหม่ (ไม่มีเซฟ)")]
    [Min(1)] [SerializeField] private int startLevel = 1;

    [Tooltip("ชื่อเสียงตอนเริ่มเกมใหม่ (ไม่มีเซฟ)")]
    [Min(0)] [SerializeField] private int startReputation = 0;

    [Header("Debug")]
    [Tooltip("ค่าที่ใช้กับเมนู Debug: Apply Values")]
    [Min(1)] [SerializeField] private int debugLevel = 1;

    [Tooltip("ค่าที่ใช้กับเมนู Debug: Apply Values")]
    [Min(0)] [SerializeField] private int debugReputation = 0;

    // เลเวลร้าน (เริ่ม 1)
    public int Level { get; private set; } = 1;

    // ชื่อเสียงสะสม (ไม่ติดลบ)
    public int Reputation { get; private set; }

    // ส่งสัญญาณเมื่อเลเวลหรือชื่อเสียงเปลี่ยน
    public event Action OnProgressChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("ShopProgress: Duplicate instance found, destroying this one.");
            Destroy(this);
            return;
        }
        Instance = this;

        // ค่าตั้งต้น ถ้ามีเซฟ SaveManager จะตั้งทับใน Start
        Level = Mathf.Max(1, startLevel);
        Reputation = Mathf.Max(0, startReputation);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ---------- ตั้งค่า ----------

    public void SetLevel(int level)
    {
        level = Mathf.Max(1, level);
        if (level == Level)
        {
            return;
        }

        Level = level;
        Debug.Log($"ShopProgress: Level = {Level}");
        OnProgressChanged?.Invoke();
    }

    public void SetReputation(int reputation)
    {
        reputation = Mathf.Max(0, reputation);
        if (reputation == Reputation)
        {
            return;
        }

        Reputation = reputation;
        Debug.Log($"ShopProgress: Reputation = {Reputation}");
        OnProgressChanged?.Invoke();
    }

    // เพิ่ม (หรือลดถ้าติดลบ) ชื่อเสียง
    public void AddReputation(int amount)
    {
        SetReputation(Reputation + amount);
    }

    // ตั้งทั้งสองค่าพร้อมกัน (ใช้ตอนโหลดเซฟ) แจ้ง event ครั้งเดียว
    public void SetAll(int level, int reputation)
    {
        Level = Mathf.Max(1, level);
        Reputation = Mathf.Max(0, reputation);
        OnProgressChanged?.Invoke();
    }

    // ---------- Debug (คลิกขวาที่หัวคอมโพเนนต์ระหว่าง Play) ----------

    [ContextMenu("Debug: Level +1")]
    private void DebugLevelUp()
    {
        SetLevel(Level + 1);
    }

    [ContextMenu("Debug: Reputation +100")]
    private void DebugReputationUp()
    {
        AddReputation(100);
    }

    [ContextMenu("Debug: Apply Values")]
    private void DebugApplyValues()
    {
        SetAll(debugLevel, debugReputation);
        Debug.Log($"ShopProgress: Debug set Level {Level}, Reputation {Reputation}");
    }

    [ContextMenu("Debug: Reset Progress")]
    private void DebugResetProgress()
    {
        SetAll(startLevel, startReputation);
        Debug.Log($"ShopProgress: Reset to Level {Level}, Reputation {Reputation}");
    }

    [ContextMenu("Debug: Print Progress")]
    private void DebugPrint()
    {
        Debug.Log($"ShopProgress: Level {Level}, Reputation {Reputation}");
    }
}
