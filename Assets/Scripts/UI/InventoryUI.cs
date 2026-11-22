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
    Recipes,
    Equipment,
    Accessory
}

// หน้าต่างกระเป๋าแบบ Collectibles: แสดงไอเทมทุกชนิดในเกมตามแท็บ
// ของที่เคยเจอแสดงไอคอนและจำนวน ของที่ยังไม่เคยเจอแสดงเป็นเงาดำ คลิกช่องเพื่อดูรายละเอียด
// กด I เปิด/ปิด, Esc ปิด, Q / E เปลี่ยนแท็บ
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
    [Tooltip("ปุ่มแท็บ 5 ปุ่ม เรียงตามลำดับ: Ingredients, Dishes, Recipes, Equipment, Accessory")]
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

    [Tooltip("ซ่อนสูตรลับที่ยังไม่รู้ (ไม่แสดงแม้แต่เงาดำ) ทั้งในแท็บ Recipes และจานของสูตรนั้นในแท็บ Dishes\nจานจะกลับมาแสดงเมื่อรู้สูตร หรือเคยได้จานนั้นมาแล้ว")]
    [SerializeField] private bool hideUnknownSecretRecipes = true;

    [Tooltip("แท็บที่เปิดเป็นแท็บแรก")]
    [SerializeField] private BagTab startingTab = BagTab.Ingredients;

    [Header("Messages")]
    [Tooltip("ข้อความแจ้งเตือนตอนเปิดกระเป๋าไม่ได้ (เช่น ระหว่างเปิดร้าน)")]
    [SerializeField] private string unavailableMessage = "You can't open your bag right now.";

    [Tooltip("ข้อความในแท็บที่ยังไม่มีไอเทม")]
    [SerializeField] private string emptyTabMessage = "Nothing here yet.";

    // หน้าต่างเปิดอยู่หรือไม่
    public bool IsOpen { get; private set; }

    // แท็บที่กำลังดูอยู่
    public BagTab CurrentTab { get; private set; }

    // ช่องที่สร้างไว้แล้ว นำกลับมาใช้ซ้ำ
    private readonly List<InventorySlotUI> slots = new List<InventorySlotUI>();

    // รายการของแท็บปัจจุบัน (ใช้ซ้ำ ไม่สร้างใหม่ทุกครั้ง)
    private readonly List<ItemData> itemBuffer = new List<ItemData>();
    private readonly List<RecipeData> recipeBuffer = new List<RecipeData>();

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
        if (CurrentTab == BagTab.Recipes)
        {
            shown = DrawRecipes();
        }
        else
        {
            shown = DrawItems();
        }

        if (emptyTabText != null)
        {
            emptyTabText.gameObject.SetActive(shown == 0);
            emptyTabText.text = emptyTabMessage;
        }

        UpdateDetails();
    }

    // วาดไอเทมของแท็บ คืนค่าจำนวนช่องที่แสดง
    private int DrawItems()
    {
        CollectItemsForTab(CurrentTab, itemBuffer);
        EnsureSlotCount(itemBuffer.Count);

        int discoveredCount = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            if (i >= itemBuffer.Count)
            {
                slots[i].gameObject.SetActive(false);
                continue;
            }

            ItemData item = itemBuffer[i];
            int count = inventory != null ? inventory.GetCount(item) : 0;
            bool isDiscovered = inventory != null && inventory.IsDiscovered(item);

            SlotState state;
            if (count > 0) state = SlotState.Owned;
            else if (isDiscovered) state = SlotState.DiscoveredEmpty;
            else state = SlotState.Undiscovered;

            if (state != SlotState.Undiscovered)
            {
                discoveredCount++;
            }

            slots[i].gameObject.SetActive(true);
            slots[i].SetItemEntry(item, count, state);
            slots[i].SetSelected(item == selectedItem && selectedEquipSlot == null);
        }

        if (progressText != null)
        {
            progressText.text = $"Discovered {discoveredCount} / {itemBuffer.Count}";
        }

        return itemBuffer.Count;
    }

    // วาดสูตรอาหาร คืนค่าจำนวนช่องที่แสดง
    private int DrawRecipes()
    {
        recipeBuffer.Clear();

        if (recipeDatabase != null)
        {
            foreach (RecipeData recipe in recipeDatabase.Recipes)
            {
                if (recipe == null)
                {
                    continue;
                }

                bool knows = recipeBook != null && recipeBook.Knows(recipe);
                if (!knows && recipe.isSecret && hideUnknownSecretRecipes)
                {
                    continue;
                }

                recipeBuffer.Add(recipe);
            }
        }

        // เรียงตามประเทศ แล้วตามประเภท แล้วตามชื่อ
        recipeBuffer.Sort((a, b) =>
        {
            int c = a.cuisine.CompareTo(b.cuisine);
            if (c != 0) return c;
            c = a.course.CompareTo(b.course);
            if (c != 0) return c;
            return string.CompareOrdinal(a.recipeName, b.recipeName);
        });

        EnsureSlotCount(recipeBuffer.Count);

        int knownCount = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            if (i >= recipeBuffer.Count)
            {
                slots[i].gameObject.SetActive(false);
                continue;
            }

            RecipeData recipe = recipeBuffer[i];
            bool knows = recipeBook != null && recipeBook.Knows(recipe);
            if (knows)
            {
                knownCount++;
            }

            // ตัวเลขมุมขวาบน = จำนวนจานที่วัตถุดิบในกระเป๋าทำได้ตอนนี้
            int servings = knows ? RecipeDatabase.GetMaxServings(recipe, inventory) : 0;

            slots[i].gameObject.SetActive(true);
            slots[i].SetRecipeEntry(recipe, servings, knows ? SlotState.Owned : SlotState.Undiscovered);
            slots[i].SetSelected(recipe == selectedRecipe);
        }

        if (progressText != null)
        {
            progressText.text = $"Known {knownCount} / {recipeBuffer.Count}";
        }

        return recipeBuffer.Count;
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

        // เคยได้จานนี้มาแล้ว (เช่น NPC ให้) แสดงไว้ตามแบบ Collectibles
        if (inventory != null && (inventory.GetCount(dish) > 0 || inventory.IsDiscovered(dish)))
        {
            return false;
        }

        return true;
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

        foreach (KeyValuePair<ItemData, int> pair in recipe.GetRequiredCounts())
        {
            int have = inventory != null ? inventory.GetCount(pair.Key) : 0;
            string mark = have >= pair.Value ? "+" : "-";
            sb.AppendLine($" {mark} {pair.Key.itemName} x{pair.Value}  (have {have})");
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

    // ข้อความจำนวนแยกตามคุณภาพ เช่น "Perfect x2 · Good x5 · OK x0" (ตัวที่เป็น 0 สีจาง)
    private string BuildQualityText(ItemData item, string separator)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < DishQualityUtil.BestFirst.Length; i++)
        {
            DishQuality q = DishQualityUtil.BestFirst[i];
            int n = inventory.GetCount(item, q);
            string part = $"{DishQualityUtil.ColoredLabel(q)} x{n}";
            if (n == 0) part = $"<color=#FFFFFF66>{DishQualityUtil.Label(q)} x0</color>";
            if (i > 0) sb.Append(separator);
            sb.Append(part);
        }
        return sb.ToString();
    }

    private void UpdateQualityTooltip()
    {
        if (!showQualityTooltip || !IsOpen || hoveredSlot == null || !hoveredSlot.isActiveAndEnabled || inventory == null)
        {
            HideQualityTooltip();
            return;
        }

        ItemData item = hoveredSlot.CurrentItem;
        if (item == null || !Inventory.TracksQuality(item) || hoveredSlot.CurrentState == SlotState.Undiscovered || inventory.GetCount(item) <= 0)
        {
            HideQualityTooltip();
            return;
        }

        if (!EnsureQualityTooltip())
        {
            return;
        }

        tooltipText.text = BuildQualityText(item, "\n");
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
