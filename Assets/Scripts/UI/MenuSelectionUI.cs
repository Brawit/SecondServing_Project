using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// หน้าต่างเลือกเมนูประจำวัน: เปิดเมื่อกด E ที่เคาน์เตอร์ช่วงเปิดร้านได้ (17:00–19:00)
// - ฝั่งซ้าย: ช่องเมนู (− / + ตั้งจำนวนจาน, กด Shift ค้าง = ทีละ 5, ✕ เอาออก)
// - ฝั่งขวา: สูตรที่รู้ คลิกเพื่อใส่ลงช่องว่าง
// - ปุ่ม Open Shop กดได้เมื่อมีอย่างน้อย 1 จาน / Esc หรือ Cancel = ปิดโดยยังไม่เปิดร้าน
// - [2.4b] บรรทัดเมนูนิยมวันนี้ใต้หัวข้อ (สูตรที่ยังไม่รู้แสดงเป็นคำใบ้) และเมนูนิยมในรายการสูตรขึ้นก่อน
// ระหว่างเปิดหน้าต่าง: นาฬิกาหยุด เดินไม่ได้ เปิดกระเป๋าไม่ได้ ใช้ Hotbar ไม่ได้
// หมายเหตุ: วางสคริปต์นี้บนวัตถุที่เปิดอยู่ตลอด (ไม่ใช่ตัวหน้าต่างที่ถูกซ่อน)
// สร้างและผูกอัตโนมัติได้จากเมนู Tools > Second Serving > Build Daily Menu Window
public class MenuSelectionUI : MonoBehaviour
{
    public static MenuSelectionUI Instance { get; private set; }

    // มีหน้าต่างเลือกเมนูเปิดอยู่หรือไม่ (ระบบอื่นใช้เช็กเพื่อล็อกปุ่ม)
    public static bool IsAnyOpen => Instance != null && Instance.IsOpen;

    [Header("References")]
    [Tooltip("เมนูประจำวัน (คอมโพเนนต์ DailyMenu)")]
    [SerializeField] private DailyMenu dailyMenu;

    [Tooltip("ตัวควบคุมการเดินของผู้เล่น ใช้หยุดเดินตอนเปิดหน้าต่าง")]
    [SerializeField] private PlayerController playerController;

    [Header("Window")]
    [Tooltip("ตัวหน้าต่างที่จะซ่อน/แสดง")]
    [SerializeField] private GameObject windowRoot;

    [Tooltip("ที่วางแถวช่องเมนู (มี Vertical Layout Group)")]
    [SerializeField] private Transform slotContainer;

    [Tooltip("แม่แบบแถวช่องเมนู (ปิดไว้ จะถูกคัดลอกตามจำนวนช่อง)")]
    [SerializeField] private MenuSlotRowUI slotRowTemplate;

    [Tooltip("ที่วางรายการสูตร (Content ของ Scroll View)")]
    [SerializeField] private Transform recipeContainer;

    [Tooltip("แม่แบบรายการสูตร (ปิดไว้ จะถูกคัดลอกตามจำนวนสูตรที่รู้)")]
    [SerializeField] private MenuRecipeItemUI recipeItemTemplate;

    [Tooltip("ข้อความรวมจำนวนจาน (เว้นว่างได้)")]
    [SerializeField] private TMP_Text totalText;

    [Tooltip("[2.4b] ข้อความเมนูนิยมวันนี้ (เว้นว่างได้)")]
    [SerializeField] private TMP_Text popularText;

    [Tooltip("[2.4b] ข้อความนำหน้าเมนูนิยม")]
    [SerializeField] private string popularLabel = "Popular today:";

    [SerializeField] private Button openShopButton;
    [SerializeField] private Button cancelButton;

    [Header("Settings")]
    [Tooltip("กด Shift ค้างแล้วกด − / + จะเปลี่ยนทีละเท่านี้")]
    [Min(1)] [SerializeField] private int shiftStep = 5;

    [Tooltip("หยุดนาฬิการะหว่างเปิดหน้าต่าง")]
    [SerializeField] private bool pauseClockWhileOpen = true;

    public bool IsOpen { get; private set; }

    private readonly List<MenuSlotRowUI> rows = new List<MenuSlotRowUI>();
    private readonly List<MenuRecipeItemUI> recipeItems = new List<MenuRecipeItemUI>();
    private readonly List<RecipeData> recipeBuffer = new List<RecipeData>();

