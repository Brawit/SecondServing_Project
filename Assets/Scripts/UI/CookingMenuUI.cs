using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// [2.5] หน้าต่างทำอาหารนอกเวลาร้าน: เปิดเมื่อกด E ที่เตาตอนร้านไม่ได้เปิด
// - แสดงสูตรที่รู้และวัตถุดิบในกระเป๋าพอทำ คลิก = ปิดหน้าต่างแล้วเริ่มมินิเกม (จานเข้ากระเป๋าพร้อมคุณภาพ)
// - Esc หรือปุ่ม Cancel = ปิด
// - [v2] สูตรที่มีช่องหมวด: เลือกวัตถุดิบด้วยลูกศรในแถว / ค่าเริ่มต้น = ตัวที่เลือกล่าสุด (RecipeBook) ถ้ายังพอ ไม่งั้นตัวที่มีมากสุด
//   กดทำแล้วจำตัวนั้นเป็น "ตัวล่าสุด" ของสูตร (เซฟด้วย) / เปลี่ยนลูกศรเฉยๆ ไม่ปิดหน้าต่าง = จำไว้แค่ตอนหน้าต่างเปิดอยู่
// ระหว่างเปิดหน้าต่าง: เดินไม่ได้ เปิดกระเป๋าไม่ได้ ใช้ Hotbar ไม่ได้ / นาฬิกาเดินปกติ (ไม่หยุด)
// หมายเหตุ: วางสคริปต์นี้บนวัตถุที่เปิดอยู่ตลอด (ไม่ใช่ตัวหน้าต่างที่ถูกซ่อน)
// สร้างและผูกอัตโนมัติได้จาก Tools > Second Serving > Build Cooking System
public class CookingMenuUI : MonoBehaviour
{
    public static CookingMenuUI Instance { get; private set; }

    // มีหน้าต่างทำอาหารเปิดอยู่หรือไม่ (ระบบอื่นใช้เช็กเพื่อล็อกปุ่ม)
    public static bool IsAnyOpen => Instance != null && Instance.IsOpen;

    [Header("References")]
    [Tooltip("กระเป๋าของผู้เล่น")]
    [SerializeField] private Inventory inventory;

    [Tooltip("สมุดสูตรของผู้เล่น")]
    [SerializeField] private RecipeBook recipeBook;

    [Tooltip("ตัวควบคุมการเดินของผู้เล่น ใช้หยุดเดินตอนเปิดหน้าต่าง")]
    [SerializeField] private PlayerController playerController;

    [Header("Window")]
    [Tooltip("ตัวหน้าต่างที่จะซ่อน/แสดง")]
    [SerializeField] private GameObject windowRoot;

    [Tooltip("ที่วางรายการสูตร (Content ของ Scroll View)")]
    [SerializeField] private Transform listContainer;

    [Tooltip("แม่แบบรายการสูตร (ปิดไว้ จะถูกคัดลอกตามจำนวนสูตร)")]
    [SerializeField] private CookingRecipeItemUI itemTemplate;

    [Tooltip("ข้อความตอนไม่มีสูตรที่ทำได้ (เว้นว่างได้)")]
    [SerializeField] private TMP_Text emptyText;

    [SerializeField] private Button cancelButton;

    [Header("Text")]
    [SerializeField] private string emptyMessage = "Nothing you can cook with what's in your bag.";

    public bool IsOpen { get; private set; }

    private readonly List<CookingRecipeItemUI> items = new List<CookingRecipeItemUI>();
    private readonly List<RecipeData> buffer = new List<RecipeData>();
    private bool isDirty = true;

    // [v2] ตัวที่ผู้เล่นเลื่อนเลือกไว้ระหว่างหน้าต่างเปิดอยู่ (ล้างทุกครั้งที่เปิดใหม่)
    private readonly Dictionary<RecipeData, ItemData> pendingChoices = new Dictionary<RecipeData, ItemData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("CookingMenuUI: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (inventory == null || recipeBook == null || playerController == null)
        {
            PlayerInteractor player = FindFirstObjectByType<PlayerInteractor>();
            if (player != null)
            {
                if (inventory == null) inventory = player.GetComponent<Inventory>();
                if (recipeBook == null) recipeBook = player.GetComponent<RecipeBook>();
                if (playerController == null) playerController = player.GetComponent<PlayerController>();
            }
        }

        if (inventory == null) Debug.LogWarning("CookingMenuUI: Inventory is not assigned.", this);
        if (recipeBook == null) Debug.LogWarning("CookingMenuUI: Recipe Book is not assigned.", this);
        if (windowRoot == null) Debug.LogWarning("CookingMenuUI: Window Root is not assigned.", this);
        if (windowRoot == gameObject) Debug.LogWarning("CookingMenuUI: Do not put this script on the Window Root itself. Put it on its parent.", this);
        if (itemTemplate == null) Debug.LogWarning("CookingMenuUI: Item Template is not assigned.", this);

        if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        if (inventory != null) inventory.OnChanged += MarkDirty;
        if (recipeBook != null) recipeBook.OnChanged += MarkDirty;
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.OnChanged -= MarkDirty;
        if (recipeBook != null) recipeBook.OnChanged -= MarkDirty;
    }

    private void OnDestroy()
    {
        if (DayEndManager.Instance != null) DayEndManager.Instance.OnDayEndStarted -= HandleDayEndStarted;
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        SetWindowVisible(false);
        IsOpen = false;

        // สลบระหว่างเปิดหน้าต่าง (ถึง 03:00) = ปิดหน้าต่าง โดยไม่ปล่อยการเดิน (DayEndManager ล็อกผู้เล่นไว้)
        if (DayEndManager.Instance != null) DayEndManager.Instance.OnDayEndStarted += HandleDayEndStarted;
    }

