using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// [2.5] หน้าต่างทำอาหารนอกเวลาร้าน: เปิดเมื่อกด E ที่เตาตอนร้านไม่ได้เปิด
// - แสดงสูตรที่รู้และวัตถุดิบในกระเป๋าพอทำ คลิก = ปิดหน้าต่างแล้วเริ่มมินิเกม (จานเข้ากระเป๋าพร้อมคุณภาพ)
// - Esc หรือปุ่ม Cancel = ปิด
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
                int inBag = inventory != null ? inventory.GetCount(recipe.dishItem) : 0;
                items[i].Show(recipe, RecipeDatabase.GetMaxServings(recipe, inventory), inBag);
            }
        }

        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(buffer.Count == 0);
            emptyText.text = emptyMessage;
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
            item.OnClicked += HandleItemClicked;
            items.Add(item);
        }
    }

    // คลิกสูตร: ปิดหน้าต่าง (ปล่อยการเดิน) แล้วเริ่มมินิเกม (CookingSession ล็อกการเดินเอง)
    private void HandleItemClicked(RecipeData recipe)
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

        CookingSession.Instance.StartCooking(recipe, CookingSession.CookMode.Free);
    }
}
