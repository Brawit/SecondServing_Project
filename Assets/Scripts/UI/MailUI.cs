using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// [2.5c] หน้าต่างจดหมาย (แบบ Stardew) ใช้ 2 โหมด
// - Mailbox (ตู้จดหมาย): หยิบฉบับยังไม่อ่านที่เก่าสุดมาอ่านทีละฉบับ ไม่มีรายการ / ปิดแล้วกด E ที่ตู้อีกครั้ง = ฉบับถัดไป
// - Archive (โต๊ะคอมในห้องนอน): รายการจดหมายทั้งหมด (ใหม่สุดบน) อ่านซ้ำได้ตลอด รับของที่ยังไม่ได้รับได้
// เนื้อจดหมาย: จาก (ผู้ส่ง · บรรทัดรอง), วันที่ได้, เนื้อความ (เลื่อนได้), คำลงท้าย, ช่องของแนบด้านล่างตรงกลาง (คลิก = รับ)
// Esc หรือปุ่ม Close = ปิด
// ระหว่างเปิดหน้าต่าง: นาฬิกาหยุด เดินไม่ได้ เปิดกระเป๋าไม่ได้ ใช้ Hotbar ไม่ได้ (เหมือนหน้าต่างเลือกเมนู)
// หมายเหตุ: วางสคริปต์นี้บนวัตถุที่เปิดอยู่ตลอด (ไม่ใช่ตัวหน้าต่างที่ถูกซ่อน)
// สร้างและผูกอัตโนมัติได้จาก Tools > Second Serving > Build Mail System
public class MailUI : MonoBehaviour
{
    public enum Mode { Mailbox, Archive }

    public static MailUI Instance { get; private set; }

    // มีหน้าต่างจดหมายเปิดอยู่หรือไม่ (ระบบอื่นใช้เช็กเพื่อล็อกปุ่ม)
    public static bool IsAnyOpen => Instance != null && Instance.IsOpen;

    [Header("References")]
    [Tooltip("ตัวควบคุมการเดินของผู้เล่น ใช้หยุดเดินตอนเปิดหน้าต่าง (เว้นว่าง = หาเอง)")]
    [SerializeField] private PlayerController playerController;

    [Header("Window")]
    [Tooltip("ตัวหน้าต่างที่จะซ่อน/แสดง")]
    [SerializeField] private GameObject windowRoot;

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text hintText;

    [Header("List (Archive)")]
    [Tooltip("ฝั่งซ้ายทั้งก้อน (ซ่อนในโหมด Mailbox)")]
    [SerializeField] private GameObject listRoot;

    [Tooltip("ที่วางรายการจดหมาย (Content ของ Scroll View ฝั่งซ้าย)")]
    [SerializeField] private Transform listContainer;

    [Tooltip("แม่แบบรายการจดหมาย (ปิดไว้)")]
    [SerializeField] private MailListItemUI itemTemplate;

    [Tooltip("ข้อความตอนยังไม่มีจดหมาย")]
    [SerializeField] private TMP_Text emptyText;

    [Header("Letter")]
    [Tooltip("ฝั่งจดหมายทั้งก้อน (กระดาษ)")]
    [SerializeField] private RectTransform letterPanel;

    [SerializeField] private TMP_Text fromText;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text signOffText;

    [Tooltip("Scroll View ของเนื้อจดหมาย (เลื่อนกลับบนสุดเมื่อเปลี่ยนฉบับ)")]
    [SerializeField] private ScrollRect letterScroll;

    [Tooltip("แถวช่องของแนบด้านล่างจดหมาย")]
    [SerializeField] private Transform attachmentContainer;

    [Tooltip("แม่แบบช่องของแนบ (ปิดไว้)")]
    [SerializeField] private LetterAttachmentSlotUI attachmentTemplate;

    [SerializeField] private Button closeButton;

    [Header("Layout")]
    [Tooltip("ระยะซ้าย/ขวาของกระดาษในโหมด Mailbox (ไม่มีรายการ)")]
    [SerializeField] private float singleSideMargin = 220f;

    [Tooltip("ระยะซ้ายของกระดาษในโหมด Archive (หลบรายการ)")]
    [SerializeField] private float archiveLeftMargin = 428f;

    [Tooltip("ระยะขวาของกระดาษในโหมด Archive")]
    [SerializeField] private float archiveRightMargin = 24f;

    [Header("Behaviour")]
    [Tooltip("หยุดนาฬิการะหว่างเปิดหน้าต่าง")]
    [SerializeField] private bool pauseClockWhileOpen = true;

    [Header("Text")]
    [SerializeField] private string mailboxTitle = "Mail";
    [SerializeField] private string archiveTitle = "Letters";
    [SerializeField] private string emptyMessage = "No letters yet.";

