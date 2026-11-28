using System;
using System.Collections.Generic;
using UnityEngine;

// ชนิดค่าสถานะของผู้เล่น
// หมายเหตุ: เพิ่มค่าใหม่ต่อท้ายเท่านั้น ห้ามสลับลำดับ
public enum StatType
{
    Health,
    Stamina,
    Mana,
    Fever
}

// ประเภทการปะทะในการต่อสู้ ที่ทำให้ได้ Mana และ FEVER
// หมายเหตุ: เพิ่มค่าใหม่ต่อท้ายเท่านั้น
public enum CombatHitType
{
    Attack,      // คลิกซ้ายตีโดนมอนสเตอร์
    HeavyAttack, // คลิกขวาตีหนักโดนมอนสเตอร์
    Block        // กด F ป้องกันการโจมตีของมอนสเตอร์ได้
}

// ค่าสถานะของผู้เล่น: Health / Stamina / Mana / FEVER
// - Health: ไม่ฟื้นเอง ฟื้นจากอาหารหรือการนอน
// - Stamina: ใช้กับเครื่องมือและการต่อสู้ ฟื้นเองหลังไม่ได้ใช้สักพัก (ยืนเฉยหรือเดินก็ฟื้น)
// - Mana: ไม่ฟื้นเอง ได้จากอาหาร/โพชั่น และการตีโดน/ป้องกันได้
//         ฟื้นช้าๆ ได้เฉพาะเมื่อมี "แหล่งฟื้น Mana" (อาหารบอส ของสวมใส่ ความชำนาญอาวุธ)
// - FEVER: หลอดท่าไม้ตาย เต็ม 200 ได้จากการต่อสู้และอาหารบางอย่าง เก็บข้ามวันได้
// ตื่นนอนแล้ว Health / Stamina / Mana เต็ม ส่วน FEVER คงค่าเดิม
public class PlayerStats : MonoBehaviour
{
    [Header("Base Max Values")]
    [Tooltip("Health สูงสุดตอนเริ่มเกม")]
    [Min(1)] [SerializeField] private int baseMaxHealth = 100;

    [Tooltip("Stamina สูงสุดตอนเริ่มเกม")]
    [Min(1)] [SerializeField] private int baseMaxStamina = 100;

    [Tooltip("Mana สูงสุดตอนเริ่มเกม")]
    [Min(1)] [SerializeField] private int baseMaxMana = 50;

    [Tooltip("FEVER สูงสุด (หลอดท่าไม้ตาย)")]
    [Min(1)] [SerializeField] private int maxFever = 200;

    [Header("Stamina Regen")]
    [Tooltip("Stamina ที่ฟื้นต่อวินาที")]
    [Min(0f)] [SerializeField] private float staminaRegenPerSecond = 10f;

    [Tooltip("ต้องไม่ใช้ Stamina กี่วินาที ถึงจะเริ่มฟื้น")]
    [Min(0f)] [SerializeField] private float staminaRegenDelay = 1.5f;

    [Header("Mana Regen")]
    [Tooltip("Mana ที่ฟื้นเองต่อวินาทีโดยไม่มีโบนัส (ตามที่ออกแบบไว้ = 0 ไม่ฟื้นเอง)")]
    [Min(0f)] [SerializeField] private float baseManaRegenPerSecond = 0f;

    [Header("Combat Gains")]
    [Tooltip("Mana ที่ได้เมื่อตีธรรมดาโดนมอนสเตอร์")]
    [Min(0)] [SerializeField] private int manaOnAttackHit = 2;

    [Tooltip("Mana ที่ได้เมื่อตีหนักโดนมอนสเตอร์")]
    [Min(0)] [SerializeField] private int manaOnHeavyHit = 4;

    [Tooltip("Mana ที่ได้เมื่อป้องกันการโจมตีของมอนสเตอร์ได้")]
    [Min(0)] [SerializeField] private int manaOnBlockHit = 2;

    [Tooltip("FEVER ที่ได้เมื่อตีธรรมดาโดน")]
    [Min(0)] [SerializeField] private int feverOnAttackHit = 1;

    [Tooltip("FEVER ที่ได้เมื่อตีหนักโดน")]
    [Min(0)] [SerializeField] private int feverOnHeavyHit = 2;

    [Tooltip("FEVER ที่ได้เมื่อป้องกันได้")]
    [Min(0)] [SerializeField] private int feverOnBlockHit = 1;

    [Header("Messages")]
    [Tooltip("ข้อความตอนกินของ ({0} = ชื่อ, {1} = ค่าที่ได้)")]
    [SerializeField] private string ateMessageFormat = "Ate {0}. {1}";

