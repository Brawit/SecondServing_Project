using TMPro;
using UnityEngine;

// [2.5c] ตู้จดหมายหน้าร้าน: กด E = หยิบจดหมายใหม่ (ยังไม่อ่าน) ออกมาอ่านทีละฉบับ แบบ Stardew
// - จดหมายที่อ่านแล้วเก็บไว้ที่โต๊ะคอมในห้องนอน (MailDesk) อ่านซ้ำได้ที่นั่น
// - มีจดหมายยังไม่อ่าน = "!" เหนือตู้ / ผู้เล่นอยู่ใกล้ = "[E] Mail" / ไม่มีจดหมายใหม่ = แจ้ง "The mailbox is empty."
// - ระหว่างเปิดร้านเปิดไม่ได้ (แจ้งเตือน)
// ต้องมี Collider2D บนวัตถุเดียวกัน (แบบทึบได้ ผู้เล่นกด E ได้ในระยะ)
// สร้าง prefab ได้จาก Tools > Second Serving > Build Mail System (Assets/Prefabs/Mailbox.prefab)
public class Mailbox : MonoBehaviour, IInteractable
{
    [Header("References")]
    [Tooltip("ข้อความเหนือตู้ (TextMeshPro แบบ 3D)")]
    [SerializeField] private TMP_Text labelText;

    [Header("Prompt")]
    [Tooltip("ข้อความชวนกด E เมื่อผู้เล่นอยู่ใกล้ (เว้นว่าง = ไม่แสดง)")]
    [SerializeField] private string promptText = "[E] Mail";

    [Tooltip("ระยะที่แสดงข้อความชวนกด E")]
    [Min(0.5f)] [SerializeField] private float promptDistance = 1.6f;

    [Header("Text")]
    [Tooltip("เครื่องหมายเมื่อมีจดหมายยังไม่อ่าน")]
    [SerializeField] private string unreadMark = "<size=160%><color=#FFD24A><b>!</b></color></size>";

    [Tooltip("แจ้งเตือนเมื่อกดระหว่างเปิดร้าน")]
    [SerializeField] private string servingMessage = "The mail can wait until the shop closes.";

    [Tooltip("ไม่มีจดหมายใหม่")]
    [SerializeField] private string emptyMessage = "The mailbox is empty.";

    [Tooltip("แจ้งเตือนเมื่อไม่มีหน้าต่างจดหมายในฉาก")]
    [SerializeField] private string noWindowMessage = "No mail window in the scene. Run Build Mail System.";

    private Transform player;
    private bool promptShown;
    private int lastUnread = -1;
    private bool lastWindowOpen;

    private void Awake()
    {
        if (GetComponent<Collider2D>() == null)
        {
            Debug.LogWarning($"Mailbox '{name}': Needs a Collider2D to be found by the player.", this);
        }
        if (labelText == null)
        {
            Debug.LogWarning($"Mailbox '{name}': Label Text is not assigned.", this);
        }
    }

    private void Start()
    {
        PlayerInteractor interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor != null) player = interactor.transform;
        RefreshLabel();
    }

    // ---------- IInteractable ----------

    public bool CanInteract(PlayerInteractor interactor)
    {
        return !MailUI.IsAnyOpen && !CookingSession.IsBusy && !MenuSelectionUI.IsAnyOpen && !CookingMenuUI.IsAnyOpen;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (interactor != null) player = interactor.transform;

        if (ShopManager.Instance != null && ShopManager.Instance.IsServing)
        {
            NotificationUI.Post(servingMessage);
            return;
        }

        if (MailUI.Instance == null)
        {
            NotificationUI.Post(noWindowMessage);
            return;
        }

        if (!MailUI.Instance.OpenMailbox())
        {
            NotificationUI.Post(emptyMessage);
        }
    }

    // ---------- ข้อความเหนือตู้ ----------

    private void Update()
    {
        bool show = !string.IsNullOrEmpty(promptText) && player != null && !MailUI.IsAnyOpen && !CookingSession.IsBusy &&
                    Vector2.Distance(player.position, transform.position) <= promptDistance;
        int unread = MailSystem.Instance != null ? MailSystem.Instance.UnreadCount : 0;
        bool windowOpen = MailUI.IsAnyOpen;

        if (show != promptShown || unread != lastUnread || windowOpen != lastWindowOpen)
        {
            promptShown = show;
            lastUnread = unread;
            lastWindowOpen = windowOpen;
            RefreshLabel();
        }
    }

    private void RefreshLabel()
    {
        if (labelText == null)
        {
            return;
        }

        int unread = MailSystem.Instance != null ? MailSystem.Instance.UnreadCount : 0;
        string text = unread > 0 ? unreadMark : "";
        if (promptShown)
        {
            text = string.IsNullOrEmpty(text) ? promptText : $"{text}\n{promptText}";
        }
        labelText.text = text;
    }
}
