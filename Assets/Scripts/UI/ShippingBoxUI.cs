using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// [2.6b] หน้าต่างกล่องขายข้ามคืน (เปิดจาก ShippingBox)
// - ซ้าย: SellPanelUI (ใส่ของลงกล่อง = ย้ายออกจากกระเป๋าทันที) คลิกซ้าย = ทั้งกอง / คลิกขวา = 1 / Shift + คลิกขวา = 10
// - ขวา: ของในกล่องวันนี้ + ยอดรวม + ตัวนับจาน/ชื่อเสียง + เอาคืน (ได้ทุกชิ้นจนกว่าจะนอน)
// - ระหว่างเปิด: นาฬิกาหยุด เดินไม่ได้ / Esc หรือปุ่ม Close ปิด
// หมายเหตุ: วางสคริปต์นี้บนวัตถุที่เปิดอยู่ตลอด (ไม่ใช่ตัวหน้าต่างที่ถูกซ่อน)
// สร้างและผูกอัตโนมัติได้จาก Tools > Second Serving > Build Shops
public class ShippingBoxUI : MonoBehaviour
{
    public static ShippingBoxUI Instance { get; private set; }
    public static bool IsAnyOpen => Instance != null && Instance.IsOpen;

    [Header("References")]
    [Tooltip("เว้นว่าง = หาเองจาก Player")]
    [SerializeField] private PlayerController playerController;

    [Header("Window")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button closeButton;
    [SerializeField] private SellPanelUI sellPanel;

    [Header("Box")]
    [SerializeField] private RectTransform boxContent;
    [Tooltip("แถวต้นแบบ (ปิดไว้ในฉาก)")]
    [SerializeField] private BoxEntryRowUI boxRowTemplate;
    [SerializeField] private TMP_Text boxEmptyText;
    [SerializeField] private TMP_Text summaryText;

    [Header("Behaviour")]
    [SerializeField] private bool pauseClockWhileOpen = true;

    [Header("Text")]
    [SerializeField] private string title = "Shipping Box";
    [SerializeField] private string emptyBoxMessage = "The box is empty.\nItems are sold overnight.";
    [Tooltip("{0} = ยอดรวม, {1} = จานวันนี้, {2} = จานที่ได้ชื่อเสียงสูงสุด, {3} = ชื่อเสียงคืนนี้")]
    [SerializeField] private string summaryFormat = "Total tonight  {0}\nDishes today (shop + box)  {1} / {2}  →  +{3} reputation";
    [Tooltip("{0} = ชื่อ, {1} = จำนวน")]
    [SerializeField] private string shippedFormat = "Put {0} x{1} in the shipping box.";

    public bool IsOpen { get; private set; }

    private readonly List<BoxEntryRowUI> rows = new List<BoxEntryRowUI>();
    private bool pausedClockByThis;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("ShippingBoxUI: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (playerController == null)
        {
            PlayerInteractor player = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
            if (player != null) playerController = player.GetComponent<PlayerController>();
        }

        if (windowRoot == null) Debug.LogWarning("ShippingBoxUI: Window Root is not assigned.", this);
        if (boxRowTemplate != null) boxRowTemplate.gameObject.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void Start()
    {
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (SalesLedger.Instance != null) SalesLedger.Instance.OnChanged -= RefreshBox;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsOpen) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
    }

    // ---------- เปิด / ปิด ----------

    public bool Open()
    {
        if (IsOpen || windowRoot == null) return false;
        if (SalesLedger.Instance == null)
        {
            NotificationUI.Post("No SalesLedger in the scene. Run Build Shops.");
            return false;
        }

        IsOpen = true;
        SetVisible(true);
        if (titleText != null) titleText.text = title;
        SalesLedger.Instance.OnChanged += RefreshBox;
        if (sellPanel != null) sellPanel.Configure(PutInBox, "ship");
        RefreshBox();

        if (pauseClockWhileOpen && GameClock.Instance != null && !GameClock.Instance.IsPaused)
        {
            GameClock.Instance.Pause();
            pausedClockByThis = true;
        }
        if (playerController != null) playerController.SetCanMove(false);

        Debug.Log("ShippingBoxUI: Opened.");
        return true;
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        if (SalesLedger.Instance != null) SalesLedger.Instance.OnChanged -= RefreshBox;
        SetVisible(false);

        if (pausedClockByThis && GameClock.Instance != null) GameClock.Instance.Resume();
        pausedClockByThis = false;

        bool dayEnding = DayEndManager.Instance != null && DayEndManager.Instance.IsDayEnding;
        if (playerController != null && !dayEnding) playerController.SetCanMove(true);

        Debug.Log("ShippingBoxUI: Closed.");
    }

    private void SetVisible(bool visible)
    {
        if (windowRoot != null) windowRoot.SetActive(visible);
    }

    // ---------- ใส่ / เอาคืน ----------

    private bool PutInBox(ItemData item, int amount, DishQuality quality)
    {
        SalesLedger ledger = SalesLedger.Instance;
        if (ledger == null || !ledger.PutInBox(item, amount, quality)) return false;

        // [v2] ชื่อจานรวมแบบอยู่แล้ว เช่น "Sushi (Salmon) · Perfect"
        string name = Inventory.TracksQuality(item) ? $"{item.itemName} · {DishQualityUtil.Label(quality)}" : item.itemName;
        NotificationUI.Post(string.Format(shippedFormat, name, amount));
        return true;
    }

    private void TakeBack(int index, int amount)
    {
        SalesLedger ledger = SalesLedger.Instance;
        if (ledger == null) return;
        ledger.TakeBack(index, amount);
    }

    // ---------- แสดงผล ----------

    private void RefreshBox()
    {
        if (!IsOpen) return;
        SalesLedger ledger = SalesLedger.Instance;
        IReadOnlyList<BoxEntry> box = ledger != null ? ledger.Box : null;
        int count = box != null ? box.Count : 0;

        while (rows.Count < count && boxRowTemplate != null && boxContent != null)
        {
            rows.Add(Instantiate(boxRowTemplate, boxContent));
        }

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < count;
            rows[i].gameObject.SetActive(used);
            if (used) rows[i].Bind(i, box[i], TakeBack);
        }

        if (boxEmptyText != null)
        {
            boxEmptyText.gameObject.SetActive(count == 0);
            boxEmptyText.text = emptyBoxMessage;
        }

        if (summaryText != null && ledger != null)
        {
            summaryText.text = string.Format(summaryFormat, PlayerWallet.Format(ledger.BoxTotal),
                ledger.DishesCountedToday, ledger.DishesForMaxReputation, ledger.PendingReputation);
        }
    }
}