    [Tooltip("ข้อความตอนของนั้นกินไม่ได้ ({0} = ชื่อ)")]
    [SerializeField] private string cannotEatMessageFormat = "{0} can't be eaten as is.";

    [Tooltip("ข้อความตอน Stamina ไม่พอ")]
    [SerializeField] private string tooTiredMessage = "Too tired... rest a moment.";

    [Tooltip("ข้อความตอน Mana ไม่พอ")]
    [SerializeField] private string notEnoughManaMessage = "Not enough Mana.";

    // ค่าปัจจุบัน (เก็บเป็น float เพื่อให้ฟื้นแบบลื่นๆ ทีละนิดได้)
    private float health;
    private float stamina;
    private float mana;
    private float fever;

    // ค่าสูงสุดที่เพิ่มถาวร เช่น จากอาหารบอส (จะเซฟในขั้น B4)
    private int bonusMaxHealth;
    private int bonusMaxStamina;
    private int bonusMaxMana;

    // เวลาที่เหลือก่อน Stamina เริ่มฟื้น
    private float staminaRegenTimer;

    // แหล่งฟื้น Mana ช้าๆ "รหัสแหล่ง -> Mana ต่อวินาที" เช่น "ring_of_focus" -> 0.5
    private readonly Dictionary<string, float> manaRegenSources = new Dictionary<string, float>();

    private bool subscribedToDayEnd;

    // ---------- Events ----------

    // ส่งสัญญาณเมื่อค่าใดค่าหนึ่งเปลี่ยน (ให้ UI อัปเดต)
    public event Action OnStatsChanged;

    // ส่งสัญญาณเมื่อ Health เหลือ 0 (ยังไม่มีบทลงโทษ)
    public event Action OnHealthDepleted;

    // ส่งสัญญาณเมื่อ FEVER เต็มหลอด
    public event Action OnFeverFull;

    // ---------- ข้อมูลให้ระบบอื่นอ่าน ----------

    public float Health => health;
    public float Stamina => stamina;
    public float Mana => mana;
    public float Fever => fever;

    public int MaxHealth => baseMaxHealth + bonusMaxHealth;
    public int MaxStamina => baseMaxStamina + bonusMaxStamina;
    public int MaxMana => baseMaxMana + bonusMaxMana;
    public int MaxFever => maxFever;

    public bool IsFeverFull => fever >= maxFever;

    // Mana ที่ฟื้นต่อวินาทีตอนนี้ (ค่าพื้นฐาน + ทุกแหล่ง)
    public float ManaRegenPerSecond
    {
        get
        {
            float total = baseManaRegenPerSecond;
            foreach (float value in manaRegenSources.Values)
            {
                total += value;
            }
            return Mathf.Max(0f, total);
        }
    }

    private void Awake()
    {
        // เริ่มเกม: ทุกค่าเต็ม ยกเว้น FEVER เริ่มที่ 0
        health = MaxHealth;
        stamina = MaxStamina;
        mana = MaxMana;
        fever = 0f;
    }

    private void Start()
    {
        TrySubscribeDayEnd();
        OnStatsChanged?.Invoke();
    }

    private void OnEnable()
    {
        TrySubscribeDayEnd();
    }

    private void OnDisable()
    {
        UnsubscribeDayEnd();
    }

    private void Update()
    {
        bool changed = false;

        // Stamina: นับถอยหลังหลังใช้ แล้วค่อยฟื้น
        if (staminaRegenTimer > 0f)
        {
            staminaRegenTimer -= Time.deltaTime;
        }
        else if (stamina < MaxStamina && staminaRegenPerSecond > 0f)
        {
            stamina = Mathf.Min(MaxStamina, stamina + staminaRegenPerSecond * Time.deltaTime);
            changed = true;
        }

        // Mana: ฟื้นเฉพาะเมื่อมีแหล่งฟื้น
        float manaRegen = ManaRegenPerSecond;
        if (manaRegen > 0f && mana < MaxMana)
        {
            mana = Mathf.Min(MaxMana, mana + manaRegen * Time.deltaTime);
            changed = true;
        }

        if (changed)
        {
            OnStatsChanged?.Invoke();
        }
    }

    // ---------- อ่านค่าแบบระบุชนิด ----------

    public float GetCurrent(StatType type)
    {
        switch (type)
        {
            case StatType.Health: return health;
            case StatType.Stamina: return stamina;
            case StatType.Mana: return mana;
            case StatType.Fever: return fever;
            default: return 0f;
        }
    }

