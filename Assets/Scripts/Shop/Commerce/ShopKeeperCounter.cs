using TMPro;
using UnityEngine;

// [2.6b] เคาน์เตอร์ร้านค้า: กด E = เปิดหน้าต่างร้าน (ShopUI) ตามไฟล์รายการ ShopCatalog
// - เวลาเปิดอ่านจาก catalog (ตั้งต้น 09:00–17:00) นอกเวลา = แจ้งว่าปิด
// - ใช้ไม่ได้ระหว่างเปิดร้านอาหาร / หน้าสรุปวัน / มีหน้าต่างอื่นเปิดอยู่
// - ตอนนี้ยังไม่มี NPC เจ้าของร้าน: วางไว้ใกล้เคาน์เตอร์ช่างไปก่อน ย้ายตอนมีแผนที่จริง
// ต้องมี Collider2D บนวัตถุเดียวกัน
public class ShopKeeperCounter : MonoBehaviour, IInteractable
{
    [Header("Shop")]
    [SerializeField] private ShopCatalog catalog;

    [Header("References")]
    [Tooltip("ข้อความเหนือเคาน์เตอร์ (TextMeshPro แบบ 3D)")]
    [SerializeField] private TMP_Text labelText;

    [Header("Prompt")]
    [SerializeField] private string promptFormat = "[E] {0}";
    [Min(0.5f)] [SerializeField] private float promptDistance = 1.6f;

    [Header("Text")]
    [Tooltip("นอกเวลา ({0} = ชื่อร้าน, {1} = เวลาเปิด, {2} = เวลาปิด)")]
    [SerializeField] private string closedFormat = "{0} is closed. Open {1}:00–{2}:00.";
    [SerializeField] private string noWindowMessage = "No shop window in the scene. Run Build Shops.";
    [SerializeField] private string noCatalogMessage = "This shop has no catalog.";

    [Header("Debug")]
    [Tooltip("เปิดได้ทุกเวลา (ทดสอบ)")]
    [SerializeField] private bool ignoreHours;

    private Transform player;
    private bool promptShown;

    public ShopCatalog Catalog => catalog;

    public bool IsOpenNow
    {
        get
        {
            if (ignoreHours || catalog == null || GameClock.Instance == null) return true;
            return catalog.IsOpenAt(GameClock.Instance.Hour);
        }
    }

    private void Awake()
    {
        if (GetComponent<Collider2D>() == null)
        {
            Debug.LogWarning($"ShopKeeperCounter '{name}': Needs a Collider2D to be found by the player.", this);
        }
        if (catalog == null) Debug.LogWarning($"ShopKeeperCounter '{name}': Catalog is not assigned.", this);
    }

    private void Start()
    {
        PlayerInteractor interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor != null) player = interactor.transform;
        RefreshLabel();
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return !ShopPurchase.IsCommerceBlocked();
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (interactor != null) player = interactor.transform;

        if (catalog == null)
        {
            NotificationUI.Post(noCatalogMessage);
            return;
        }

        if (!IsOpenNow)
        {
            NotificationUI.Post(string.Format(closedFormat, catalog.shopName, catalog.openHour.ToString("00"), catalog.closeHour.ToString("00")));
            return;
        }

        if (ShopUI.Instance == null)
        {
            NotificationUI.Post(noWindowMessage);
            return;
        }

        ShopUI.Instance.Open(catalog);
    }

    private void Update()
    {
        bool show = player != null && !ShopUI.IsAnyOpen &&
                    Vector2.Distance(player.position, transform.position) <= promptDistance;
        if (show != promptShown)
        {
            promptShown = show;
            RefreshLabel();
        }
    }

    // ป้ายแสดงชื่อร้านตลอด (ยังไม่มีภาพ) ใกล้ๆ = มี [E]
    private void RefreshLabel()
    {
        if (labelText == null) return;
        string shopName = catalog != null ? catalog.shopName : name;
        labelText.text = promptShown ? string.Format(promptFormat, shopName) : shopName;
    }
}