    [Tooltip("{0} = จำนวนฉบับที่ยังเหลือในตู้")]
    [SerializeField] private string mailboxMoreHint = "{0} more in the mailbox · Click a gift to take it · Esc to close";
    [SerializeField] private string mailboxLastHint = "Click a gift to take it · Esc to close";
    [SerializeField] private string archiveHint = "Click a letter to read · Click a gift to take it · Esc to close";

    [Tooltip("{0} = ผู้ส่ง, {1} = บรรทัดรอง")]
    [SerializeField] private string fromFormat = "From: {0} · {1}";
    [Tooltip("ไม่มีบรรทัดรอง ({0} = ผู้ส่ง)")]
    [SerializeField] private string fromOnlyFormat = "From: {0}";
    [Tooltip("{0} = วันที่ได้")]
    [SerializeField] private string dayFormat = "Day {0}";

    public bool IsOpen { get; private set; }
    public Mode CurrentMode { get; private set; }

    private readonly List<MailListItemUI> items = new List<MailListItemUI>();
    private readonly List<LetterAttachmentSlotUI> slots = new List<LetterAttachmentSlotUI>();
    private readonly List<MailLetter> ordered = new List<MailLetter>();
    private MailLetter selected;
    private bool pausedClockByThis;
    private bool isDirty = true;
    private bool isSubscribed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("MailUI: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (playerController == null)
        {
            PlayerInteractor player = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
            if (player != null) playerController = player.GetComponent<PlayerController>();
        }

        if (windowRoot == null) Debug.LogWarning("MailUI: Window Root is not assigned.", this);
        if (windowRoot == gameObject) Debug.LogWarning("MailUI: Do not put this script on the Window Root itself. Put it on its parent.", this);
        if (itemTemplate == null) Debug.LogWarning("MailUI: Item Template is not assigned.", this);
        if (attachmentTemplate == null) Debug.LogWarning("MailUI: Attachment Template is not assigned. Run Build Mail System again.", this);

        if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
        if (attachmentTemplate != null) attachmentTemplate.gameObject.SetActive(false);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void Start()
    {
        SetWindowVisible(false);
        IsOpen = false;
        Subscribe();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        if (isSubscribed && MailSystem.Instance != null)
        {
            MailSystem.Instance.OnMailChanged -= MarkDirty;
        }
        isSubscribed = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Subscribe()
    {
        if (isSubscribed || MailSystem.Instance == null)
        {
            return;
        }

        MailSystem.Instance.OnMailChanged += MarkDirty;
        isSubscribed = true;
    }

    private void MarkDirty()
    {
        isDirty = true;
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        // ร้านเปิดหรือกำลังจบวันระหว่างนี้ (ไม่ควรเกิด เพราะนาฬิกาหยุด) = ปิด
        if ((ShopManager.Instance != null && ShopManager.Instance.IsServing) ||
            (DayEndManager.Instance != null && DayEndManager.Instance.IsDayEnding))
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

    // ---------- เปิด / ปิด ----------

    // ตู้จดหมาย: เปิดฉบับยังไม่อ่านที่เก่าสุด คืนค่า false ถ้าไม่มีจดหมายใหม่
    public bool OpenMailbox()
    {
        MailSystem mail = MailSystem.Instance;
        MailLetter next = mail != null ? mail.NextUnread() : null;
        if (next == null)
        {
            return false;
        }

        return OpenInMode(Mode.Mailbox, next);
    }

    // โต๊ะ: เปิดกล่องจดหมายทั้งหมด (ไม่มีจดหมาย = ขึ้นข้อความว่าง)
    public bool OpenArchive()
    {
        MailSystem mail = MailSystem.Instance;
        MailLetter first = null;
        if (mail != null && mail.Letters.Count > 0)
        {
            first = mail.Letters[mail.Letters.Count - 1];
        }
        return OpenInMode(Mode.Archive, first);
    }

    private bool OpenInMode(Mode mode, MailLetter letter)
    {
        if (IsOpen || windowRoot == null)
        {
            return false;
        }

        Subscribe();

        CurrentMode = mode;
        IsOpen = true;
        SetWindowVisible(true);
        ApplyLayout();
        selected = letter;
        Refresh();
        ResetScroll();

        if (pauseClockWhileOpen && GameClock.Instance != null && !GameClock.Instance.IsPaused)
        {
            GameClock.Instance.Pause();
            pausedClockByThis = true;
        }

        if (playerController != null)
        {
            playerController.SetCanMove(false);
        }

        Debug.Log($"MailUI: Opened ({mode}).");
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

        // กำลังจบวันอยู่ = DayEndManager ล็อกผู้เล่นไว้ ไม่ปล่อยการเดิน
        bool dayEnding = DayEndManager.Instance != null && DayEndManager.Instance.IsDayEnding;
        if (playerController != null && !dayEnding)
        {
            playerController.SetCanMove(true);
        }

        Debug.Log("MailUI: Closed.");
    }

    private void SetWindowVisible(bool visible)
    {
        if (windowRoot != null)
        {
            windowRoot.SetActive(visible);
        }
    }

    // โหมด Mailbox: ซ่อนรายการ กระดาษอยู่กลาง / โหมด Archive: รายการซ้าย กระดาษขวา
    private void ApplyLayout()
    {
        bool archive = CurrentMode == Mode.Archive;

        if (listRoot != null) listRoot.SetActive(archive);
        if (titleText != null) titleText.text = archive ? archiveTitle : mailboxTitle;

        if (letterPanel != null)
        {
            Vector2 min = letterPanel.offsetMin;
            Vector2 max = letterPanel.offsetMax;
            min.x = archive ? archiveLeftMargin : singleSideMargin;
            max.x = -(archive ? archiveRightMargin : singleSideMargin);
            letterPanel.offsetMin = min;
            letterPanel.offsetMax = max;
        }
    }

    // ---------- เลือก / รับของ ----------

    private void Select(MailLetter letter)
    {
        if (letter == null || letter == selected)
        {
            return;
        }

        selected = letter;
        Refresh();
        ResetScroll();
    }

    private void Claim(int index)
    {
        if (selected == null || MailSystem.Instance == null)
        {
            return;
        }

        MailSystem.Instance.TryClaim(selected, index);
        Refresh();
    }

    private void ResetScroll()
    {
        if (letterScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            letterScroll.verticalNormalizedPosition = 1f;
        }
    }

    // ---------- วาด ----------

    private void Refresh()
    {
        isDirty = false;
        MailSystem mail = MailSystem.Instance;

        ordered.Clear();
        if (mail != null)
        {
            for (int i = mail.Letters.Count - 1; i >= 0; i--)
            {
                ordered.Add(mail.Letters[i]); // ใหม่สุดอยู่บน
            }
        }

        if (selected != null && !ordered.Contains(selected))
        {
            selected = null;
        }

        // แสดงฉบับไหน = อ่านฉบับนั้นแล้ว (MarkRead ทำให้ isDirty แต่วาดต่อจากนี้อยู่แล้ว)
        if (selected != null && !selected.isRead && mail != null)
        {
            mail.MarkRead(selected);
            isDirty = false;
        }

        bool archive = CurrentMode == Mode.Archive;
        if (archive)
        {
            EnsureItemCount(ordered.Count);
            for (int i = 0; i < items.Count; i++)
            {
                bool used = i < ordered.Count;
                items[i].gameObject.SetActive(used);
                if (used) items[i].Show(ordered[i], ordered[i] == selected);
            }
        }

        bool hasLetter = selected != null;
        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(!hasLetter);
            emptyText.text = emptyMessage;
        }
        if (letterPanel != null)
        {
            letterPanel.gameObject.SetActive(hasLetter);
        }

        if (hintText != null)
        {
            if (archive)
            {
                hintText.text = archiveHint;
            }
            else
            {
                int remaining = mail != null ? mail.UnreadCount : 0;
                hintText.text = remaining > 0 ? string.Format(mailboxMoreHint, remaining) : mailboxLastHint;
            }
        }

        ShowLetter(selected);
    }

    private void ShowLetter(MailLetter letter)
    {
        if (fromText != null)
        {
            if (letter == null) fromText.text = "";
            else if (string.IsNullOrEmpty(letter.SenderSubtitle)) fromText.text = string.Format(fromOnlyFormat, letter.Sender);
            else fromText.text = string.Format(fromFormat, letter.Sender, letter.SenderSubtitle);
        }
        if (dayText != null) dayText.text = letter != null ? string.Format(dayFormat, letter.dayReceived) : "";
        if (bodyText != null) bodyText.text = letter != null ? letter.Body : "";
        if (signOffText != null) signOffText.text = letter != null ? letter.SignOff : "";

        int count = letter != null ? letter.AttachmentCount : 0;
        EnsureSlotCount(count);
        for (int i = 0; i < slots.Count; i++)
        {
            bool used = i < count;
            slots[i].gameObject.SetActive(used);
            if (used) slots[i].Show(letter.GetAttachment(i), i, letter.IsClaimed(i));
        }
        if (attachmentContainer != null)
        {
            attachmentContainer.gameObject.SetActive(count > 0);
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
            MailListItemUI item = Instantiate(itemTemplate, listContainer);
            item.name = $"MailItem_{items.Count}";
            item.gameObject.SetActive(true);
            item.OnClicked += Select;
            items.Add(item);
        }
    }

    private void EnsureSlotCount(int count)
    {
        if (attachmentTemplate == null || attachmentContainer == null)
        {
            return;
        }

        while (slots.Count < count)
        {
            LetterAttachmentSlotUI slot = Instantiate(attachmentTemplate, attachmentContainer);
            slot.name = $"Attachment_{slots.Count}";
            slot.gameObject.SetActive(true);
            slot.OnClicked += Claim;
            slots.Add(slot);
        }
    }
}
