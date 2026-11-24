using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// [2.6] หน้าต่างช่าง (เปิดจาก BuilderCounter): อัปเกรดร้านขั้นถัดไป
// - แสดง: เลเวล, หลอดชื่อเสียง, ราคา (เงินที่มี), เวลา, สิ่งที่ปลดล็อก, เหตุผลที่ยังจ้างไม่ได้ (สีแดง)
// - Hire = กล่องยืนยัน Yes / No → Yes = ConstructionManager.TryStart (ตรวจซ้ำก่อนหักเงิน)
// - กำลังสร้าง = "Under construction · N day(s) left" / เลเวลสูงสุด = "Your shop is fully upgraded."
// - Esc = ปิดกล่องยืนยันก่อน แล้วค่อยปิดหน้าต่าง / ระหว่างเปิด: นาฬิกาหยุด เดินไม่ได้
// หมายเหตุ: วางสคริปต์นี้บนวัตถุที่เปิดอยู่ตลอด (ไม่ใช่ตัวหน้าต่างที่ถูกซ่อน)
// สร้างและผูกอัตโนมัติได้จาก Tools > Second Serving > Build Economy
public class ConstructionUI : MonoBehaviour
{
    public static ConstructionUI Instance { get; private set; }
    public static bool IsAnyOpen => Instance != null && Instance.IsOpen;

    [Header("References")]
    [Tooltip("ตัวควบคุมการเดินของผู้เล่น (เว้นว่าง = หาเอง)")]
    [SerializeField] private PlayerController playerController;

    [Header("Window")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text levelText;

    [Tooltip("แถบหลอดชื่อเสียง (ยืดด้วย Anchor Max X)")]
    [SerializeField] private RectTransform reputationFill;
    [SerializeField] private TMP_Text reputationText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text unlockText;

    [Tooltip("สถานะ/เหตุผลที่จ้างไม่ได้")]
    [SerializeField] private TMP_Text statusText;

    [SerializeField] private Button hireButton;
    [SerializeField] private Button closeButton;

    [Header("Confirm")]
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("Behaviour")]
    [SerializeField] private bool pauseClockWhileOpen = true;

    [Header("Text")]
    [SerializeField] private string titleFormat = "Builder";
    [Tooltip("{0} = เลเวลตอนนี้, {1} = เลเวลถัดไป")]
    [SerializeField] private string levelFormat = "Shop Level {0} → {1}";
    [Tooltip("{0} = ชื่อเสียง, {1} = ขนาดหลอด")]
    [SerializeField] private string reputationFormat = "Reputation  {0:N0} / {1:N0}";
    [Tooltip("{0} = ราคา, {1} = เงินที่มี")]
    [SerializeField] private string costFormat = "Cost  {0}  (you have {1})";
    [Tooltip("{0} = จำนวนวัน")]
    [SerializeField] private string timeFormat = "Time  {0} day(s)";
    [Tooltip("{0} = สิ่งที่ปลดล็อก")]
    [SerializeField] private string unlockFormat = "Unlocks  {0}";
    [Tooltip("{0} = ชื่องาน, {1} = วันที่เหลือ")]
    [SerializeField] private string buildingFormat = "Under construction: {0} · {1} day(s) left";
    [SerializeField] private string maxLevelMessage = "Your shop is fully upgraded.";
    [SerializeField] private string readyMessage = "Ready to build!";
    [Tooltip("{0} = ชื่องาน, {1} = ราคา, {2} = จำนวนวัน")]
    [SerializeField] private string confirmFormat = "{0} for {1}?\nThe builder starts tomorrow and needs {2} day(s).";
    [Tooltip("{0} = จำนวนวัน")]
    [SerializeField] private string startedFormat = "Deal! The builder will start tomorrow. It takes {0} day(s).";
    [SerializeField] private Color okColor = new Color(0.55f, 0.9f, 0.55f);
    [SerializeField] private Color problemColor = new Color(1f, 0.45f, 0.4f);

    public bool IsOpen { get; private set; }

    private ConstructionProject shown;
    private bool pausedClockByThis;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("ConstructionUI: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (playerController == null)
        {
            PlayerInteractor player = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
            if (player != null) playerController = player.GetComponent<PlayerController>();
        }

        if (windowRoot == null) Debug.LogWarning("ConstructionUI: Window Root is not assigned.", this);
        if (hireButton != null) hireButton.onClick.AddListener(AskConfirm);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (yesButton != null) yesButton.onClick.AddListener(ConfirmYes);
        if (noButton != null) noButton.onClick.AddListener(HideConfirm);
    }