    public int GetMax(StatType type)
    {
        switch (type)
        {
            case StatType.Health: return MaxHealth;
            case StatType.Stamina: return MaxStamina;
            case StatType.Mana: return MaxMana;
            case StatType.Fever: return MaxFever;
            default: return 1;
        }
    }

    // ---------- เพิ่ม / ลด ----------

    // เพิ่มค่า (ไม่เกินค่าสูงสุด) คืนค่าที่เพิ่มได้จริง
    public float Restore(StatType type, float amount)
    {
        if (amount <= 0f)
        {
            return 0f;
        }

        float before = GetCurrent(type);
        float after = Mathf.Min(GetMax(type), before + amount);
        SetCurrentInternal(type, after);

        float gained = after - before;
        if (gained > 0f)
        {
            if (type == StatType.Fever && IsFeverFull && before < maxFever)
            {
                Debug.Log("PlayerStats: FEVER is full!");
                OnFeverFull?.Invoke();
            }

            OnStatsChanged?.Invoke();
        }

        return gained;
    }

    // ใช้ค่าสถานะ ถ้าไม่พอจะไม่หักเลยและคืนค่า false
    // showMessage = แสดงแจ้งเตือนเมื่อไม่พอ
    public bool TrySpend(StatType type, float amount, bool showMessage = true)
    {
        if (amount <= 0f)
        {
            return true;
        }

        float current = GetCurrent(type);
        if (current < amount)
        {
            if (showMessage)
            {
                if (type == StatType.Stamina) NotificationUI.Post(tooTiredMessage);
                else if (type == StatType.Mana) NotificationUI.Post(notEnoughManaMessage);
            }

            Debug.Log($"PlayerStats: Not enough {type} ({current:0}/{amount:0}).");
            return false;
        }

        SetCurrentInternal(type, current - amount);

        if (type == StatType.Stamina)
        {
            // เพิ่งใช้ Stamina ต้องรอก่อนจะฟื้น
            staminaRegenTimer = staminaRegenDelay;
        }

        OnStatsChanged?.Invoke();
        return true;
    }

    // ใช้ FEVER ทั้งหลอดสำหรับท่าไม้ตาย คืนค่า false ถ้ายังไม่เต็ม
    public bool TryUseFever()
    {
        if (!IsFeverFull)
        {
            Debug.Log($"PlayerStats: FEVER is not full yet ({fever:0}/{maxFever}).");
            return false;
        }

        fever = 0f;
        OnStatsChanged?.Invoke();
        return true;
    }

    // โดนโจมตี (ยังไม่มีบทลงโทษเมื่อ Health หมด)
    public void TakeDamage(float amount)
    {
        if (amount <= 0f || health <= 0f)
        {
            return;
        }

        health = Mathf.Max(0f, health - amount);
        OnStatsChanged?.Invoke();

        if (health <= 0f)
        {
            Debug.Log("PlayerStats: Health reached 0 (no penalty yet).");
            OnHealthDepleted?.Invoke();
        }
    }

    // ระบบต่อสู้เรียกเมื่อการโจมตี/ป้องกันโดนมอนสเตอร์ ได้ Mana และ FEVER ตามที่ตั้งไว้
    public void RegisterCombatHit(CombatHitType hitType)
    {
        int manaGain;
        int feverGain;

        switch (hitType)
        {
            case CombatHitType.Attack:
                manaGain = manaOnAttackHit;
                feverGain = feverOnAttackHit;
                break;
            case CombatHitType.HeavyAttack:
                manaGain = manaOnHeavyHit;
                feverGain = feverOnHeavyHit;
                break;
            case CombatHitType.Block:
                manaGain = manaOnBlockHit;
                feverGain = feverOnBlockHit;
                break;
            default:
                return;
        }

        Restore(StatType.Mana, manaGain);
        Restore(StatType.Fever, feverGain);
    }

    // ---------- กิน ----------

