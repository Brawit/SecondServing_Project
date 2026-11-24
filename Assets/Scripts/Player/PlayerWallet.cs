using System;
using System.Globalization;
using UnityEngine;

// [2.6] กระเป๋าเงินของผู้เล่น (สกุล B = Baht) ใส่บนตัว Player
// - เงินเข้า: ลูกค้าจ่าย (ราคา + ทิป), ของแนบในจดหมาย / [2.6b] ขายของ, กล่องขายข้ามคืน
// - เงินออก: จ้างช่างก่อสร้าง / [2.6b] ซื้อของ
// - เซฟใน SaveData v8 (money)
public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance { get; private set; }

    [Header("Start")]
    [Tooltip("เงินตอนเริ่มเกมใหม่ (ไม่มีเซฟ)")]
    [Min(0)] [SerializeField] private int startMoney = 500;

    // เงินตอนนี้
    public int Money { get; private set; }

    // ส่งสัญญาณเมื่อเงินเปลี่ยน (เงินเดิม, เงินใหม่)
    public event Action<int, int> OnMoneyChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("PlayerWallet: Duplicate instance found, destroying this one.");
            Destroy(this);
            return;
        }
        Instance = this;

        // ค่าตั้งต้น ถ้ามีเซฟ SaveManager จะตั้งทับใน Start
        Money = Mathf.Max(0, startMoney);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public int StartMoney => Mathf.Max(0, startMoney);

    public bool CanAfford(int amount)
    {
        return amount <= 0 || Money >= amount;
    }

    public void Add(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetMoney(Money + amount);
    }

    // จ่ายเงิน: พอ = หักแล้วคืน true / ไม่พอ = ไม่หักอะไรเลย คืน false
    public bool TrySpend(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }
        if (Money < amount)
        {
            return false;
        }

        SetMoney(Money - amount);
        return true;
    }

    // ตั้งเงินตรงๆ (ใช้ตอนโหลดเซฟ / Debug)
    public void SetMoney(int amount)
    {
        int old = Money;
        Money = Mathf.Max(0, amount);
        if (old != Money)
        {
            OnMoneyChanged?.Invoke(old, Money);
        }
    }

    // รูปแบบเงินที่ใช้ทั้งเกม เช่น "B 1,250"
    public static string Format(int amount)
    {
        return "B " + amount.ToString("N0", CultureInfo.InvariantCulture);
    }

    // ---------- Debug (คลิกขวาที่หัวคอมโพเนนต์ระหว่าง Play) ----------

    [ContextMenu("Debug: Money +1,000")]
    private void DebugAdd1000() { Add(1000); Debug.Log($"PlayerWallet: {Format(Money)}"); }

    [ContextMenu("Debug: Money +10,000")]
    private void DebugAdd10000() { Add(10000); Debug.Log($"PlayerWallet: {Format(Money)}"); }

    [ContextMenu("Debug: Money +100,000")]
    private void DebugAdd100000() { Add(100000); Debug.Log($"PlayerWallet: {Format(Money)}"); }

    [ContextMenu("Debug: Reset Money")]
    private void DebugReset() { SetMoney(StartMoney); Debug.Log($"PlayerWallet: {Format(Money)}"); }
}