    private void Start()
    {
        SetVisible(false);
        HideConfirm();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsOpen) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (confirmPanel != null && confirmPanel.activeSelf) HideConfirm();
            else Close();
        }
    }

    // ---------- เปิด / ปิด ----------

    public bool Open()
    {
        if (IsOpen || windowRoot == null) return false;

        IsOpen = true;
        SetVisible(true);
        HideConfirm();
        Refresh();

        if (pauseClockWhileOpen && GameClock.Instance != null && !GameClock.Instance.IsPaused)
        {
            GameClock.Instance.Pause();
            pausedClockByThis = true;
        }
        if (playerController != null) playerController.SetCanMove(false);

        Debug.Log("ConstructionUI: Opened.");
        return true;
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        HideConfirm();
        SetVisible(false);

        if (pausedClockByThis && GameClock.Instance != null) GameClock.Instance.Resume();
        pausedClockByThis = false;

        bool dayEnding = DayEndManager.Instance != null && DayEndManager.Instance.IsDayEnding;
        if (playerController != null && !dayEnding) playerController.SetCanMove(true);

        Debug.Log("ConstructionUI: Closed.");
    }

    private void SetVisible(bool visible)
    {
        if (windowRoot != null) windowRoot.SetActive(visible);
    }

    // ---------- แสดงผล ----------

    private void Refresh()
    {
        ConstructionManager manager = ConstructionManager.Instance;
        ShopProgress progress = ShopProgress.Instance;
        PlayerWallet wallet = PlayerWallet.Instance;

        if (titleText != null) titleText.text = titleFormat;

        int level = progress != null ? progress.Level : 1;
        int rep = progress != null ? progress.Reputation : 0;
        int bar = progress != null ? Mathf.Max(1, progress.BarSize) : 1;
        if (reputationText != null) reputationText.text = string.Format(reputationFormat, rep, bar);
        if (reputationFill != null)
        {
            Vector2 max = reputationFill.anchorMax;
            max.x = Mathf.Clamp01((float)rep / bar);
            reputationFill.anchorMax = max;
        }

        shown = manager != null ? manager.NextShopUpgrade() : null;
        bool canHire = false;
        string status;

        if (manager != null && manager.IsBuilding)
        {
            status = string.Format(buildingFormat, manager.Current.displayName, manager.DaysLeft);
        }
        else if (shown == null)
        {
            status = maxLevelMessage;
        }
        else if (manager.CanStart(shown, out string reason))
        {
            status = readyMessage;
            canHire = true;
        }
        else
        {
            status = reason;
        }

        ShopLevelInfo nextInfo = progress != null ? progress.GetInfo(level + 1) : null;
        bool hasNext = shown != null;

        if (levelText != null) levelText.text = shown != null ? string.Format(levelFormat, level, level + 1) : $"Shop Level {level}";
        if (costText != null) costText.text = shown != null ? string.Format(costFormat, PlayerWallet.Format(shown.money), PlayerWallet.Format(wallet != null ? wallet.Money : 0)) : "";
        if (timeText != null) timeText.text = shown != null ? string.Format(timeFormat, shown.days) : "";
        if (unlockText != null) unlockText.text = hasNext && nextInfo != null && !string.IsNullOrEmpty(nextInfo.unlockText) ? string.Format(unlockFormat, nextInfo.unlockText) : "";

        if (statusText != null)
        {
            statusText.text = status;
            statusText.color = canHire ? okColor : problemColor;
        }
        if (hireButton != null) hireButton.interactable = canHire;
    }

    // ---------- ยืนยัน ----------

    private void AskConfirm()
    {
        if (shown == null || ConstructionManager.Instance == null) return;
        if (!ConstructionManager.Instance.CanStart(shown, out _)) { Refresh(); return; }

        if (confirmText != null)
        {
            confirmText.text = string.Format(confirmFormat, shown.displayName, PlayerWallet.Format(shown.money), shown.days);
        }
        if (confirmPanel != null) confirmPanel.SetActive(true);
    }

    private void ConfirmYes()
    {
        HideConfirm();
        if (shown != null && ConstructionManager.Instance != null && ConstructionManager.Instance.TryStart(shown))
        {
            NotificationUI.Post(string.Format(startedFormat, shown.days));
            Close(); // ช่างติดงานแล้ว ใช้หน้าต่างไม่ได้จนกว่าจะเสร็จ
            return;
        }
        Refresh();
    }

    private void HideConfirm()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }
}
