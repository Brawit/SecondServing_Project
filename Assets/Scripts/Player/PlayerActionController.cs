using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// ตัวรับปุ่ม "ใช้ของ" ของผู้เล่น ทำงานตามของในช่อง Hotbar ที่เลือก
//
// | ปุ่ม           | ถืออาหาร | ถือเครื่องมือ | ถืออาวุธ     |
// | คลิกซ้าย       | -        | ใช้เครื่องมือ | Attack       |
// | คลิกขวา        | กิน      | -             | Heavy Attack |
// | F (ค้าง)       | -        | -             | Block        |
// | R              | -        | -             | Skill        |
// | ค้าง F แล้วกด R | -        | -             | Ultimate     |
//
// เลือกช่อง: ลูกกลิ้งเมาส์ หรือปุ่ม 1–9, 0
// ยังไม่มีการโจมตีจริง (เฟส 3) ตอนนี้หัก Stamina/Mana/FEVER แล้วแจ้ง event + Debug.Log
// ใช้ของไม่ได้ระหว่าง: เปิดกระเป๋า, เปิดร้าน (Hotbar ล็อก), หน้าสรุปวัน, เมาส์ชี้อยู่บน UI (เฉพาะคลิก)
[RequireComponent(typeof(Hotbar))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerActionController : MonoBehaviour
{
    [Header("Weapon Costs (placeholder until Phase 3)")]
    [Tooltip("Stamina ที่ใช้ต่อการตีธรรมดา")]
    [Min(0f)] [SerializeField] private float attackStaminaCost = 5f;

    [Tooltip("Stamina ที่ใช้ต่อการตีหนัก")]
    [Min(0f)] [SerializeField] private float heavyAttackStaminaCost = 15f;

    [Tooltip("Stamina ที่ใช้ตอนเริ่มป้องกัน")]
    [Min(0f)] [SerializeField] private float blockStaminaCost = 3f;

    [Tooltip("Mana ที่ใช้ต่อการใช้สกิล")]
    [Min(0f)] [SerializeField] private float skillManaCost = 20f;

    [Header("Tool Costs (placeholder until Phase 3)")]
    [Tooltip("Stamina ที่ใช้ต่อการใช้เครื่องมือ 1 ครั้ง")]
    [Min(0f)] [SerializeField] private float toolStaminaCost = 4f;

    [Header("Cooldowns (seconds)")]
    [Min(0f)] [SerializeField] private float attackCooldown = 0.4f;
    [Min(0f)] [SerializeField] private float heavyAttackCooldown = 0.9f;
    [Min(0f)] [SerializeField] private float skillCooldown = 1.0f;
    [Min(0f)] [SerializeField] private float toolCooldown = 0.5f;
    [Min(0f)] [SerializeField] private float eatCooldown = 0.5f;

    [Header("Scroll")]
    [Tooltip("กลับทิศลูกกลิ้งเมาส์ (เลื่อนลง = ช่องซ้าย)")]
    [SerializeField] private bool invertScroll = false;

    [Header("Messages")]
    [Tooltip("ข้อความตอนกดใช้ของระหว่างเปิดร้าน")]
    [SerializeField] private string lockedMessage = "Hotbar is locked while the shop is open.";

    [Tooltip("ข้อความตอน FEVER ยังไม่เต็ม ({0} = ค่าปัจจุบัน, {1} = ค่าสูงสุด)")]
    [SerializeField] private string feverNotFullFormat = "FEVER not full yet ({0}/{1}).";

    [Tooltip("ข้อความตอนของในช่องหมด ({0} = ชื่อ)")]
    [SerializeField] private string noItemLeftFormat = "No {0} left.";

    private Hotbar hotbar;
    private PlayerStats stats;
    private InventoryUI inventoryUI;

    private float nextActionTime;
    private float lastLockedMessageTime = -10f;

    // ---------- ข้อมูลให้ระบบอื่นอ่าน ----------

    // กำลังป้องกันอยู่หรือไม่ (ระบบต่อสู้เฟส 3 จะใช้เช็กตอนโดนโจมตี)
    public bool IsBlocking { get; private set; }

    // ---------- Events (ระบบต่อสู้/เครื่องมือในเฟส 3 มาฟังได้) ----------

    public event Action<ItemData> OnAttack;
    public event Action<ItemData> OnHeavyAttack;
    public event Action<ItemData> OnBlockStarted;
    public event Action<ItemData> OnBlockEnded;
    public event Action<ItemData> OnSkill;
    public event Action<ItemData> OnUltimate;
    public event Action<ItemData> OnToolUsed;
    public event Action<ItemData> OnItemEaten;

    private void Awake()
    {
        hotbar = GetComponent<Hotbar>();
        stats = GetComponent<PlayerStats>();
    }

    private void Start()
    {
        inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        // ระหว่างเปิดกระเป๋า / หน้าสรุปวัน: ไม่รับปุ่มใดๆ ของ Hotbar
        if (IsInventoryOpen() || IsDayEnding())
        {
            StopBlocking();
            return;
        }

        // เลือกช่อง (ทำได้แม้ตอนร้านเปิด เพราะไม่ได้ใช้ของ)
        HandleSelectionInput(keyboard, mouse);

        bool locked = IsShopServing();
        if (locked)
        {
            StopBlocking();
            if (PressedAnyUseButton(keyboard, mouse))
            {
                PostLockedMessage();
            }
            return;
        }

        HandleUseInput(keyboard, mouse);
    }

    // ---------- เลือกช่อง ----------

    private void HandleSelectionInput(Keyboard keyboard, Mouse mouse)
    {
        if (keyboard != null)
        {
            // ปุ่ม 1–9 = ช่อง 1–9, ปุ่ม 0 = ช่อง 10
            if (keyboard.digit1Key.wasPressedThisFrame) hotbar.Select(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) hotbar.Select(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) hotbar.Select(2);
            else if (keyboard.digit4Key.wasPressedThisFrame) hotbar.Select(3);
            else if (keyboard.digit5Key.wasPressedThisFrame) hotbar.Select(4);
            else if (keyboard.digit6Key.wasPressedThisFrame) hotbar.Select(5);
            else if (keyboard.digit7Key.wasPressedThisFrame) hotbar.Select(6);
            else if (keyboard.digit8Key.wasPressedThisFrame) hotbar.Select(7);
            else if (keyboard.digit9Key.wasPressedThisFrame) hotbar.Select(8);
            else if (keyboard.digit0Key.wasPressedThisFrame) hotbar.Select(9);
        }

        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // เลื่อนขึ้น = ช่องขวา (ปรับกลับทิศได้ใน Inspector)
                int direction = scroll > 0f ? 1 : -1;
                if (invertScroll) direction = -direction;
                hotbar.SelectOffset(direction);
            }
        }
    }

    // ---------- ใช้ของ ----------

    private void HandleUseInput(Keyboard keyboard, Mouse mouse)
    {
        ItemData item = hotbar.SelectedItem;
        bool hasItem = item != null && hotbar.SelectedCount > 0;
        bool isWeapon = hasItem && item.itemType == ItemType.Weapon;
        bool pointerOverUI = IsPointerOverUI();

        // ---- Block (ค้าง F) ----
        bool fHeld = keyboard != null && keyboard.fKey.isPressed;
        if (isWeapon && fHeld)
        {
            if (!IsBlocking)
            {
                StartBlocking(item);
            }
        }
        else
        {
            StopBlocking();
        }

        // ---- R: Skill หรือ Ultimate (ถ้าค้าง F อยู่) ----
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame && isWeapon)
        {
            if (fHeld) DoUltimate(item);
            else DoSkill(item);
        }

        if (mouse == null || pointerOverUI)
        {
            return;
        }

        // ---- คลิกซ้าย ----
        if (mouse.leftButton.wasPressedThisFrame && hasItem && !IsBlocking)
        {
            if (item.itemType == ItemType.Weapon) DoAttack(item);
            else if (item.itemType == ItemType.Tool) DoUseTool(item);
        }

        // ---- คลิกขวา ----
        if (mouse.rightButton.wasPressedThisFrame && !IsBlocking)
        {
            if (item == null)
            {
                return;
            }

            if (item.itemType == ItemType.Weapon)
            {
                if (hasItem) DoHeavyAttack(item);
            }
            else if (item.itemType == ItemType.Ingredient || item.itemType == ItemType.Dish)
            {
                DoEat(item, hasItem);
            }
        }
    }

    private void DoAttack(ItemData weapon)
    {
        if (!IsReady() || !stats.TrySpend(StatType.Stamina, attackStaminaCost)) return;

        SetCooldown(attackCooldown);
        Debug.Log($"PlayerAction: Attack with {weapon.itemName}.");
        OnAttack?.Invoke(weapon);
    }

    private void DoHeavyAttack(ItemData weapon)
    {
        if (!IsReady() || !stats.TrySpend(StatType.Stamina, heavyAttackStaminaCost)) return;

        SetCooldown(heavyAttackCooldown);
        Debug.Log($"PlayerAction: Heavy attack with {weapon.itemName}.");
        OnHeavyAttack?.Invoke(weapon);
    }

    private void StartBlocking(ItemData weapon)
    {
        // Stamina ไม่พอ = ป้องกันไม่ได้ (แจ้งเตือนครั้งเดียวตอนกด F)
        Keyboard keyboard = Keyboard.current;
        bool justPressed = keyboard != null && keyboard.fKey.wasPressedThisFrame;
        if (!stats.TrySpend(StatType.Stamina, blockStaminaCost, justPressed))
        {
            return;
        }

        IsBlocking = true;
        Debug.Log($"PlayerAction: Block started ({weapon.itemName}).");
        OnBlockStarted?.Invoke(weapon);
    }

    private void StopBlocking()
    {
        if (!IsBlocking)
        {
            return;
        }

        IsBlocking = false;
        ItemData weapon = hotbar != null ? hotbar.SelectedItem : null;
        Debug.Log("PlayerAction: Block ended.");
        OnBlockEnded?.Invoke(weapon);
    }

    private void DoSkill(ItemData weapon)
    {
        if (!IsReady() || !stats.TrySpend(StatType.Mana, skillManaCost)) return;

        SetCooldown(skillCooldown);
        Debug.Log($"PlayerAction: Skill with {weapon.itemName}.");
        OnSkill?.Invoke(weapon);
    }

    private void DoUltimate(ItemData weapon)
    {
        if (!stats.IsFeverFull)
        {
            NotificationUI.Post(string.Format(feverNotFullFormat, Mathf.FloorToInt(stats.Fever), stats.MaxFever));
            return;
        }

        if (!IsReady() || !stats.TryUseFever()) return;

        SetCooldown(skillCooldown);
        Debug.Log($"PlayerAction: ULTIMATE with {weapon.itemName}!");
        OnUltimate?.Invoke(weapon);
    }

    private void DoUseTool(ItemData tool)
    {
        if (!IsReady() || !stats.TrySpend(StatType.Stamina, toolStaminaCost)) return;

        SetCooldown(toolCooldown);
        Debug.Log($"PlayerAction: Used tool {tool.itemName} (no effect yet).");
        OnToolUsed?.Invoke(tool);
    }

    private void DoEat(ItemData food, bool hasItem)
    {
        if (!hasItem)
        {
            NotificationUI.Post(string.Format(noItemLeftFormat, food.itemName));
            return;
        }

        if (!IsReady()) return;

        if (stats.TryEat(food, hotbar.Inventory))
        {
            SetCooldown(eatCooldown);
            OnItemEaten?.Invoke(food);
        }
    }

    // ---------- เงื่อนไข ----------

    private bool IsReady()
    {
        return Time.time >= nextActionTime;
    }

    private void SetCooldown(float seconds)
    {
        nextActionTime = Time.time + seconds;
    }

    private bool IsInventoryOpen()
    {
        return inventoryUI != null && inventoryUI.IsOpen;
    }

    private static bool IsShopServing()
    {
        return ShopManager.Instance != null && ShopManager.Instance.IsServing;
    }

    private static bool IsDayEnding()
    {
        return DayEndManager.Instance != null && DayEndManager.Instance.IsDayEnding;
    }

    // เมาส์ชี้อยู่บน UI ที่รับคลิก (ปุ่ม ช่อง ฯลฯ) หรือไม่
    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    // ไม่นับคลิกซ้าย เพราะระหว่างเปิดร้านอาจใช้คลิกซ้ายกับระบบร้าน (เสิร์ฟ ฯลฯ)
    private bool PressedAnyUseButton(Keyboard keyboard, Mouse mouse)
    {
        if (hotbar.SelectedItem == null)
        {
            return false;
        }

        bool key = keyboard != null && (keyboard.fKey.wasPressedThisFrame || keyboard.rKey.wasPressedThisFrame);
        bool click = mouse != null && mouse.rightButton.wasPressedThisFrame && !IsPointerOverUI();
        return key || click;
    }

    // แจ้งเตือนว่าล็อกอยู่ (เว้นระยะ ไม่ให้เด้งรัวๆ)
    private void PostLockedMessage()
    {
        if (Time.unscaledTime - lastLockedMessageTime < 2f)
        {
            return;
        }

        lastLockedMessageTime = Time.unscaledTime;
        NotificationUI.Post(lockedMessage);
    }
}