    private bool isDirty = true;
    private bool pausedClockByThis;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("MenuSelectionUI: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dailyMenu == null) dailyMenu = FindFirstObjectByType<DailyMenu>(FindObjectsInactive.Include);
        if (dailyMenu == null) Debug.LogWarning("MenuSelectionUI: Daily Menu is not assigned.", this);
        if (playerController == null) Debug.LogWarning("MenuSelectionUI: Player Controller is not assigned.", this);
        if (windowRoot == null) Debug.LogWarning("MenuSelectionUI: Window Root is not assigned.", this);
        if (windowRoot == gameObject) Debug.LogWarning("MenuSelectionUI: Do not put this script on the Window Root itself. Put it on its parent.", this);
        if (slotRowTemplate == null) Debug.LogWarning("MenuSelectionUI: Slot Row Template is not assigned.", this);
        if (recipeItemTemplate == null) Debug.LogWarning("MenuSelectionUI: Recipe Item Template is not assigned.", this);

        // แม่แบบไม่ใช่แถวจริง ซ่อนไว้
        if (slotRowTemplate != null) slotRowTemplate.gameObject.SetActive(false);
        if (recipeItemTemplate != null) recipeItemTemplate.gameObject.SetActive(false);

        if (openShopButton != null) openShopButton.onClick.AddListener(ConfirmAndOpenShop);
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        if (dailyMenu != null)
        {
            dailyMenu.OnMenuChanged += MarkDirty;
            if (dailyMenu.Inventory != null) dailyMenu.Inventory.OnChanged += MarkDirty;
        }
    }

    private void OnDisable()
    {
        if (dailyMenu != null)
        {
            dailyMenu.OnMenuChanged -= MarkDirty;
            if (dailyMenu.Inventory != null) dailyMenu.Inventory.OnChanged -= MarkDirty;
        }
    }