    private void HandleDayEndStarted(DayEndReason reason)
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        SetWindowVisible(false);
        Debug.Log("CookingMenuUI: Closed by day end.");
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        // ร้านเปิดระหว่างนี้ไม่ได้อยู่แล้ว (เปิดร้านต้องกดที่เคาน์เตอร์) แต่กันไว้
        if (ShopManager.Instance != null && ShopManager.Instance.IsServing)
        {
            Close();
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

    private void MarkDirty()
    {
        isDirty = true;
    }

    // ---------- เปิด / ปิด ----------

    public bool Open()
    {
        if (IsOpen)
        {
            return true;
        }

        if (windowRoot == null || CookingSession.IsBusy)
        {
            return false;
        }

        IsOpen = true;
        pendingChoices.Clear();
        SetWindowVisible(true);
        Refresh();

        if (playerController != null)
        {
            playerController.SetCanMove(false);
        }

        Debug.Log("CookingMenuUI: Opened.");
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

        if (playerController != null)
        {
            playerController.SetCanMove(true);
        }

        Debug.Log("CookingMenuUI: Closed.");
    }

    private void SetWindowVisible(bool visible)
    {
        if (windowRoot != null)
        {
            windowRoot.SetActive(visible);
        }
    }

    // ---------- รายการ ----------

    private void Refresh()
    {
        isDirty = false;

        buffer.Clear();
        if (recipeBook != null)
        {
            foreach (RecipeData recipe in recipeBook.KnownRecipes)
            {
                if (recipe != null && recipe.dishItem != null && RecipeDatabase.GetMaxServings(recipe, inventory) > 0)
                {
                    buffer.Add(recipe);
                }
            }
        }

        // เรียงตามประเภท แล้วตามชื่อ
        buffer.Sort((a, b) =>
        {
            int byCourse = a.course.CompareTo(b.course);
            return byCourse != 0 ? byCourse : string.Compare(a.recipeName, b.recipeName, System.StringComparison.Ordinal);
        });

        EnsureItemCount(buffer.Count);
        for (int i = 0; i < items.Count; i++)
        {
            bool used = i < buffer.Count;
            items[i].gameObject.SetActive(used);
            if (used)
            {
                RecipeData recipe = buffer[i];
                items[i].Show(recipe, RecipeDatabase.GetMaxServings(recipe, inventory), CountDishesInBag(recipe), inventory, ChoiceFor(recipe));
            }
        }

        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(buffer.Count == 0);
            emptyText.text = emptyMessage;
        }
    }

    // [v2] จานของสูตรนี้ที่ทำไว้แล้วในกระเป๋า (จานแม่ + จานแบบทุกตัว)
    private int CountDishesInBag(RecipeData recipe)
    {
        if (inventory == null)
        {
            return 0;
        }

        int total = 0;
        foreach (ItemData dish in DishVariantRegistry.DishItemsIn(recipe, inventory))
        {
            total += inventory.GetCount(dish);
        }
        return total;
    }

    // [v2] วัตถุดิบที่จะเลือกไว้ในแถว: ตัวที่เลื่อนไว้ตอนนี้ → ตัวล่าสุดของสูตร → ตัวที่มีมากสุด (ไม่มีช่องหมวด = null)
    private ItemData ChoiceFor(RecipeData recipe)
    {
        if (recipe == null || !recipe.HasCategorySlot || inventory == null)
        {
            return null;
        }

        if (pendingChoices.TryGetValue(recipe, out ItemData pending) && RecipeRequirements.HasAll(recipe, inventory, pending))
        {
            return pending;
        }

        ItemData last = recipeBook != null ? recipeBook.GetLastChoice(recipe) : null;
        return RecipeRequirements.DefaultChoice(recipe, inventory, last);
    }

    private void HandleChoiceChanged(RecipeData recipe, ItemData choice)
    {
        if (recipe != null && choice != null)
        {
            pendingChoices[recipe] = choice;
        }
    }

    private void EnsureItemCount(int count)
    {
        if (itemTemplate == null || listContainer == null)
        {
            return;
        }

        while (items.Count < count)
        {
            CookingRecipeItemUI item = Instantiate(itemTemplate, listContainer);
            item.name = $"CookItem_{items.Count + 1}";
            item.gameObject.SetActive(true);
            item.OnCookClicked += HandleItemClicked;
            item.OnChoiceChanged += HandleChoiceChanged;
            items.Add(item);
        }
    }

    // คลิกสูตร: ปิดหน้าต่าง (ปล่อยการเดิน) แล้วเริ่มมินิเกม (CookingSession ล็อกการเดินเอง)
    // [v2] choice = วัตถุดิบที่เลือกในแถว (สูตรไม่มีช่องหมวด = null) เริ่มทำได้ = จำเป็นตัวล่าสุดของสูตร
    private void HandleItemClicked(RecipeData recipe, ItemData choice)
    {
        if (recipe == null)
        {
            return;
        }

        Close();

        if (CookingSession.Instance == null)
        {
            Debug.LogWarning("CookingMenuUI: No CookingSession in the scene. Run Tools > Second Serving > Build Cooking System.", this);
            return;
        }

        bool started = CookingSession.Instance.StartCooking(recipe, CookingSession.CookMode.Free, choice);
        if (started && recipeBook != null && recipe.HasCategorySlot && CookingSession.Instance.CurrentChoice != null)
        {
            recipeBook.SetLastChoice(recipe, CookingSession.Instance.CurrentChoice);
        }
    }
}