    // กินของ 1 ชิ้นจากกระเป๋า: เช็กว่ากินได้ หักออกจากกระเป๋า แล้วเพิ่มค่าสถานะ
    // คืนค่า true ถ้ากินสำเร็จ
    // [v2] จาน: quality = คุณภาพที่จะกิน (null = ต่ำสุดที่มีก่อน เก็บจานดีไว้) ค่า H/S/M × ตัวคูณคุณภาพ (DishPricing.EatValues)
    public bool TryEat(ItemData item, Inventory inventory, DishQuality? quality = null)
    {
        if (item == null || inventory == null)
        {
            return false;
        }

        if (!item.IsEdible)
        {
            NotificationUI.Post(string.Format(cannotEatMessageFormat, item.itemName));
            Debug.Log($"PlayerStats: {item.itemName} is not edible.");
            return false;
        }

        // คุณภาพที่จะกิน (เฉพาะจาน): ระบุมา = ต้องมีคุณภาพนั้น / ไม่ระบุ = ต่ำสุดที่มี
        DishQuality? eatQuality = null;
        if (Inventory.TracksQuality(item))
        {
            if (quality.HasValue)
            {
                eatQuality = quality.Value;
            }
            else
            {
                for (int q = 0; q < DishQualityUtil.Count; q++)
                {
                    if (inventory.GetCount(item, (DishQuality)q) > 0)
                    {
                        eatQuality = (DishQuality)q;
                        break;
                    }
                }
            }
        }

        bool removed = eatQuality.HasValue ? inventory.RemoveItem(item, 1, eatQuality.Value) : inventory.RemoveItem(item, 1);
        if (!removed)
        {
            Debug.Log($"PlayerStats: No {item.itemName} left to eat.");
            return false;
        }

        DishPricing.EatValues(item, eatQuality, out int health, out int stamina, out int mana, out int fever);
        float h = Restore(StatType.Health, health);
        float s = Restore(StatType.Stamina, stamina);
        float m = Restore(StatType.Mana, mana);
        float f = Restore(StatType.Fever, fever);

        string gains = BuildGainText(h, s, m, f);
        string qualityText = eatQuality.HasValue ? $" ({DishQualityUtil.Label(eatQuality.Value)})" : "";
        NotificationUI.Post(string.Format(ateMessageFormat, item.itemName + qualityText, gains));
        Debug.Log($"PlayerStats: Ate {item.itemName}{qualityText}. Values H{health} S{stamina} M{mana} F{fever}. {gains}");
        return true;
    }

    // สร้างข้อความ เช่น "+20 HP +35 ST" (ไม่แสดงค่าที่ได้ 0)
    private static string BuildGainText(float h, float s, float m, float f)
    {
        List<string> parts = new List<string>();
        if (h > 0f) parts.Add($"+{Mathf.RoundToInt(h)} HP");
        if (s > 0f) parts.Add($"+{Mathf.RoundToInt(s)} ST");
        if (m > 0f) parts.Add($"+{Mathf.RoundToInt(m)} MP");
        if (f > 0f) parts.Add($"+{Mathf.RoundToInt(f)} FEVER");
        return parts.Count > 0 ? string.Join(" ", parts) : "(already full)";
    }

    // ---------- แหล่งฟื้น Mana ช้าๆ ----------

    // เพิ่ม/แก้แหล่งฟื้น Mana เช่น ของสวมใส่ อาหารบอส ความชำนาญอาวุธ
    public void SetManaRegenSource(string sourceId, float perSecond)
    {
        if (string.IsNullOrEmpty(sourceId))
        {
            return;
        }

        if (perSecond <= 0f)
        {
            RemoveManaRegenSource(sourceId);
            return;
        }

        manaRegenSources[sourceId] = perSecond;
        Debug.Log($"PlayerStats: Mana regen source '{sourceId}' = {perSecond}/s (total {ManaRegenPerSecond}/s).");
    }

    public void RemoveManaRegenSource(string sourceId)
    {
        if (!string.IsNullOrEmpty(sourceId) && manaRegenSources.Remove(sourceId))
        {
            Debug.Log($"PlayerStats: Mana regen source '{sourceId}' removed (total {ManaRegenPerSecond}/s).");
        }
    }

    // ---------- ค่าสูงสุดถาวร (ใช้กับอาหารบอส และโหลดเซฟ) ----------

    public int GetBonusMax(StatType type)
    {
        switch (type)
        {
            case StatType.Health: return bonusMaxHealth;
            case StatType.Stamina: return bonusMaxStamina;
            case StatType.Mana: return bonusMaxMana;
            default: return 0;
        }
    }

    // ตั้งค่าสูงสุดที่เพิ่มถาวร (FEVER ไม่มีโบนัส)
    public void SetBonusMax(StatType type, int bonus)
    {
        bonus = Mathf.Max(0, bonus);

        switch (type)
        {
            case StatType.Health: bonusMaxHealth = bonus; break;
            case StatType.Stamina: bonusMaxStamina = bonus; break;
            case StatType.Mana: bonusMaxMana = bonus; break;
            default: return;
        }

        // ไม่ให้ค่าปัจจุบันเกินค่าสูงสุดใหม่
        SetCurrentInternal(type, Mathf.Min(GetCurrent(type), GetMax(type)));
        OnStatsChanged?.Invoke();
    }