    private void OnDestroy()
    {
        if (PopularDishes.Instance != null)
        {
            PopularDishes.Instance.OnPopularChanged -= MarkDirty;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        SetWindowVisible(false);
        IsOpen = false;

        if (PopularDishes.Instance != null)
        {
            PopularDishes.Instance.OnPopularChanged += MarkDirty;
        }
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (isDirty)
        {
            Refresh();
        }
    }

    // ---------- เปิด / ปิด ----------

    // เปิดหน้าต่าง คืนค่า false ถ้าเปิดไม่ได้ (ShopCounter เรียก)
    public bool Open()
    {
        if (IsOpen)
        {
            return true;
        }

        if (dailyMenu == null || windowRoot == null)
        {
            return false;
        }

        if (dailyMenu.IsActive)
        {
            return false;
        }

        // ตรวจเมนูเมื่อวานกับของในกระเป๋าตอนนี้ (ลดจำนวนที่เกิน ล้างสูตรที่ไม่รู้)
        dailyMenu.Revalidate();

        IsOpen = true;
        SetWindowVisible(true);
        Refresh();

        if (pauseClockWhileOpen && GameClock.Instance != null && !GameClock.Instance.IsPaused)
        {
            GameClock.Instance.Pause();
            pausedClockByThis = true;
        }

        if (playerController != null)
        {
            playerController.SetCanMove(false);
        }

        Debug.Log("MenuSelectionUI: Opened.");
        return true;
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        SetWindowVisible(false);

        if (pausedClockByThis && GameClock.Instance != null)
        {
            GameClock.Instance.Resume();
        }
        pausedClockByThis = false;

        if (playerController != null)
        {
            playerController.SetCanMove(true);
        }

        Debug.Log("MenuSelectionUI: Closed.");
    }

    // ปุ่ม Open Shop: ปิดหน้าต่างก่อน (ให้นาฬิกาเดินต่อ) แล้วค่อยเปิดร้าน
    // ต้องปิดก่อน เพราะร้านจะหยุดนาฬิกาเอง ถ้าหน้าต่างยังหยุดค้างไว้ ร้านจะไม่ได้เป็นคนหยุด และปล่อยนาฬิกาไม่ได้ตอนปิดร้าน
    private void ConfirmAndOpenShop()
    {
        if (dailyMenu == null)
        {
            return;
        }

        if (dailyMenu.TotalPlanned <= 0)
        {
            // ปุ่มควรกดไม่ได้อยู่แล้ว แต่กันไว้
            dailyMenu.TryConfirmAndOpenShop();
            return;
        }

        Close();
        dailyMenu.TryConfirmAndOpenShop();
    }

    private void SetWindowVisible(bool visible)
    {
        if (windowRoot != null)
        {
            windowRoot.SetActive(visible);
        }
    }

    // ---------- การวาด ----------

    private void MarkDirty()
    {
        isDirty = true;
    }

    public void Refresh()
    {
        isDirty = false;

        if (!IsOpen || dailyMenu == null)
        {
            isDirty = true;
            return;
        }

        DrawSlots();
        DrawRecipes();
        DrawPopular();

        int total = dailyMenu.TotalPlanned;
        if (totalText != null)
        {
            totalText.text = $"Total: {total} serving{(total == 1 ? "" : "s")}";
        }

        if (openShopButton != null)
        {
            openShopButton.interactable = total > 0;
        }
    }

    private void DrawSlots()
    {
        if (slotContainer == null || slotRowTemplate == null)
        {
            return;
        }

        int count = dailyMenu.SlotCount;

        while (rows.Count < count)
        {
            MenuSlotRowUI row = Instantiate(slotRowTemplate, slotContainer);
            row.name = $"MenuSlot_{rows.Count + 1}";
            row.OnMinus += HandleMinus;
            row.OnPlus += HandlePlus;
            row.OnRemove += HandleRemove;
            rows.Add(row);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < count;
            rows[i].gameObject.SetActive(used);
            if (!used)
            {
                continue;
            }

            DailyMenu.MenuEntry entry = dailyMenu.GetEntry(i);
            int max = dailyMenu.GetMaxServings(i);
            int dishes = entry != null && !entry.IsEmpty ? dailyMenu.GetDishesInBag(entry.Recipe) : 0;

            rows[i].SetIndex(i);
            rows[i].Show(entry, max, dishes);
        }
    }

    private void DrawRecipes()
    {
        if (recipeContainer == null || recipeItemTemplate == null)
        {
            return;
        }

        recipeBuffer.Clear();
        RecipeBook book = dailyMenu.RecipeBook;
        if (book != null)
        {
            foreach (RecipeData recipe in book.KnownRecipes)
            {
                if (recipe != null)
                {
                    recipeBuffer.Add(recipe);
                }
            }
        }

        // คำนวณจำนวนที่ทำได้ครั้งเดียว แล้วเรียง: ทำได้ก่อน แล้วเมนูนิยม แล้วตามประเทศ ประเภท ชื่อ
        PopularDishes popular = PopularDishes.Instance;
        Dictionary<RecipeData, int> available = new Dictionary<RecipeData, int>();
        HashSet<RecipeData> popularSet = new HashSet<RecipeData>();
        foreach (RecipeData recipe in recipeBuffer)
        {
            available[recipe] = dailyMenu.GetAvailableServings(recipe);
            if (popular != null && popular.IsPopular(recipe)) popularSet.Add(recipe);
        }

        recipeBuffer.Sort((a, b) =>
        {
            bool aOk = available[a] > 0;
            bool bOk = available[b] > 0;
            if (aOk != bOk) return aOk ? -1 : 1;
            bool aPop = popularSet.Contains(a);
            bool bPop = popularSet.Contains(b);
            if (aPop != bPop) return aPop ? -1 : 1;
            int c = a.cuisine.CompareTo(b.cuisine);
            if (c != 0) return c;
            c = a.course.CompareTo(b.course);
            if (c != 0) return c;
            return string.CompareOrdinal(a.recipeName, b.recipeName);
        });

        while (recipeItems.Count < recipeBuffer.Count)
        {
            MenuRecipeItemUI item = Instantiate(recipeItemTemplate, recipeContainer);
            item.name = $"Recipe_{recipeItems.Count}";
            item.OnClicked += HandleRecipeClicked;
            recipeItems.Add(item);
        }

        for (int i = 0; i < recipeItems.Count; i++)
        {
            bool used = i < recipeBuffer.Count;
            recipeItems[i].gameObject.SetActive(used);
            if (!used)
            {
                continue;
            }

            RecipeData recipe = recipeBuffer[i];
            string marker = popular != null ? popular.Marker : "*";
            recipeItems[i].Show(recipe, available[recipe], dailyMenu.IndexOf(recipe) >= 0, popularSet.Contains(recipe), marker);
        }
    }

    // [2.4b] บรรทัดเมนูนิยมวันนี้
    private void DrawPopular()
    {
        if (popularText == null)
        {
            return;
        }

        PopularDishes popular = PopularDishes.Instance;
        string summary = popular != null ? popular.GetSummaryText() : "";
        popularText.text = string.IsNullOrEmpty(summary) ? "" : $"{popularLabel}  {summary}";
    }

    // ---------- ปุ่ม ----------

    private int CurrentStep()
    {
        Keyboard keyboard = Keyboard.current;
        bool shift = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        return shift ? shiftStep : 1;
    }

    private void HandleMinus(int index)
    {
        dailyMenu.ChangePlanned(index, -CurrentStep());
    }

    private void HandlePlus(int index)
    {
        dailyMenu.ChangePlanned(index, CurrentStep());
    }

    private void HandleRemove(int index)
    {
        dailyMenu.ClearEntry(index);
    }

    private void HandleRecipeClicked(RecipeData recipe)
    {
        dailyMenu.TryAddRecipe(recipe);
    }
}
