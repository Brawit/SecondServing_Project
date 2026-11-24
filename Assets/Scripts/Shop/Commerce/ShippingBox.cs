using TMPro;
using UnityEngine;

// [2.6b] กล่องขายข้ามคืนแบบ Stardew: กด E = เปิด ShippingBoxUI
// - ใช้ได้ทุกเวลา ยกเว้นระหว่างเปิดร้านอาหาร / หน้าสรุปวัน / มีหน้าต่างอื่นเปิดอยู่
// - ของในกล่องขายตอนจบวัน (SalesLedger.SettleDay) เงินขึ้นหน้าสรุปวัน
// ต้องมี Collider2D บนวัตถุเดียวกัน
public class ShippingBox : MonoBehaviour, IInteractable
{
    [Header("References")]
    [Tooltip("ข้อความเหนือกล่อง (TextMeshPro แบบ 3D)")]
    [SerializeField] private TMP_Text labelText;

    [Header("Prompt")]
    [SerializeField] private string labelIdle = "Shipping Box";
    [SerializeField] private string labelPrompt = "[E] Shipping Box";
    [Tooltip("ป้ายตอนมีของในกล่อง ({0} = ยอดเงิน)")]
    [SerializeField] private string labelFilledFormat = "Shipping Box ({0})";
    [Min(0.5f)] [SerializeField] private float promptDistance = 1.6f;

    [Header("Text")]
    [SerializeField] private string noWindowMessage = "No shipping box window in the scene. Run Build Shops.";

    private Transform player;
    private bool promptShown;

    private void Awake()
    {
        if (GetComponent<Collider2D>() == null)
        {
            Debug.LogWarning($"ShippingBox '{name}': Needs a Collider2D to be found by the player.", this);
        }
    }

    private void Start()
    {
        PlayerInteractor interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor != null) player = interactor.transform;
        if (SalesLedger.Instance != null) SalesLedger.Instance.OnChanged += RefreshLabel;
        RefreshLabel();
    }

    private void OnDestroy()
    {
        if (SalesLedger.Instance != null) SalesLedger.Instance.OnChanged -= RefreshLabel;
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        return !ShopPurchase.IsCommerceBlocked();
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (interactor != null) player = interactor.transform;

        if (ShippingBoxUI.Instance == null)
        {
            NotificationUI.Post(noWindowMessage);
            return;
        }

        ShippingBoxUI.Instance.Open();
    }

    private void Update()
    {
        bool show = player != null && !ShippingBoxUI.IsAnyOpen &&
                    Vector2.Distance(player.position, transform.position) <= promptDistance;
        if (show != promptShown)
        {
            promptShown = show;
            RefreshLabel();
        }
    }

    private void RefreshLabel()
    {
        if (labelText == null) return;
        if (promptShown)
        {
            labelText.text = labelPrompt;
            return;
        }

        int total = SalesLedger.Instance != null ? SalesLedger.Instance.BoxTotal : 0;
        labelText.text = total > 0 ? string.Format(labelFilledFormat, PlayerWallet.Format(total)) : labelIdle;
    }
}
