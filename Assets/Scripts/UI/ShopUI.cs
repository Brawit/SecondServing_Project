using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// [2.6b] หน้าต่างร้านค้า (เปิดจาก ShopKeeperCounter) ใช้กับทุกร้านตาม ShopCatalog
// - แท็บ Buy: แถวสินค้า (ShopEntryRowUI) ค้าง Shift = ×10 / แท็บ Sell (เฉพาะร้านที่รับซื้อ): SellPanelUI ขายได้เงินทันที เอาคืนไม่ได้
//   ปุ่มเหมือนกล่องขาย: คลิกซ้าย = ทั้งกอง / คลิกขวา = 1 / Shift + คลิกขวา = 10
// - ระหว่างเปิด: นาฬิกาหยุด เดินไม่ได้ / Esc หรือปุ่ม Close ปิด
// หมายเหตุ: วางสคริปต์นี้บนวัตถุที่เปิดอยู่ตลอด (ไม่ใช่ตัวหน้าต่างที่ถูกซ่อน)
// สร้างและผูกอัตโนมัติได้จาก Tools > Second Serving > Build Shops
public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }
    public static bool IsAnyOpen => Instance != null && Instance.IsOpen;

    [Header("References")]
    [Tooltip("เว้นว่าง = หาเองจาก Player")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Inventory inventory;
    [SerializeField] private RecipeBook recipeBook;

    [Header("Window")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private Button closeButton;

    [Header("Tabs")]
    [SerializeField] private Button buyTabButton;
    [SerializeField] private Button sellTabButton;
    [SerializeField] private GameObject buyRoot;
    [SerializeField] private GameObject sellRoot;
    [SerializeField] private Color activeTabColor = new Color(0.95f, 0.8f, 0.4f);
    [SerializeField] private Color inactiveTabColor = new Color(0.35f, 0.35f, 0.4f);

    [Header("Buy")]
    [SerializeField] private RectTransform listContent;
    [Tooltip("แถวต้นแบบ (ปิดไว้ในฉาก)")]
    [SerializeField] private ShopEntryRowUI rowTemplate;

    [Header("Sell")]
    [SerializeField] private SellPanelUI sellPanel;
    [SerializeField] private TMP_Text sellSummaryText;

    [Header("Behaviour")]
    [SerializeField] private bool pauseClockWhileOpen = true;

    [Header("Text")]
    [Tooltip("{0} = เงินที่มี")]
    [SerializeField] private string moneyFormat = "Money  {0}";
    [Tooltip("{0} = ชื่อ, {1} = จำนวน")]
    [SerializeField] private string boughtFormat = "Bought {0} x{1}.";
    [Tooltip("ต่อท้ายตอนได้น้อยกว่าที่ขอ ({0} = เหตุผล)")]
    [SerializeField] private string limitFormat = " ({0})";
    [Tooltip("{0} = ชื่อ")]
    [SerializeField] private string boughtSingleFormat = "Bought {0}!";
    [Tooltip("{0} = ชื่อ, {1} = จำนวน, {2} = เงินที่ได้")]
    [SerializeField] private string soldFormat = "Sold {0} x{1} for {2}.";
    [Tooltip("{0} = ยอดขายวันนี้, {1} = จานวันนี้, {2} = จานที่ได้ชื่อเสียงสูงสุด, {3} = ชื่อเสียงที่จะได้คืนนี้")]
    [SerializeField] private string sellSummaryFormat = "Sold here today  {0}\nDishes sold today (shop + box)  {1} / {2}\nReputation tonight  +{3}\n\nEvery 5 dishes = +2 reputation (max +10 a day).";

    public bool IsOpen { get; private set; }

    private readonly List<ShopEntryRowUI> rows = new List<ShopEntryRowUI>();
    private ShopCatalog catalog;
    private bool showingSell;
    private bool pausedClockByThis;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("ShopUI: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        PlayerInteractor player = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        if (player != null)
        {
            if (playerController == null) playerController = player.GetComponent<PlayerController>();
            if (inventory == null) inventory = player.GetComponent<Inventory>();
            if (recipeBook == null) recipeBook = player.GetComponent<RecipeBook>();
        }

        if (windowRoot == null) Debug.LogWarning("ShopUI: Window Root is not assigned.", this);
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (buyTabButton != null) buyTabButton.onClick.AddListener(() => ShowTab(false));
        if (sellTabButton != null) sellTabButton.onClick.AddListener(() => ShowTab(true));
    }

    private void Start()
    {
        SetVisible(false);
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsOpen) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
    }

    // ---------- เปิด / ปิด ----------

    public bool Open(ShopCatalog shopCatalog)
    {
        if (IsOpen || windowRoot == null || shopCatalog == null) return false;

        catalog = shopCatalog;
        IsOpen = true;
        SetVisible(true);
        Subscribe();
        BuildRows();
        ShowTab(false);

        if (pauseClockWhileOpen && GameClock.Instance != null && !GameClock.Instance.IsPaused)
        {
            GameClock.Instance.Pause();
            pausedClockByThis = true;
        }
        if (playerController != null) playerController.SetCanMove(false);

        Debug.Log($"ShopUI: Opened {catalog.shopName}.");
        return true;
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        Unsubscribe();
        SetVisible(false);

        if (pausedClockByThis && GameClock.Instance != null) GameClock.Instance.Resume();
        pausedClockByThis = false;

        bool dayEnding = DayEndManager.Instance != null && DayEndManager.Instance.IsDayEnding;
        if (playerController != null && !dayEnding) playerController.SetCanMove(true);

        Debug.Log("ShopUI: Closed.");
    }

    private void SetVisible(bool visible)
    {
        if (windowRoot != null) windowRoot.SetActive(visible);
    }

    private void Subscribe()
    {
        if (inventory != null) inventory.OnChanged += RefreshAll;
        if (recipeBook != null) recipeBook.OnChanged += RefreshAll;
        if (PlayerWallet.Instance != null) PlayerWallet.Instance.OnMoneyChanged += HandleMoneyChanged;
        if (SalesLedger.Instance != null) SalesLedger.Instance.OnChanged += RefreshSellSummary;
    }

    private void Unsubscribe()
    {
        if (inventory != null) inventory.OnChanged -= RefreshAll;
        if (recipeBook != null) recipeBook.OnChanged -= RefreshAll;
        if (PlayerWallet.Instance != null) PlayerWallet.Instance.OnMoneyChanged -= HandleMoneyChanged;
        if (SalesLedger.Instance != null) SalesLedger.Instance.OnChanged -= RefreshSellSummary;
    }

    // ---------- แท็บ ----------

    private void ShowTab(bool sell)
    {
        bool canSell = catalog != null && catalog.buysFromPlayer;
        showingSell = sell && canSell;

        if (buyRoot != null) buyRoot.SetActive(!showingSell);
        if (sellRoot != null) sellRoot.SetActive(showingSell);
        if (buyTabButton != null) buyTabButton.gameObject.SetActive(canSell);
        if (sellTabButton != null) sellTabButton.gameObject.SetActive(canSell);
        SetTabColor(buyTabButton, !showingSell);
        SetTabColor(sellTabButton, showingSell);

        if (showingSell && sellPanel != null) sellPanel.Configure(SellToShop, "sell");
        RefreshAll();
    }

    private void SetTabColor(Button button, bool active)
    {
        if (button != null && button.targetGraphic != null) button.targetGraphic.color = active ? activeTabColor : inactiveTabColor;
    }

    // ---------- ซื้อ ----------

    private void BuildRows()
    {
        int index = 0;
        if (catalog != null && rowTemplate != null && listContent != null)
        {
            foreach (ShopEntry entry in catalog.entries)
            {
                if (!ShopPurchase.IsVisible(entry, inventory)) continue;
                if (index >= rows.Count) rows.Add(Instantiate(rowTemplate, listContent));
                ShopEntryRowUI row = rows[index];
                row.gameObject.SetActive(true);
                row.Bind(catalog, entry, inventory, recipeBook, Buy);
                index++;
            }
        }

        for (int i = index; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
    }

    private void Buy(ShopEntry entry, int amount)
    {
        if (ShopPurchase.TryBuy(catalog, entry, amount, inventory, recipeBook, out int bought, out string reason, out string limitNote))
        {
            string text = entry.kind == ShopEntryKind.Item
                ? string.Format(boughtFormat, entry.DisplayName, bought)
                : string.Format(boughtSingleFormat, entry.DisplayName);
            // ได้น้อยกว่าที่ขอ = บอกเหตุผล (ไม่หักเงินส่วนที่ไม่ได้)
            if (bought < amount && !string.IsNullOrEmpty(limitNote)) text += string.Format(limitFormat, limitNote);
            NotificationUI.Post(text);

            // อัปเกรดกระเป๋าแล้ว แถวขั้นถัดไปต้องเปลี่ยน
            if (entry.kind == ShopEntryKind.BagUpgrade) BuildRows();
        }
        else if (!string.IsNullOrEmpty(reason))
        {
            NotificationUI.Post(reason);
        }
        RefreshAll();
    }

    // ---------- ขาย ----------

    private bool SellToShop(ItemData item, int amount, DishQuality quality)
    {
        SalesLedger ledger = SalesLedger.Instance;
        if (ledger == null)
        {
            NotificationUI.Post("No SalesLedger in the scene. Run Build Shops.");
            return false;
        }

        int total = SalesLedger.UnitSellPrice(item, quality) * amount;
        if (!ledger.SellNow(item, amount, quality)) return false;

        string name = Inventory.TracksQuality(item) ? $"{item.itemName} ({DishQualityUtil.Label(quality)})" : item.itemName;
        NotificationUI.Post(string.Format(soldFormat, name, amount, PlayerWallet.Format(total)));
        return true;
    }

    // ---------- แสดงผล ----------

    private void HandleMoneyChanged(int oldValue, int newValue)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (!IsOpen) return;

        if (titleText != null) titleText.text = catalog != null ? catalog.shopName : "";
        if (moneyText != null) moneyText.text = string.Format(moneyFormat, PlayerWallet.Instance != null ? PlayerWallet.Format(PlayerWallet.Instance.Money) : "-");

        foreach (ShopEntryRowUI row in rows)
        {
            if (row.gameObject.activeSelf) row.Refresh();
        }
        RefreshSellSummary();
    }

    private void RefreshSellSummary()
    {
        if (sellSummaryText == null || !IsOpen) return;
        SalesLedger ledger = SalesLedger.Instance;
        if (ledger == null)
        {
            sellSummaryText.text = "";
            return;
        }

        sellSummaryText.text = string.Format(sellSummaryFormat, PlayerWallet.Format(ledger.ShopIncomeToday),
            ledger.DishesCountedToday, ledger.DishesForMaxReputation, ledger.PendingReputation);
    }
}
