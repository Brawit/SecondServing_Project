using TMPro;
using UnityEngine;

// [2.6] เคาน์เตอร์ในบ้านช่าง: กด E = คุยเรื่องก่อสร้าง (เปิด ConstructionUI)
// - ช่างเปิดรับงาน openHour–closeHour (ตั้งต้น 09:00–16:00) นอกเวลา = แจ้งว่าปิด
// - ตอนนี้ยังไม่มีบ้านช่าง/NPC ช่าง: วางวัตถุนี้ไว้ที่ไหนก็ได้ (Build Economy วางข้าง OutsideFront) ย้ายเข้าบ้านช่างตอนมีแผนที่จริง
// ต้องมี Collider2D บนวัตถุเดียวกัน
public class BuilderCounter : MonoBehaviour, IInteractable
{
    [Header("References")]
    [Tooltip("ข้อความเหนือเคาน์เตอร์ (TextMeshPro แบบ 3D)")]
    [SerializeField] private TMP_Text labelText;

    [Header("Hours")]
    [Tooltip("ชั่วโมงที่เปิด (0–23)")]
    [Range(0, 23)] [SerializeField] private int openHour = 9;

    [Tooltip("ชั่วโมงที่ปิด (เปิดถึงก่อนเวลานี้)")]
    [Range(1, 24)] [SerializeField] private int closeHour = 16;

    [Header("Prompt")]
    [SerializeField] private string promptText = "[E] Builder";
    [Min(0.5f)] [SerializeField] private float promptDistance = 1.6f;

    [Header("Text")]
    [Tooltip("นอกเวลา ({0} = เวลาเปิด, {1} = เวลาปิด)")]
    [SerializeField] private string closedFormat = "The builder's shop is closed. Open {0}:00–{1}:00.";

    [SerializeField] private string noWindowMessage = "No builder window in the scene. Run Build Economy.";

    [Tooltip("ช่างติดงานอยู่ ({0} = ชื่องาน, {1} = วันที่เหลือ) ไม่เปิดหน้าต่างช่าง")]
    [SerializeField] private string busyFormat = "The builder is busy working on {0}. Come back in {1} day(s).";

    [Tooltip("จ้างแล้วแต่ยังไม่เริ่ม ({0} = ชื่องาน, {1} = จำนวนวันที่ใช้)")]
    [SerializeField] private string startsTomorrowFormat = "The builder will start on {0} tomorrow. It takes {1} day(s).";

    private Transform player;
    private bool promptShown;

    public bool IsOpenNow
    {
        get
        {
            if (GameClock.Instance == null) return true;
            int hour = GameClock.Instance.Hour;
            return hour >= openHour && hour < closeHour;
        }
    }

    private void Awake()
    {
        if (GetComponent<Collider2D>() == null)
        {
            Debug.LogWarning($"BuilderCounter '{name}': Needs a Collider2D to be found by the player.", this);
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
        return !ConstructionUI.IsAnyOpen && !MailUI.IsAnyOpen && !CookingSession.IsBusy && !MenuSelectionUI.IsAnyOpen && !CookingMenuUI.IsAnyOpen;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (interactor != null) player = interactor.transform;

        if (!IsOpenNow)
        {
            NotificationUI.Post(string.Format(closedFormat, openHour.ToString("00"), closeHour.ToString("00")));
            return;
        }

        // [2.6] ช่างติดงาน = ไม่เปิดหน้าต่าง บอกว่ายุ่งอยู่ (ผู้ใช้ขอ)
        ConstructionManager manager = ConstructionManager.Instance;
        if (manager != null && manager.IsBuilding)
        {
            string format = manager.StartsTomorrow ? startsTomorrowFormat : busyFormat;
            NotificationUI.Post(string.Format(format, manager.Current.displayName, manager.DaysLeft));
            return;
        }

        if (ConstructionUI.Instance == null)
        {
            NotificationUI.Post(noWindowMessage);
            return;
        }

        ConstructionUI.Instance.Open();
    }

    private void Update()
    {
        bool show = !string.IsNullOrEmpty(promptText) && player != null && !ConstructionUI.IsAnyOpen &&
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
