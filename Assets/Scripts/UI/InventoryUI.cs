using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// แท็บของกระเป๋า (ลำดับต้องตรงกับปุ่มแท็บใน Inspector)
public enum BagTab
{
    Ingredients,
    Dishes,
    Crafting,   // [v2 ผู้ใช้] แทนแท็บ Recipes เดิม (สูตรอาหารดูที่แท็บ Dishes แทน) ตอนนี้ยังไม่มีสูตรคราฟต์
    Equipment,
    Accessory
}

// หน้าต่างกระเป๋าแบบ Collectibles: แสดงไอเทมทุกชนิดในเกมตามแท็บ
// ของที่เคยเจอแสดงไอคอนและจำนวน ของที่ยังไม่เคยเจอไม่แสดง (ปิด Hide Undiscovered = เงาดำแบบเดิม) คลิกช่องเพื่อดูรายละเอียด
// [v2 ผู้ใช้] แท็บ Recipes เดิมเปลี่ยนเป็น Crafting (ยังว่าง) / สูตรอาหารดูที่แท็บ Dishes: วางเมาส์หรือคลิก = วัตถุดิบ + ทำได้กี่จาน
// กด I เปิด/ปิด, Esc ปิด, Q / E เปลี่ยนแท็บ
// [v2] แท็บ Dishes: 1 ช่องต่อสูตร (รวมทุกแบบทุกคุณภาพ) / วางเมาส์ = กล่องแยกแบบ / คลิก = แถวแบบ × คุณภาพ ในแผงรายละเอียด (กิน / ลากลง Hotbar)
// หมายเหตุ: ต้องวางสคริปต์นี้บนวัตถุที่เปิดอยู่ตลอด (ไม่ใช่ตัวหน้าต่างที่ถูกซ่อน)
public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("กระเป๋าของผู้เล่น (ลาก Player มาใส่)")]
    [SerializeField] private Inventory inventory;

    [Tooltip("สมุดสูตรของผู้เล่น (ลาก Player มาใส่)")]
    [SerializeField] private RecipeBook recipeBook;

    [Tooltip("ไฟล์ข้อมูลรวมไอเทม (Assets/ItemDatabase)")]
    [SerializeField] private ItemDatabase itemDatabase;

    [Tooltip("ไฟล์ข้อมูลรวมสูตร (Assets/Recipes/RecipeDatabase)")]
    [SerializeField] private RecipeDatabase recipeDatabase;

    [Tooltip("ตัวควบคุมการเดินของผู้เล่น (ลาก Player มาใส่) ใช้หยุดเดินตอนเปิดกระเป๋า")]
    [SerializeField] private PlayerController playerController;

    [Header("Window")]
    [Tooltip("ตัวหน้าต่างที่จะซ่อน/แสดง")]
    [SerializeField] private GameObject windowRoot;

    [Tooltip("วัตถุที่มี Grid Layout Group ไว้เป็นที่วางช่อง")]
    [SerializeField] private Transform slotContainer;

    [Tooltip("Prefab ของช่อง 1 ช่อง")]
    [SerializeField] private InventorySlotUI slotPrefab;

    [Header("Tabs")]
    [Tooltip("ปุ่มแท็บ 5 ปุ่ม เรียงตามลำดับ: Ingredients, Dishes, Crafting, Equipment, Accessory")]
    [SerializeField] private Button[] tabButtons = new Button[5];

    [Tooltip("ชื่อแท็บที่แสดงในหัวข้อ (เรียงลำดับเดียวกับปุ่ม)")]
    [SerializeField] private string[] tabTitles = { "Ingredients", "Dishes", "Recipes", "Equipment", "Accessory" };

    [Tooltip("ข้อความหัวข้อของแท็บ (เว้นว่างได้)")]
    [SerializeField] private TMP_Text titleText;

    [Tooltip("ข้อความความคืบหน้า เช่น Discovered 12 / 83 (เว้นว่างได้)")]
    [SerializeField] private TMP_Text progressText;

    [Tooltip("ข้อความระดับกระเป๋า (เว้นว่างได้)")]
    [SerializeField] private TMP_Text bagLevelText;

    [Tooltip("ข้อความที่แสดงเมื่อแท็บนี้ยังไม่มีไอเทมเลย (เว้นว่างได้)")]
    [SerializeField] private TMP_Text emptyTabText;

    [Header("Details")]
    [Tooltip("แผงรายละเอียดด้านข้าง (เว้นว่างได้)")]
    [SerializeField] private GameObject detailPanel;

    [Tooltip("ชื่อของสิ่งที่เลือก")]
    [SerializeField] private TMP_Text detailNameText;

    [Tooltip("รายละเอียดของสิ่งที่เลือก")]
    [SerializeField] private TMP_Text detailBodyText;

    [Header("Equipment")]
    [Tooltip("แผงช่องสวมใส่ (แสดงเฉพาะแท็บ Equipment / Accessory) เว้นว่างได้")]
    [SerializeField] private EquipmentPanelUI equipmentPanel;

    [Tooltip("ตอนแสดงแผงช่องสวมใส่ ให้เลื่อนขอบบนของแผงรายละเอียดลงเท่าไร (ให้พอดีใต้แผงสวมใส่)")]
    [Min(0f)] [SerializeField] private float detailShiftWithEquipment = 330f;

    [Header("Settings")]
    [Tooltip("หยุดนาฬิกาในเกมระหว่างเปิดกระเป๋า")]
    [SerializeField] private bool pauseClockWhileOpen = true;

    [Tooltip("ห้ามผู้เล่นเดินระหว่างเปิดกระเป๋า")]
    [SerializeField] private bool blockMovementWhileOpen = true;

    [Tooltip("ซ่อนจานของสูตรลับที่ยังไม่รู้ในแท็บ Dishes (ไม่แสดงแม้แต่เงาดำ)\nจานจะกลับมาแสดงเมื่อรู้สูตร หรือเคยได้จานนั้นมาแล้ว")]
    [SerializeField] private bool hideUnknownSecretRecipes = true;

    [Tooltip("[v2] ไม่แสดงของที่ยังไม่เคยเจอเลย (ทุกแท็บ) และสูตรที่ยังไม่รู้ (แท็บ Recipes) แบบหน้าต่างคราฟต์ของ Stardew\nไม่ติ๊ก = แสดงเป็นเงาดำแบบเดิม\nแท็บ Dishes: รู้สูตรแล้ว = แสดงจานนั้น (จางถ้ายังไม่มี)")]
    [SerializeField] private bool hideUndiscovered = true;

    [Tooltip("แท็บที่เปิดเป็นแท็บแรก")]
    [SerializeField] private BagTab startingTab = BagTab.Ingredients;

    [Header("Messages")]
    [Tooltip("ข้อความแจ้งเตือนตอนเปิดกระเป๋าไม่ได้ (เช่น ระหว่างเปิดร้าน)")]
    [SerializeField] private string unavailableMessage = "You can't open your bag right now.";

    [Tooltip("ข้อความในแท็บที่ยังไม่มีไอเทม")]
    [SerializeField] private string emptyTabMessage = "Nothing here yet.";

    [Tooltip("[v2] ข้อความในแท็บ Crafting ตอนยังไม่มีสูตรคราฟต์")]
    [SerializeField] private string emptyCraftingMessage = "No crafting recipes yet.";

    // หน้าต่างเปิดอยู่หรือไม่
    public bool IsOpen { get; private set; }

    // แท็บที่กำลังดูอยู่
    public BagTab CurrentTab { get; private set; }

    // ช่องที่สร้างไว้แล้ว นำกลับมาใช้ซ้ำ
    private readonly List<InventorySlotUI> slots = new List<InventorySlotUI>();

    // รายการของแท็บปัจจุบัน (ใช้ซ้ำ ไม่สร้างใหม่ทุกครั้ง)
    private readonly List<ItemData> itemBuffer = new List<ItemData>();
    private readonly List<SlotState> itemStateBuffer = new List<SlotState>();
    private readonly List<int> itemCountBuffer = new List<int>();

    // สิ่งที่ผู้เล่นคลิกเลือกไว้
    private ItemData selectedItem;
    private RecipeData selectedRecipe;

    // ช่องสวมใส่ที่คลิกเลือกไว้ (ใช้แสดงรายละเอียดของช่องว่าง)
    private EquipmentSlotUI selectedEquipSlot;

    // ขอบบนเดิมของแผงรายละเอียด (ใช้คืนค่าตอนออกจากแท็บที่มีแผงสวมใส่)
    private RectTransform detailRect;
    private float detailOriginalTop;

    // ต้องวาดใหม่ตอนเปิดหน้าต่างครั้งหน้าหรือไม่ (ระหว่างปิดอยู่จะไม่วาด เพื่อประหยัด)
    private bool isDirty = true;

    // จำไว้ว่าหน้าต่างนี้เป็นคนหยุดนาฬิกาเอง
    private bool pausedClockByThis = false;

    // ---------- [2.5] กล่องบอกคุณภาพจาน (สร้างเองตอนใช้ครั้งแรก ไม่ต้องวางในฉาก) ----------

    [Header("Dish Quality Tooltip")]
    [Tooltip("เอาเมาส์วางบนจานในกระเป๋า = แสดงจำนวนแยกตามคุณภาพ")]
    [SerializeField] private bool showQualityTooltip = true;

    [Tooltip("ระยะห่างของกล่องจากเมาส์ (หน่วยตาม Canvas)")]
    [SerializeField] private Vector2 tooltipOffset = new Vector2(24f, -24f);

    [Tooltip("ขนาดตัวอักษรในกล่อง")]
    [Min(8f)] [SerializeField] private float tooltipFontSize = 22f;

    private InventorySlotUI hoveredSlot;
    private RectTransform tooltipRect;
    private TMP_Text tooltipText;
    private Canvas tooltipCanvas;

    // ---------- [v2] จานแยกแบบ: แถว แบบ × คุณภาพ ในแผงรายละเอียด (สร้างด้วยโค้ด ไม่ต้องรัน Build Inventory Window Layout) ----------

    [Header("Dish Rows (v2)")]
    [Tooltip("ค่าพลังของผู้เล่น ใช้กับปุ่ม Eat ในแถวจาน (เว้นว่างไว้ จะหาบน GameObject เดียวกับ Inventory)")]
    [SerializeField] private PlayerStats playerStats;

    [Tooltip("ความสูงของแถว 1 แถว (หน่วยตาม Canvas)")]
    [Min(24f)] [SerializeField] private float dishRowHeight = 48f;

    [Tooltip("ขนาดตัวอักษรในแถว")]
    [Min(8f)] [SerializeField] private float dishRowFontSize = 22f;

    [Tooltip("ระยะห่างระหว่างแถว")]
    [Min(0f)] [SerializeField] private float dishRowSpacing = 6f;

    [Tooltip("ความสูงสูงสุดของรายการแถว (เกินนี้ = เลื่อนขึ้นลงได้)")]
    [Min(60f)] [SerializeField] private float dishRowListMaxHeight = 360f;

    [Tooltip("ระยะห่างระหว่างข้อความรายละเอียดกับรายการแถว")]
    [Min(0f)] [SerializeField] private float dishRowListGap = 10f;

    [Tooltip("ข้อความแถว {0} = ชื่อวัตถุดิบของแบบ, {1} = คุณภาพ, {2} = จำนวน")]
    [SerializeField] private string dishRowFormat = "{0} · {1} x{2}";

    [Tooltip("ข้อความแถวของจานที่ไม่มีแบบ {0} = คุณภาพ, {1} = จำนวน")]
    [SerializeField] private string dishRowPlainFormat = "{0} x{1}";

    [Tooltip("ชื่อแทนวัตถุดิบ ของจานแม่ในสูตรที่มีช่องหมวด (เช่น จานจากเซฟเก่า)")]
    [SerializeField] private string baseDishLabel = "Basic";

    [Tooltip("ข้อความบนปุ่มกิน")]
    [SerializeField] private string eatButtonLabel = "Eat";

    [Tooltip("เอาเมาส์วางบนแถวจาน = กล่องบอกค่าฟื้นพลังของแบบ × คุณภาพนั้น (ใช้กล่องเดียวกับกล่องคุณภาพ)")]
    [SerializeField] private bool showRowRestoreTooltip = true;

    [Tooltip("ข้อความในกล่องค่าฟื้นพลัง เมื่อจานนี้ไม่ฟื้นอะไรเลย")]
    [SerializeField] private string noRestoreText = "Restores nothing.";

    private DishVariantRowUI hoveredRow;

    [Tooltip("[v2] ข้อความในกล่อง/รายละเอียด เมื่อยังไม่รู้สูตรของจานนี้")]
    [SerializeField] private string recipeNotLearnedText = "Recipe not learned yet.";

    [Tooltip("[v2] หัวข้อส่วนจำนวนในกระเป๋า ในกล่องตอนวางเมาส์บนช่องจาน")]
    [SerializeField] private string inBagHeader = "In bag:";

    [Tooltip("[v2] สีของวัตถุดิบที่ขาด (รหัสสีแบบ #RRGGBB)")]
    [SerializeField] private string missingIngredientColor = "#FF8A8A";

    private RectTransform dishRowListRect;
    private RectTransform dishRowContent;
    private ScrollRect dishRowScroll;
    private readonly List<DishVariantRowUI> dishRows = new List<DishVariantRowUI>();
    private RectTransform detailBodyRect;
    private float detailBodyOriginalBottom;
    private bool dishRowListVisible;

    // จานในกลุ่มของช่องที่เลือก / ที่วางเมาส์ (ใช้ซ้ำ)
    private readonly List<ItemData> groupBuffer = new List<ItemData>();

    // สูตรที่มีจานแบบที่เคยได้มาแล้วอย่างน้อย 1 แบบ (คิดใหม่ทุกครั้งที่วาด)
    private readonly HashSet<RecipeData> discoveredVariantRecipes = new HashSet<RecipeData>();

    private void Awake()
    {
        // เตือนถ้าลืมลากของใส่ช่องใน Inspector
        if (inventory == null) Debug.LogWarning("InventoryUI: Inventory is not assigned.", this);
        if (recipeBook == null) Debug.LogWarning("InventoryUI: Recipe Book is not assigned. Recipes tab will be empty.", this);
        if (itemDatabase == null) Debug.LogWarning("InventoryUI: Item Database is not assigned.", this);
        if (recipeDatabase == null && recipeBook != null && recipeBook.Database != null)
        {
            recipeDatabase = recipeBook.Database;
        }
        if (recipeDatabase == null) Debug.LogWarning("InventoryUI: Recipe Database is not assigned.", this);
        if (windowRoot == null) Debug.LogWarning("InventoryUI: Window Root is not assigned.", this);
        if (slotContainer == null) Debug.LogWarning("InventoryUI: Slot Container is not assigned.", this);
        if (slotPrefab == null) Debug.LogWarning("InventoryUI: Slot Prefab is not assigned.", this);
        if (blockMovementWhileOpen && playerController == null) Debug.LogWarning("InventoryUI: Player Controller is not assigned.", this);
        if (playerStats == null && inventory != null)
        {
            playerStats = inventory.GetComponent<PlayerStats>();
        }
        if (playerStats == null) Debug.LogWarning("InventoryUI: Player Stats not found. The Eat buttons in dish rows won't work.", this);

        // ป้องกันการวางสคริปต์ผิดที่ (บนตัวหน้าต่างเอง)
        if (windowRoot == gameObject)
        {
            Debug.LogWarning("InventoryUI: Do not put this script on the Window Root itself. Put it on its parent.", this);
        }

        // ผูกปุ่มแท็บ
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null)
            {
                continue;
            }

            BagTab tab = (BagTab)i;
            tabButtons[i].onClick.AddListener(() => SetTab(tab));
        }

        CurrentTab = startingTab;

        // จำขอบบนเดิมของแผงรายละเอียดไว้
        if (detailPanel != null)
        {
            detailRect = detailPanel.GetComponent<RectTransform>();
            if (detailRect != null)
            {
                detailOriginalTop = detailRect.offsetMax.y;
            }
        }

        if (equipmentPanel != null)
        {
            equipmentPanel.OnSlotClicked += HandleEquipSlotClicked;
            if (equipmentPanel.Equipment == null)
            {
                Debug.LogWarning("InventoryUI: Equipment Panel has no Player Equipment assigned.", this);
            }
        }
    }

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.OnChanged += MarkDirty;
        }
        if (recipeBook != null)
        {
            recipeBook.OnChanged += MarkDirty;
        }
        if (equipmentPanel != null && equipmentPanel.Equipment != null)
        {
            equipmentPanel.Equipment.OnChanged += MarkDirty;
        }
    }

    private void OnDisable()
    {
        HideQualityTooltip();

        if (inventory != null)
        {
            inventory.OnChanged -= MarkDirty;
        }
        if (recipeBook != null)
        {
            recipeBook.OnChanged -= MarkDirty;
        }
        if (equipmentPanel != null && equipmentPanel.Equipment != null)
        {
            equipmentPanel.Equipment.OnChanged -= MarkDirty;
        }
    }

    private void OnDestroy()
    {
        if (equipmentPanel != null)
        {
            equipmentPanel.OnSlotClicked -= HandleEquipSlotClicked;
        }
    }

    private void Start()
    {
        // เริ่มเกมโดยปิดหน้าต่างไว้
        SetWindowVisible(false);
        IsOpen = false;
    }

    private void Update()
    {
        UpdateQualityTooltip();

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        // I = เปิด/ปิดกระเป๋า
        if (keyboard.iKey.wasPressedThisFrame)
        {
            Toggle();
            return;
        }

        if (!IsOpen)
        {
            return;
        }

        // Esc = ปิดกระเป๋า
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        // Q / E = แท็บก่อนหน้า / ถัดไป
        if (keyboard.qKey.wasPressedThisFrame)
        {
            ShiftTab(-1);
        }
        else if (keyboard.eKey.wasPressedThisFrame)
        {
            ShiftTab(1);
        }

        // ถ้ากระเป๋าเปลี่ยนระหว่างเปิดอยู่ ให้วาดใหม่
        if (isDirty)
        {
            Refresh();
        }
    }

    // ---------- เปิด / ปิด ----------

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (IsOpen) return;

        // ห้ามเปิดกระเป๋าระหว่างเปิดร้าน และระหว่างหน้าสรุปวัน
        if (!CanOpenInventory())
        {
            NotificationUI.Post(unavailableMessage);
            return;
        }

        IsOpen = true;
        SetWindowVisible(true);
        Refresh();

        // หยุดนาฬิกา เฉพาะกรณีที่นาฬิกายังเดินอยู่
        if (pauseClockWhileOpen && GameClock.Instance != null && !GameClock.Instance.IsPaused)
        {
            GameClock.Instance.Pause();
            pausedClockByThis = true;
        }

        // ห้ามเดินระหว่างเปิดกระเป๋า
        if (blockMovementWhileOpen && playerController != null)
        {
            playerController.SetCanMove(false);
        }

        Debug.Log("Inventory opened.");
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        SetWindowVisible(false);

        // ให้นาฬิกาเดินต่อ เฉพาะกรณีที่หน้าต่างนี้เป็นคนหยุดไว้
        if (pausedClockByThis && GameClock.Instance != null)
        {
            GameClock.Instance.Resume();
        }
        pausedClockByThis = false;

        // ให้เดินได้อีกครั้ง
        if (blockMovementWhileOpen && playerController != null)
        {
            playerController.SetCanMove(true);
        }

        Debug.Log("Inventory closed.");
    }

    // ตอนนี้เปิดกระเป๋าได้หรือไม่ (เปิดได้เฉพาะตอนสำรวจ)
    public bool CanOpenInventory()
    {
        if (ShopManager.Instance != null && ShopManager.Instance.IsServing)
        {
            return false;
        }

        if (DayEndManager.Instance != null && DayEndManager.Instance.IsDayEnding)
        {
            return false;
        }

        // ระหว่างเปิดหน้าต่างเลือกเมนู
        if (MenuSelectionUI.IsAnyOpen)
        {
            return false;
        }

        // [2.5] ระหว่างเปิดหน้าต่างทำอาหาร / เล่นมินิเกม
        if (CookingMenuUI.IsAnyOpen || CookingSession.IsBusy)
        {
            return false;
        }

        // [2.5c] ระหว่างเปิดหน้าต่างจดหมาย / [2.6] ช่าง / [2.6b] ร้านค้า + กล่องขาย
        if (MailUI.IsAnyOpen || ConstructionUI.IsAnyOpen || ShopUI.IsAnyOpen || ShippingBoxUI.IsAnyOpen)
        {
            return false;
        }

        return true;
    }

    private void SetWindowVisible(bool visible)
    {
        if (windowRoot != null)
        {
            windowRoot.SetActive(visible);
        }
    }

    // ---------- แท็บ ----------

    public void SetTab(BagTab tab)
    {
        if (CurrentTab != tab)
        {
            CurrentTab = tab;
            selectedItem = null;
            selectedRecipe = null;
            selectedEquipSlot = null;
        }

        Refresh();
    }

    private void ShiftTab(int direction)
    {
        int count = System.Enum.GetValues(typeof(BagTab)).Length;
        int next = ((int)CurrentTab + direction + count) % count;
        SetTab((BagTab)next);
    }

    // ปุ่มของแท็บที่เลือกอยู่จะกดไม่ได้ (ใช้สี Disabled Color ของปุ่มเป็นสีแท็บที่เลือก)
    private void UpdateTabButtons()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] != null)
            {
                tabButtons[i].interactable = i != (int)CurrentTab;
            }
        }

        if (titleText != null)
        {
            int index = (int)CurrentTab;
            titleText.text = tabTitles != null && index < tabTitles.Length ? tabTitles[index] : CurrentTab.ToString();
        }
    }

    // ---------- การวาด ----------

    private void MarkDirty()
    {
        isDirty = true;
    }

    // วาดแท็บปัจจุบันใหม่ทั้งหมด
    public void Refresh()
    {
        isDirty = false;

        if (!IsOpen || slotContainer == null || slotPrefab == null)
        {
            // ยังไม่ได้เปิด: ไว้วาดตอนเปิด
            isDirty = true;
            return;
        }

        UpdateTabButtons();
        UpdateBagLevelText();
        UpdateEquipmentPanel();

        int shown;
        if (CurrentTab == BagTab.Crafting)
        {
            shown = DrawCrafting();
        }
        else
        {
            shown = DrawItems();
        }

        if (emptyTabText != null)
        {
            emptyTabText.gameObject.SetActive(shown == 0);
            emptyTabText.text = CurrentTab == BagTab.Crafting ? emptyCraftingMessage : emptyTabMessage;
        }

        UpdateDetails();
    }

    // วาดไอเทมของแท็บ คืนค่าจำนวนช่องที่แสดง
    // [v2 ผู้ใช้] Hide Undiscovered = ของที่ยังไม่เคยเจอไม่แสดงเลย (ความคืบหน้ายังนับจากของทั้งหมดในแท็บ)
    private int DrawItems()
    {
        RebuildDiscoveredVariantRecipes();
        CollectItemsForTab(CurrentTab, itemBuffer);

        int totalCount = itemBuffer.Count;
        int discoveredCount = 0;
        bool isDishTab = CurrentTab == BagTab.Dishes;

        // คิดสถานะทุกตัวก่อน แล้วตัดตัวที่ไม่แสดงออก
        itemStateBuffer.Clear();
        itemCountBuffer.Clear();
        int write = 0;
        for (int read = 0; read < itemBuffer.Count; read++)
        {
            ItemData item = itemBuffer[read];

            // [v2] แท็บ Dishes: 1 ช่องต่อสูตร จำนวน = รวมทุกแบบทุกคุณภาพ / รู้สูตรแล้ว = ถือว่ารู้จักจานนี้
            int count;
            bool isDiscovered;
            if (isDishTab)
            {
                count = GetDishGroupCount(item);
                isDiscovered = IsDishGroupDiscovered(item) || KnowsRecipeOfDish(item);
            }
            else
            {
                count = inventory != null ? inventory.GetCount(item) : 0;
                isDiscovered = inventory != null && inventory.IsDiscovered(item);
            }

            SlotState state;
            if (count > 0) state = SlotState.Owned;
            else if (isDiscovered) state = SlotState.DiscoveredEmpty;
            else state = SlotState.Undiscovered;

            if (state != SlotState.Undiscovered)
            {
                discoveredCount++;
            }
            else if (hideUndiscovered)
            {
                continue;
            }

            itemBuffer[write++] = item;
            itemStateBuffer.Add(state);
            itemCountBuffer.Add(count);
        }
        itemBuffer.RemoveRange(write, itemBuffer.Count - write);

        EnsureSlotCount(itemBuffer.Count);

        for (int i = 0; i < slots.Count; i++)
        {
            if (i >= itemBuffer.Count)
            {
                slots[i].gameObject.SetActive(false);
                continue;
            }

            ItemData item = itemBuffer[i];

            // [v2] ช่องรวมของสูตรที่มีช่องหมวด ลากลง Hotbar ไม่ได้ (ลากจากแถวแบบ × คุณภาพ ในแผงรายละเอียดแทน)
            slots[i].AllowHotbarDrag = !(isDishTab && HasVariants(item));

            slots[i].gameObject.SetActive(true);
            slots[i].SetItemEntry(item, itemCountBuffer[i], itemStateBuffer[i]);
            slots[i].SetSelected(item == selectedItem && selectedEquipSlot == null);
        }

        if (progressText != null)
        {
            progressText.text = $"Discovered {discoveredCount} / {totalCount}";
        }

        return itemBuffer.Count;
    }

    // [v2 ผู้ใช้] แท็บ Crafting: ยังไม่มีระบบ/สูตรคราฟต์ แสดงว่างไว้ก่อน (เพิ่มสูตรแล้วค่อยวาดที่นี่ + กล่องบอกสูตรตอนวางเมาส์)
    private int DrawCrafting()
    {
        foreach (InventorySlotUI slot in slots)
        {
            slot.gameObject.SetActive(false);
        }

        if (progressText != null)
        {
            progressText.text = "";
        }

        return 0;
    }

    // รวบรวมไอเทมของแท็บ เรียงตามหมวด แล้วตามชื่อ
    private void CollectItemsForTab(BagTab tab, List<ItemData> result)
    {
        result.Clear();

        if (itemDatabase == null)
        {
            return;
        }

        foreach (ItemData item in itemDatabase.Items)
        {
            if (item == null || !IsItemInTab(item, tab))
            {
                continue;
            }

            // แท็บ Dishes: ซ่อนจานของสูตรลับที่ยังไม่รู้
            if (tab == BagTab.Dishes && IsHiddenSecretDish(item))
            {
                continue;
            }

            result.Add(item);
        }

        result.Sort((a, b) =>
        {
            int c = a.category.CompareTo(b.category);
            if (c != 0) return c;
            return string.CompareOrdinal(a.itemName, b.itemName);
        });
    }

    // จานนี้เป็นของสูตรลับที่ยังไม่รู้ และควรซ่อนหรือไม่
    // ไม่ซ่อนถ้า: ปิดตัวเลือกซ่อนไว้ / หาสูตรไม่เจอ / ไม่ใช่สูตรลับ / รู้สูตรแล้ว / มีจานในกระเป๋าหรือเคยได้จานนี้มาแล้ว
    private bool IsHiddenSecretDish(ItemData dish)
    {
        if (!hideUnknownSecretRecipes || recipeDatabase == null)
        {
            return false;
        }

        // itemId ของจาน = recipe_id ของสูตร (ตัว import ตั้งให้)
        RecipeData recipe = recipeDatabase.GetById(dish.itemId);
        if (recipe == null || !recipe.isSecret)
        {
            return false;
        }

        if (recipeBook != null && recipeBook.Knows(recipe))
        {
            return false;
        }

        // เคยได้จานนี้มาแล้ว (เช่น NPC ให้) แสดงไว้ตามแบบ Collectibles / [v2] นับทุกแบบของสูตรนี้
        if (inventory != null && (GetDishGroupCount(dish) > 0 || IsDishGroupDiscovered(dish)))
        {
            return false;
        }

        return true;
    }

    // ---------- [v2] กลุ่มจาน (จานแม่ + ทุกแบบ) ----------

    // สูตรของจานแม่นี้ ถ้าเป็นจานแม่จริง (dishItem ของสูตร) ไม่งั้น null
    private static RecipeData GroupRecipeOf(ItemData dish)
    {
        RecipeData recipe = DishPricing.RecipeOf(dish);
        return recipe != null && recipe.dishItem == dish ? recipe : null;
    }

    // จานนี้เป็นช่องรวมของสูตรที่มีจานแยกแบบหรือไม่
    private static bool HasVariants(ItemData dish)
    {
        RecipeData recipe = GroupRecipeOf(dish);
        return recipe != null && recipe.HasCategorySlot;
    }

    // ใส่จานทุกตัวในกลุ่มที่มีอยู่ในกระเป๋า (จานแม่ก่อน แล้วแบบเรียงตามรหัส) ลง result
    private void CollectDishGroup(ItemData dish, List<ItemData> result)
    {
        result.Clear();
        if (dish == null || inventory == null)
        {
            return;
        }

        RecipeData recipe = GroupRecipeOf(dish);
        if (recipe == null)
        {
            if (inventory.GetCount(dish) > 0)
            {
                result.Add(dish);
            }
            return;
        }

        result.AddRange(DishVariantRegistry.DishItemsIn(recipe, inventory));
    }

    // จำนวนรวมของกลุ่ม (ทุกแบบทุกคุณภาพ)
    private int GetDishGroupCount(ItemData dish)
    {
        if (inventory == null || dish == null)
        {
            return 0;
        }

        RecipeData recipe = GroupRecipeOf(dish);
        if (recipe == null || !recipe.HasCategorySlot)
        {
            return inventory.GetCount(dish);
        }

        int total = 0;
        foreach (KeyValuePair<ItemData, int> pair in inventory.Items)
        {
            if (pair.Key == dish || (pair.Key != null && pair.Key.variantRecipe == recipe))
            {
                total += pair.Value;
            }
        }
        return total;
    }

    // เคยได้จานในกลุ่มนี้มาแล้วหรือยัง (จานแม่ หรือแบบใดก็ได้)
    private bool IsDishGroupDiscovered(ItemData dish)
    {
        if (inventory == null || dish == null)
        {
            return false;
        }

        if (inventory.IsDiscovered(dish))
        {
            return true;
        }

        RecipeData recipe = GroupRecipeOf(dish);
        return recipe != null && discoveredVariantRecipes.Contains(recipe);
    }

    // [v2] รู้สูตรของจานแม่นี้แล้วหรือยัง
    private bool KnowsRecipeOfDish(ItemData dish)
    {
        RecipeData recipe = GroupRecipeOf(dish);
        return recipe != null && recipeBook != null && recipeBook.Knows(recipe);
    }

    // จดสูตรที่เคยได้จานแบบมาแล้ว (อ่านจากรายการของที่เคยเจอ)
    private void RebuildDiscoveredVariantRecipes()
    {
        discoveredVariantRecipes.Clear();
        if (inventory == null)
        {
            return;
        }

        foreach (ItemData item in inventory.DiscoveredItems)
        {
            if (item != null && item.variantRecipe != null)
            {
                discoveredVariantRecipes.Add(item.variantRecipe);
            }
        }
    }

    // ชื่อแบบที่ใช้ในแถว / กล่อง: จานแบบ = ชื่อวัตถุดิบ / จานแม่ของสูตรที่มีช่องหมวด = baseDishLabel / จานธรรมดา = ""
    private string VariantLabel(ItemData dish)
    {
        if (dish == null)
        {
            return "";
        }

        if (dish.variantIngredient != null)
        {
            return dish.variantIngredient.itemName;
        }

        return HasVariants(dish) ? baseDishLabel : "";
    }

    private static bool IsItemInTab(ItemData item, BagTab tab)
    {
        switch (tab)
        {
            case BagTab.Ingredients:
                return item.itemType == ItemType.Ingredient;
            case BagTab.Dishes:
                return item.itemType == ItemType.Dish;
            case BagTab.Equipment:
                return item.itemType == ItemType.Tool || item.itemType == ItemType.Weapon || item.itemType == ItemType.Equipment;
            case BagTab.Accessory:
                return item.itemType == ItemType.Accessory;
            default:
                return false;
        }
    }

    // แท็บนี้แสดงแผงช่องสวมใส่หรือไม่
    private static bool TabShowsEquipment(BagTab tab)
    {
        return tab == BagTab.Equipment || tab == BagTab.Accessory;
    }

    // เปิด/ปิดแผงสวมใส่ตามแท็บ และเลื่อนแผงรายละเอียดลงไปอยู่ใต้แผงสวมใส่
    private void UpdateEquipmentPanel()
    {
        bool show = equipmentPanel != null && TabShowsEquipment(CurrentTab);

        if (equipmentPanel != null)
        {
            equipmentPanel.gameObject.SetActive(show);
            if (show)
            {
                equipmentPanel.Refresh();
                equipmentPanel.SetSelectedSlot(selectedEquipSlot);
            }
        }

        if (detailRect != null)
        {
            Vector2 max = detailRect.offsetMax;
            max.y = show ? detailOriginalTop - detailShiftWithEquipment : detailOriginalTop;
            detailRect.offsetMax = max;
        }
    }

    private void UpdateBagLevelText()
    {
        if (bagLevelText == null || inventory == null)
        {
            return;
        }

        bagLevelText.text = $"Bag Lv.{inventory.BagLevel}  (max {inventory.StackCap} each)";
    }

    // สร้างช่องเพิ่มจนมีครบตามจำนวนที่ต้องการ
    private void EnsureSlotCount(int count)
    {
        while (slots.Count < count)
        {
            InventorySlotUI newSlot = Instantiate(slotPrefab, slotContainer);
            newSlot.name = $"Slot_{slots.Count}";
            newSlot.OnClicked += HandleSlotClicked;
            newSlot.OnHoverEnter += HandleSlotHoverEnter;
            newSlot.OnHoverExit += HandleSlotHoverExit;
            slots.Add(newSlot);
        }
    }

    // ---------- รายละเอียด ----------

    private void HandleSlotClicked(InventorySlotUI slot)
    {
        selectedItem = slot.CurrentItem;
        selectedRecipe = slot.CurrentRecipe;
        selectedEquipSlot = null;

        foreach (InventorySlotUI s in slots)
        {
            s.SetSelected(s == slot);
        }

        if (equipmentPanel != null)
        {
            equipmentPanel.SetSelectedSlot(null);
        }

        UpdateDetails();
    }

    // คลิกช่องสวมใส่: ดูรายละเอียดของที่สวมอยู่ (หรือบอกว่าช่องนี้รับของประเภทไหน ถ้าช่องว่าง)
    private void HandleEquipSlotClicked(EquipmentSlotUI slot)
    {
        selectedEquipSlot = slot;
        selectedItem = slot != null ? slot.CurrentItem : null;
        selectedRecipe = null;

        foreach (InventorySlotUI s in slots)
        {
            s.SetSelected(false);
        }

        if (equipmentPanel != null)
        {
            equipmentPanel.SetSelectedSlot(slot);
        }

        UpdateDetails();
    }

    private void UpdateDetails()
    {
        // ช่องสวมใส่ที่เลือกไว้: อ่านของในช่องใหม่ทุกครั้ง (เผื่อเพิ่งสวม/ถอด)
        if (selectedEquipSlot != null)
        {
            selectedItem = selectedEquipSlot.CurrentItem;
        }

        bool hasSelection = selectedItem != null || selectedRecipe != null || selectedEquipSlot != null;

        if (detailPanel != null)
        {
            detailPanel.SetActive(hasSelection);
        }

        // [v2] แถวจานแสดงเฉพาะตอนเลือกจานที่มีอยู่ในกระเป๋า (ShowItemDetails เปิดเอง)
        HideDishRows();

        if (!hasSelection)
        {
            return;
        }

        if (selectedItem != null)
        {
            ShowItemDetails(selectedItem);
        }
        else if (selectedRecipe != null)
        {
            ShowRecipeDetails(selectedRecipe);
        }
        else
        {
            ShowEmptyEquipSlotDetails(selectedEquipSlot.Slot);
        }
    }

    private void ShowEmptyEquipSlotDetails(EquipSlot slot)
    {
        string kind = PlayerEquipment.IsStatSlot(slot) ? "Stat slot" : "Fashion slot (looks only)";
        SetDetailText($"{PlayerEquipment.GetSlotLabel(slot)} (empty)",
            $"{kind}\n\nDrag a {PlayerEquipment.GetAcceptedType(slot)} item here from your bag to equip it.");
    }

    private void ShowItemDetails(ItemData item)
    {
        // [v2] จาน: แสดงรวมทั้งกลุ่ม + แถวแบบ × คุณภาพ
        if (Inventory.TracksQuality(item) && inventory != null && selectedEquipSlot == null)
        {
            ShowDishDetails(item);
            return;
        }

        bool isDiscovered = inventory != null && inventory.IsDiscovered(item);

        if (!isDiscovered)
        {
            SetDetailText("???", "You haven't found this yet.");
            return;
        }

        int count = inventory.GetCount(item);
        StringBuilder sb = new StringBuilder();

        if (item.itemType == ItemType.Ingredient)
        {
            sb.AppendLine($"{item.category} · {item.source}");
        }
        else
        {
            sb.AppendLine(item.itemType.ToString());
        }

        sb.AppendLine($"Owned: {count} / {inventory.StackCap}");

        // [2.5] จานอาหาร: จำนวนแยกตามคุณภาพ
        if (Inventory.TracksQuality(item) && count > 0)
        {
            sb.AppendLine($"Quality: {BuildQualityText(item, " · ")}");
        }

        // ของสวมใส่: บอกช่องที่ใส่ได้ และจำนวนที่สวมอยู่ (ของที่สวมอยู่ไม่นับในกระเป๋า)
        if (item.IsEquippable)
        {
            bool isStat = item.equipType == EquipType.Shoes || item.equipType == EquipType.Ring || item.equipType == EquipType.Necklace;
            sb.AppendLine($"Equip slot: {item.equipType} ({(isStat ? "stat" : "fashion")})");

            PlayerEquipment equipment = equipmentPanel != null ? equipmentPanel.Equipment : null;
            int equipped = equipment != null ? equipment.CountEquipped(item) : 0;
            if (equipped > 0)
            {
                sb.AppendLine($"Equipped: {equipped}");
            }
        }

        if (item.sellPrice > 0)
        {
            sb.AppendLine($"Sell price: {item.sellPrice}");
        }

        if (!string.IsNullOrWhiteSpace(item.description))
        {
            sb.AppendLine();
            sb.Append(item.description);
        }

        SetDetailText(item.itemName, sb.ToString());
    }

    // [v2] รายละเอียดจาน: จำนวนรวมทุกแบบ + คุณภาพรวม + แถวแบบ × คุณภาพ (กิน / ลากลง Hotbar)
    private void ShowDishDetails(ItemData dish)
    {
        if (!IsDishGroupDiscovered(dish) && !KnowsRecipeOfDish(dish))
        {
            SetDetailText("???", "You haven't found this yet.");
            return;
        }

        CollectDishGroup(dish, groupBuffer);
        int count = GetDishGroupCount(dish);
        bool variants = HasVariants(dish);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(dish.itemType.ToString());

        if (variants)
        {
            sb.AppendLine($"Owned: {count}  (max {inventory.StackCap} per kind)");
        }
        else
        {
            sb.AppendLine($"Owned: {count} / {inventory.StackCap}");
        }

        if (count > 0)
        {
            sb.AppendLine($"Quality: {BuildGroupQualityText(groupBuffer, " · ")}");
        }

        if (dish.sellPrice > 0)
        {
            sb.AppendLine(variants
                ? $"Base price: {dish.sellPrice} (changes with the ingredient)"
                : $"Sell price: {dish.sellPrice}");
        }

        if (!string.IsNullOrWhiteSpace(dish.description))
        {
            sb.AppendLine();
            sb.AppendLine(dish.description);
        }

        // [v2 ผู้ใช้] สูตร (ย้ายมาจากแท็บ Recipes เดิม)
        sb.AppendLine();
        AppendRecipeSection(sb, dish);

        SetDetailText(dish.itemName, sb.ToString().TrimEnd());
        ShowDishRows(groupBuffer);
    }

    // จำนวนแยกตามคุณภาพ รวมทุกจานในกลุ่ม
    private string BuildGroupQualityText(List<ItemData> group, string separator)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < DishQualityUtil.BestFirst.Length; i++)
        {
            DishQuality q = DishQualityUtil.BestFirst[i];
            int n = 0;
            foreach (ItemData item in group)
            {
                n += inventory.GetCount(item, q);
            }
            // [v2 ผู้ใช้] แสดงเฉพาะคุณภาพที่มีในกระเป๋า
            if (n <= 0) continue;
            if (sb.Length > 0) sb.Append(separator);
            sb.Append($"{DishQualityUtil.ColoredLabel(q)} x{n}");
        }
        return sb.ToString();
    }

    // ---------- [v2] แถวแบบ × คุณภาพ ----------

    private void ShowDishRows(List<ItemData> group)
    {
        if (inventory == null || group.Count == 0 || !EnsureDishRowList())
        {
            HideDishRows();
            return;
        }

        int used = 0;
        foreach (ItemData dish in group)
        {
            string variantName = VariantLabel(dish);
            foreach (DishQuality q in DishQualityUtil.BestFirst)
            {
                int n = inventory.GetCount(dish, q);
                if (n <= 0)
                {
                    continue;
                }

                string text = string.IsNullOrEmpty(variantName)
                    ? string.Format(dishRowPlainFormat, DishQualityUtil.ColoredLabel(q), n)
                    : string.Format(dishRowFormat, variantName, DishQualityUtil.ColoredLabel(q), n);

                DishVariantRowUI row = GetDishRow(used);
                row.gameObject.SetActive(true);
                row.Set(dish, q, text);
                used++;
            }
        }

        for (int i = used; i < dishRows.Count; i++)
        {
            dishRows[i].gameObject.SetActive(false);
        }

        if (used == 0)
        {
            HideDishRows();
            return;
        }

        // ความสูงของรายการ = ตามจำนวนแถว ไม่เกินค่าสูงสุด (เกิน = เลื่อนได้)
        float contentHeight = used * dishRowHeight + (used - 1) * dishRowSpacing + 8f;
        float listHeight = Mathf.Min(contentHeight, dishRowListMaxHeight);

        Vector2 min = dishRowListRect.offsetMin;
        Vector2 max = dishRowListRect.offsetMax;
        max.y = min.y + listHeight;
        dishRowListRect.offsetMax = max;
        dishRowListRect.gameObject.SetActive(true);

        // ดันขอบล่างของข้อความรายละเอียดขึ้น ไม่ให้ทับรายการ
        if (detailBodyRect != null)
        {
            Vector2 bodyMin = detailBodyRect.offsetMin;
            bodyMin.y = detailBodyOriginalBottom + listHeight + dishRowListGap;
            detailBodyRect.offsetMin = bodyMin;
        }

        if (!dishRowListVisible && dishRowScroll != null)
        {
            dishRowScroll.verticalNormalizedPosition = 1f;
        }
        dishRowListVisible = true;
    }

    private void HideDishRows()
    {
        if (dishRowListRect != null && dishRowListRect.gameObject.activeSelf)
        {
            dishRowListRect.gameObject.SetActive(false);
        }

        if (detailBodyRect != null && dishRowListVisible)
        {
            Vector2 bodyMin = detailBodyRect.offsetMin;
            bodyMin.y = detailBodyOriginalBottom;
            detailBodyRect.offsetMin = bodyMin;
        }

        dishRowListVisible = false;
    }

    private DishVariantRowUI GetDishRow(int index)
    {
        while (dishRows.Count <= index)
        {
            TMP_FontAsset font = detailBodyText != null ? detailBodyText.font : null;
            DishVariantRowUI row = DishVariantRowUI.Create(dishRowContent, font, dishRowHeight, dishRowFontSize, eatButtonLabel);
            row.name = $"DishVariantRow_{dishRows.Count}";
            row.OnEatClicked += HandleDishRowEat;
            row.OnHoverEnter += HandleDishRowHoverEnter;
            row.OnHoverExit += HandleDishRowHoverExit;
            dishRows.Add(row);
        }
        return dishRows[index];
    }

    private void HandleDishRowEat(DishVariantRowUI row)
    {
        if (row == null || row.Item == null)
        {
            return;
        }

        if (playerStats == null)
        {
            Debug.LogWarning("InventoryUI: Can't eat. Player Stats not found.", this);
            return;
        }

        // กินแล้วกระเป๋าเปลี่ยน → OnChanged → วาดแถวใหม่เอง
        playerStats.TryEat(row.Item, inventory, row.Quality);
    }

    // สร้างรายการแถว (Scroll View เล็กๆ) ที่ด้านล่างของแผงรายละเอียด ใช้ระยะซ้าย/ขวา/ล่างเดียวกับข้อความรายละเอียด
    private bool EnsureDishRowList()
    {
        if (dishRowListRect != null)
        {
            return true;
        }

        if (detailPanel == null)
        {
            return false;
        }

        float left = 20f;
        float right = 20f;
        float bottom = 20f;
        if (detailBodyText != null)
        {
            detailBodyRect = detailBodyText.rectTransform;
            detailBodyOriginalBottom = detailBodyRect.offsetMin.y;
            left = detailBodyRect.offsetMin.x;
            right = -detailBodyRect.offsetMax.x;
            bottom = detailBodyOriginalBottom;
        }

        GameObject listGo = new GameObject("DishVariantList", typeof(RectTransform));
        listGo.transform.SetParent(detailPanel.transform, false);
        listGo.layer = detailPanel.layer;
        dishRowListRect = listGo.GetComponent<RectTransform>();
        dishRowListRect.anchorMin = new Vector2(0f, 0f);
        dishRowListRect.anchorMax = new Vector2(1f, 0f);
        dishRowListRect.pivot = new Vector2(0.5f, 0f);
        dishRowListRect.offsetMin = new Vector2(left, bottom);
        dishRowListRect.offsetMax = new Vector2(-right, bottom + dishRowHeight);

        // พื้นหลังจางๆ (รับเมาส์ เพื่อให้เลื่อนด้วยล้อเมาส์ได้)
        Image bg = listGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.18f);
        bg.raycastTarget = true;
        listGo.AddComponent<RectMask2D>();

        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(listGo.transform, false);
        contentGo.layer = listGo.layer;
        dishRowContent = contentGo.GetComponent<RectTransform>();
        dishRowContent.anchorMin = new Vector2(0f, 1f);
        dishRowContent.anchorMax = new Vector2(1f, 1f);
        dishRowContent.pivot = new Vector2(0.5f, 1f);
        dishRowContent.offsetMin = Vector2.zero;
        dishRowContent.offsetMax = Vector2.zero;

        VerticalLayoutGroup v = contentGo.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(4, 4, 4, 4);
        v.spacing = dishRowSpacing;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        dishRowScroll = listGo.AddComponent<ScrollRect>();
        dishRowScroll.viewport = dishRowListRect;
        dishRowScroll.content = dishRowContent;
        dishRowScroll.horizontal = false;
        dishRowScroll.vertical = true;
        dishRowScroll.movementType = ScrollRect.MovementType.Clamped;
        dishRowScroll.scrollSensitivity = 30f;

        listGo.SetActive(false);
        return true;
    }

    private void ShowRecipeDetails(RecipeData recipe)
    {
        bool knows = recipeBook != null && recipeBook.Knows(recipe);

        if (!knows)
        {
            SetDetailText("???", $"{recipe.cuisine} · {recipe.course}\nRecipe not learned yet.");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"{recipe.cuisine} · {recipe.course}{(recipe.isSecret ? " · Secret" : "")}");
        sb.AppendLine();
        sb.AppendLine("Ingredients:");

        // [v2] ช่องหมวดแสดง "Any Fish" + จำนวนรวมทั้งหมวด
        foreach (RecipeSlot slot in recipe.Slots)
        {
            int have = 0;
            if (inventory != null)
            {
                if (slot.anyOfCategory)
                {
                    foreach (KeyValuePair<ItemData, int> owned in inventory.Items)
                    {
                        if (slot.Accepts(owned.Key)) have += owned.Value;
                    }
                }
                else if (slot.item != null)
                {
                    have = inventory.GetCount(slot.item);
                }
            }
            string mark = have >= 1 ? "+" : "-";
            sb.AppendLine($" {mark} {slot.Label}  (have {have})");
        }

        sb.AppendLine();
        sb.Append($"Can make now: {RecipeDatabase.GetMaxServings(recipe, inventory)}");

        SetDetailText(recipe.recipeName, sb.ToString());
    }

    // ---------- [2.5] กล่องบอกคุณภาพจาน ----------

    private void HandleSlotHoverEnter(InventorySlotUI slot)
    {
        hoveredSlot = slot;
    }

    private void HandleSlotHoverExit(InventorySlotUI slot)
    {
        if (hoveredSlot == slot)
        {
            hoveredSlot = null;
            HideQualityTooltip();
        }
    }

    // ข้อความจำนวนแยกตามคุณภาพ เช่น "Perfect x2 · Good x5" / [v2 ผู้ใช้] แสดงเฉพาะคุณภาพที่มีในกระเป๋า
    private string BuildQualityText(ItemData item, string separator)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < DishQualityUtil.BestFirst.Length; i++)
        {
            DishQuality q = DishQualityUtil.BestFirst[i];
            int n = inventory.GetCount(item, q);
            if (n <= 0) continue;
            if (sb.Length > 0) sb.Append(separator);
            sb.Append($"{DishQualityUtil.ColoredLabel(q)} x{n}");
        }
        return sb.ToString();
    }

    // [v2] ข้อความในกล่อง: จานไม่มีแบบ = แยกคุณภาพแบบเดิม / มีแบบ = บรรทัดละแบบ เช่น "Salmon  Perfect 1 · Good 2"
    private string BuildDishTooltipText(ItemData dish)
    {
        CollectDishGroup(dish, groupBuffer);
        if (!HasVariants(dish))
        {
            return BuildQualityText(dish, "\n");
        }

        StringBuilder sb = new StringBuilder();
        foreach (ItemData item in groupBuffer)
        {
            StringBuilder parts = new StringBuilder();
            foreach (DishQuality q in DishQualityUtil.BestFirst)
            {
                int n = inventory.GetCount(item, q);
                if (n <= 0) continue;
                if (parts.Length > 0) parts.Append(" · ");
                parts.Append($"{DishQualityUtil.ColoredLabel(q)} {n}");
            }

            if (parts.Length == 0) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"{VariantLabel(item)}  {parts}");
        }
        return sb.ToString();
    }

    // [v2 ผู้ใช้] กล่องของช่องจานในตาราง: ชื่อ / สูตร + วัตถุดิบที่มี + ทำได้กี่จาน / แล้วค่อยจำนวนในกระเป๋าแยกแบบ×คุณภาพ (เฉพาะที่มี)
    private string BuildDishSlotTooltipText(ItemData dish)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"<b>{dish.itemName}</b>\n");
        AppendRecipeSection(sb, dish);

        if (GetDishGroupCount(dish) > 0)
        {
            sb.Append($"{inBagHeader}\n").Append(BuildDishTooltipText(dish));
        }

        return sb.ToString().TrimEnd();
    }

    // ส่วนสูตรของจาน: "Recipe (Japan · Main)" / " + Rice  (have 3)" / " - Any Fish  (have 0)" (ขาด = สีแดง) / "Can make now: 2"
    // ยังไม่รู้สูตร (เช่น ได้จานจาก NPC) = "Recipe not learned yet."
    private void AppendRecipeSection(StringBuilder sb, ItemData dish)
    {
        RecipeData recipe = GroupRecipeOf(dish);
        if (recipe == null)
        {
            return;
        }

        bool knows = recipeBook != null && recipeBook.Knows(recipe);
        if (!knows)
        {
            sb.Append($"<color=#FFFFFF99>{recipeNotLearnedText}</color>\n");
            return;
        }

        sb.Append($"Recipe ({recipe.cuisine} · {recipe.course}{(recipe.isSecret ? " · Secret" : "")}):\n");
        foreach (RecipeSlot slot in recipe.Slots)
        {
            int have = CountForSlot(slot);
            string line = $"{slot.Label}  (have {have})";
            sb.Append(have >= 1 ? $" + {line}\n" : $"<color={missingIngredientColor}> - {line}</color>\n");
        }

        int canMake = RecipeDatabase.GetMaxServings(recipe, inventory);
        sb.Append(canMake > 0 ? $"Can make now: {canMake}\n" : $"<color=#FFFFFF99>Can make now: 0</color>\n");
    }

    // จำนวนวัตถุดิบในกระเป๋าที่ใส่ช่องสูตรนี้ได้ (ช่องหมวด = รวมทั้งหมวด)
    private int CountForSlot(RecipeSlot slot)
    {
        if (inventory == null)
        {
            return 0;
        }

        if (slot.anyOfCategory)
        {
            int have = 0;
            foreach (KeyValuePair<ItemData, int> owned in inventory.Items)
            {
                if (slot.Accepts(owned.Key)) have += owned.Value;
            }
            return have;
        }

        return slot.item != null ? inventory.GetCount(slot.item) : 0;
    }

    // ---------- [v2] กล่องค่าฟื้นพลังของแถวจาน ----------

    private void HandleDishRowHoverEnter(DishVariantRowUI row)
    {
        hoveredRow = row;
    }

    private void HandleDishRowHoverExit(DishVariantRowUI row)
    {
        if (hoveredRow == row)
        {
            hoveredRow = null;
            HideQualityTooltip();
        }
    }

    // ข้อความในกล่อง (แบบ Stardew): ชื่อ + คุณภาพ / คำอธิบาย (ถ้ามี) / ค่าที่ได้เมื่อกิน 1 จาน (ตัวเลขเดียวกับตอนกินจริง)
    private string BuildRowTooltipText(DishVariantRowUI row)
    {
        ItemData item = row.Item;
        StringBuilder sb = new StringBuilder();
        sb.Append($"<b>{item.itemName}</b> · {DishQualityUtil.ColoredLabel(row.Quality)}");

        if (!string.IsNullOrWhiteSpace(item.description))
        {
            sb.Append($"\n<size=85%>{item.description}</size>");
        }

        DishPricing.EatValues(item, row.Quality, out int health, out int stamina, out int mana, out int fever);
        int before = sb.Length;
        if (health > 0) sb.Append($"\n<color=#FF7A7A>Health +{health}</color>");
        if (stamina > 0) sb.Append($"\n<color=#FFD166>Stamina +{stamina}</color>");
        if (mana > 0) sb.Append($"\n<color=#7AB8FF>Mana +{mana}</color>");
        if (fever > 0) sb.Append($"\n<color=#FF9BE0>FEVER +{fever}</color>");
        if (sb.Length == before) sb.Append($"\n<color=#FFFFFF99>{noRestoreText}</color>");

        return sb.ToString();
    }

    private void UpdateQualityTooltip()
    {
        if (!IsOpen || inventory == null)
        {
            HideQualityTooltip();
            return;
        }

        // [v2] แถวจานในแผงรายละเอียดมาก่อน / ไม่ได้วางบนแถว = กล่องคุณภาพของช่องในตาราง (แบบเดิม)
        string text = null;
        if (showRowRestoreTooltip && hoveredRow != null && hoveredRow.isActiveAndEnabled && hoveredRow.Item != null)
        {
            text = BuildRowTooltipText(hoveredRow);
        }
        else if (showQualityTooltip && hoveredSlot != null && hoveredSlot.isActiveAndEnabled)
        {
            ItemData item = hoveredSlot.CurrentItem;
            if (item != null && Inventory.TracksQuality(item) && hoveredSlot.CurrentState != SlotState.Undiscovered)
            {
                text = BuildDishSlotTooltipText(item);
            }
        }

        if (text == null)
        {
            HideQualityTooltip();
            return;
        }

        if (!EnsureQualityTooltip())
        {
            return;
        }

        tooltipText.text = text;
        if (!tooltipRect.gameObject.activeSelf)
        {
            tooltipRect.gameObject.SetActive(true);
        }
        tooltipRect.SetAsLastSibling();

        // ตามเมาส์
        Mouse mouse = Mouse.current;
        RectTransform canvasRect = tooltipCanvas.transform as RectTransform;
        if (mouse != null && canvasRect != null)
        {
            Camera cam = tooltipCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : tooltipCanvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouse.position.ReadValue(), cam, out Vector2 local))
            {
                tooltipRect.anchoredPosition = local + tooltipOffset;
            }
        }
    }

    private void HideQualityTooltip()
    {
        if (tooltipRect != null && tooltipRect.gameObject.activeSelf)
        {
            tooltipRect.gameObject.SetActive(false);
        }
    }

    // สร้างกล่อง (พื้นหลังเข้ม + ข้อความ ขยายตามเนื้อหา) บน Canvas หลักของหน้าต่างกระเป๋า
    private bool EnsureQualityTooltip()
    {
        if (tooltipRect != null)
        {
            return true;
        }

        Canvas canvas = windowRoot != null ? windowRoot.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return false;
        }
        tooltipCanvas = canvas.rootCanvas;

        GameObject box = new GameObject("DishQualityTooltip", typeof(RectTransform));
        box.transform.SetParent(tooltipCanvas.transform, false);
        box.layer = tooltipCanvas.gameObject.layer;
        tooltipRect = box.GetComponent<RectTransform>();
        tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0f, 1f);

        Image bg = box.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.07f, 0.92f);
        bg.raycastTarget = false;

        VerticalLayoutGroup layout = box.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = box.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(box.transform, false);
        textGo.layer = box.layer;
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = tooltipFontSize;
        text.raycastTarget = false;
        tooltipText = text;

        box.SetActive(false);
        return true;
    }

    private void SetDetailText(string title, string body)
    {
        if (detailNameText != null)
        {
            detailNameText.text = title;
        }

        if (detailBodyText != null)
        {
            detailBodyText.text = body;
        }
    }
}
