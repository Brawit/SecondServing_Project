using UnityEngine;

// เตียงนอน: กด E สองครั้งเพื่อนอน (ครั้งแรกถามยืนยัน กันกดพลาด) แล้วจบวัน
// ต้องมี Collider2D (Is Trigger) บนวัตถุเดียวกัน
public class Bed : MonoBehaviour, IInteractable
{
    [Header("Confirm")]
    [Tooltip("ต้องกด E ซ้ำเพื่อยืนยันการนอน")]
    [SerializeField] private bool requireConfirm = true;

    [Tooltip("ต้องกดซ้ำภายในกี่วินาที")]
    [SerializeField] private float confirmWindowSeconds = 3f;

    [Tooltip("ข้อความถามยืนยัน")]
    [SerializeField] private string confirmMessage = "Press E again to go to sleep.";

    // เวลาที่กดครั้งแรก (ใช้เวลาจริง ไม่ขึ้นกับการหยุดเกม)
    private float firstPressTime = -999f;

    private void Awake()
    {
        if (GetComponent<Collider2D>() == null) Debug.LogWarning($"Bed '{name}': Needs a Collider2D (Is Trigger) to be found by the player.", this);
    }

    // ---------- IInteractable ----------

    public bool CanInteract(PlayerInteractor interactor)
    {
        return DayEndManager.Instance != null && !DayEndManager.Instance.IsDayEnding;
    }

    public void Interact(PlayerInteractor interactor)
    {
        // เช็กเงื่อนไขก่อนถามยืนยัน จะได้ไม่ถามแล้วค่อยบอกว่านอนไม่ได้
        if (!DayEndManager.Instance.CanSleep(out string reason))
        {
            NotificationUI.Post(reason);
            return;
        }

        if (requireConfirm)
        {
            bool confirmed = Time.unscaledTime - firstPressTime <= confirmWindowSeconds;
            if (!confirmed)
            {
                firstPressTime = Time.unscaledTime;
                NotificationUI.Post(confirmMessage, confirmWindowSeconds);
                return;
            }
        }

        firstPressTime = -999f;
        DayEndManager.Instance.RequestSleep();
    }
}