    // ตั้ง FEVER โดยตรง (ใช้ตอนโหลดเซฟ)
    public void SetFever(float value)
    {
        fever = Mathf.Clamp(value, 0f, maxFever);
        OnStatsChanged?.Invoke();
    }

    // ---------- วันใหม่ ----------

    // ตื่นนอน: Health / Stamina / Mana เต็ม ส่วน FEVER คงค่าเดิม
    public void RefillForNewDay()
    {
        health = MaxHealth;
        stamina = MaxStamina;
        mana = MaxMana;
        staminaRegenTimer = 0f;

        Debug.Log($"PlayerStats: Refilled for new day (FEVER kept at {fever:0}/{maxFever}).");
        OnStatsChanged?.Invoke();
    }

    private void HandleDayEndFinished()
    {
        RefillForNewDay();
    }

    private void TrySubscribeDayEnd()
    {
        if (subscribedToDayEnd || DayEndManager.Instance == null)
        {
            return;
        }

        DayEndManager.Instance.OnDayEndFinished += HandleDayEndFinished;
        subscribedToDayEnd = true;
    }

    private void UnsubscribeDayEnd()
    {
        if (!subscribedToDayEnd)
        {
            return;
        }

        if (DayEndManager.Instance != null)
        {
            DayEndManager.Instance.OnDayEndFinished -= HandleDayEndFinished;
        }

        subscribedToDayEnd = false;
    }

    // ---------- ภายใน ----------

    private void SetCurrentInternal(StatType type, float value)
    {
        switch (type)
        {
            case StatType.Health: health = value; break;
            case StatType.Stamina: stamina = value; break;
            case StatType.Mana: mana = value; break;
            case StatType.Fever: fever = value; break;
        }
    }

    // ---------- Debug (คลิกขวาที่หัวคอมโพเนนต์ใน Inspector ระหว่าง Play) ----------

    [ContextMenu("Debug: Take 20 Damage")]
    private void DebugTakeDamage()
    {
        TakeDamage(20f);
    }

    [ContextMenu("Debug: Spend 30 Stamina")]
    private void DebugSpendStamina()
    {
        TrySpend(StatType.Stamina, 30f);
    }

    [ContextMenu("Debug: Spend 20 Mana")]
    private void DebugSpendMana()
    {
        TrySpend(StatType.Mana, 20f);
    }

    [ContextMenu("Debug: Simulate Attack Hit")]
    private void DebugAttackHit()
    {
        RegisterCombatHit(CombatHitType.Attack);
    }

    [ContextMenu("Debug: Simulate Heavy Attack Hit")]
    private void DebugHeavyHit()
    {
        RegisterCombatHit(CombatHitType.HeavyAttack);
    }

    [ContextMenu("Debug: Simulate Block Hit")]
    private void DebugBlockHit()
    {
        RegisterCombatHit(CombatHitType.Block);
    }

    [ContextMenu("Debug: Add 50 FEVER")]
    private void DebugAddFever()
    {
        Restore(StatType.Fever, 50f);
    }

    [ContextMenu("Debug: Use FEVER (Ultimate)")]
    private void DebugUseFever()
    {
        if (TryUseFever())
        {
            Debug.Log("PlayerStats: Ultimate used!");
        }
    }

    [ContextMenu("Debug: Add Mana Regen Source (1/s)")]
    private void DebugAddManaRegen()
    {
        SetManaRegenSource("debug", 1f);
    }

    [ContextMenu("Debug: Remove Mana Regen Source")]
    private void DebugRemoveManaRegen()
    {
        RemoveManaRegenSource("debug");
    }

    [ContextMenu("Debug: Refill For New Day")]
    private void DebugRefill()
    {
        RefillForNewDay();
    }

    // กินของชิ้นแรกที่กินได้ในกระเป๋า (ใช้ทดสอบก่อนมี Hotbar)
    [ContextMenu("Debug: Eat First Edible Item")]
    private void DebugEatFirstEdible()
    {
        Inventory inventory = GetComponent<Inventory>();
        if (inventory == null)
        {
            Debug.LogWarning("PlayerStats: No Inventory on this GameObject.");
            return;
        }

        foreach (KeyValuePair<ItemData, int> pair in inventory.Items)
        {
            if (pair.Key != null && pair.Key.IsEdible)
            {
                TryEat(pair.Key, inventory);
                return;
            }
        }

        Debug.Log("PlayerStats: No edible item in inventory.");
    }
}
