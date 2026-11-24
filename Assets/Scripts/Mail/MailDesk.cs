using TMPro;
using UnityEngine;

// [2.5c] โต๊ะคอมในห้องนอน: กด E = เปิดกล่องจดหมายที่เคยได้ทั้งหมด (อ่านซ้ำ / รับของที่ยังไม่ได้รับ)
// เฟส 3: คอมบนโต๊ะนี้ใช้ต่อได้ เช่น อ่านรีวิวร้านออนไลน์ (ตัวเอกเรียน Computer Science)
// ต้องมี Collider2D บนวัตถุเดียวกัน
// สร้าง prefab ได้จาก Tools > Second Serving > Build Mail System (Assets/Prefabs/MailDesk.prefab)
public class MailDesk : MonoBehaviour, IInteractable
{
    [Header("References")]
    [Tooltip("ข้อความเหนือโต๊ะ (TextMeshPro แบบ 3D)")]
    [SerializeField] private TMP_Text labelText;

    [Header("Prompt")]
    [Tooltip("ข้อความชวนกด E เมื่อผู้เล่นอยู่ใกล้ (เว้นว่าง = ไม่แสดง)")]
    [SerializeField] private string promptText = "[E] Letters";

    [Tooltip("ระยะที่แสดงข้อความชวนกด E")]
    [Min(0.5f)] [SerializeField] private float promptDistance = 1.6f;

    [Header("Text")]
    [Tooltip("แจ้งเตือนเมื่อกดระหว่างเปิดร้าน")]
    [SerializeField] private string servingMessage = "Not now, the shop is open.";

    [Tooltip("แจ้งเตือนเมื่อไม่มีหน้าต่างจดหมายในฉาก")]
    [SerializeField] private string noWindowMessage = "No mail window in the scene. Run Build Mail System.";

    private Transform player;
    private bool promptShown;

    private void Awake()
    {
        if (GetComponent<Collider2D>() == null)
        {
            Debug.LogWarning($"MailDesk '{name}': Needs a Collider2D to be found by the player.", this);
        }
    }

    private void Start()
    {
        PlayerInteractor interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor != null) player = interactor.transform;
        RefreshLabel();
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return !MailUI.IsAnyOpen && !ConstructionUI.IsAnyOpen && !CookingSession.IsBusy && !MenuSelectionUI.IsAnyOpen && !CookingMenuUI.IsAnyOpen;
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

        MailUI.Instance.OpenArchive();
    }

    private void Update()
    {
        bool show = !string.IsNullOrEmpty(promptText) && player != null && !MailUI.IsAnyOpen && !CookingSession.IsBusy &&
                    Vector2.Distance(player.position, transform.position) <= promptDistance;
        if (show != promptShown)
        {
            promptShown = show;
            RefreshLabel();
        }
    }

    private void RefreshLabel()
    {
        if (labelText != null)
        {
            labelText.text = promptShown ? promptText : "";
        }
    }
}
