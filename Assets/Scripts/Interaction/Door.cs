using UnityEngine;

// ประตูวาร์ป: กด E แล้วย้ายผู้เล่นไปจุดปลายทาง พร้อมเปลี่ยนขอบกล้อง
// ต้องมี Collider2D (Is Trigger) บนวัตถุเดียวกัน
public class Door : MonoBehaviour, IInteractable
{
    [Header("Destination")]
    [Tooltip("จุดที่ผู้เล่นจะไปโผล่ (วัตถุเปล่าวางไว้หน้าประตูอีกฝั่ง)")]
    [SerializeField] private Transform destination;

    [Tooltip("ขอบกล้องของพื้นที่ปลายทาง (เช่น CameraBoundsMap หรือ CameraBounds_Shop)")]
    [SerializeField] private Collider2D destinationCameraBounds;

    [Header("Rules")]
    [Tooltip("ห้ามใช้ประตูนี้ระหว่างเปิดร้าน (ติ๊กไว้กับประตูทางออกจากร้าน)")]
    [SerializeField] private bool blockedWhileServing = true;

    [Tooltip("ข้อความแจ้งเตือนตอนใช้ประตูไม่ได้เพราะเปิดร้านอยู่")]
    [SerializeField] private string blockedMessage = "You can't leave while the shop is open.";

    private void Awake()
    {
        if (destination == null) Debug.LogWarning($"Door '{name}': Destination is not assigned.", this);
        if (GetComponent<Collider2D>() == null) Debug.LogWarning($"Door '{name}': Needs a Collider2D (Is Trigger) to be found by the player.", this);
    }

    // ---------- IInteractable ----------

    public bool CanInteract(PlayerInteractor interactor)
    {
        return destination != null && interactor != null && interactor.Teleporter != null;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (blockedWhileServing && ShopManager.Instance != null && ShopManager.Instance.IsServing)
        {
            NotificationUI.Post(blockedMessage);
            return;
        }

        interactor.Teleporter.TeleportTo(destination.position, destinationCameraBounds);
        Debug.Log($"Door '{name}' used.");
    }

    // วาดเส้นจากประตูไปจุดปลายทางใน Scene view
    private void OnDrawGizmos()
    {
        if (destination == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, destination.position);
        Gizmos.DrawWireSphere(destination.position, 0.25f);
    }
}
