using UnityEngine;

// เคาน์เตอร์ร้าน: กด E เพื่อเปิดร้าน (ใช้ได้ช่วงที่ร้านเปิดได้เท่านั้น)
// ต้องมี Collider2D (Is Trigger) บนวัตถุเดียวกัน
public class ShopCounter : MonoBehaviour, IInteractable
{
    private void Awake()
    {
        if (GetComponent<Collider2D>() == null) Debug.LogWarning($"ShopCounter '{name}': Needs a Collider2D (Is Trigger) to be found by the player.", this);
    }

    // ---------- IInteractable ----------

    public bool CanInteract(PlayerInteractor interactor)
    {
        // ระหว่างเปิดร้าน ไม่ให้เคาน์เตอร์แย่งปุ่ม E (เผื่ออนาคตมีจุดทำอาหารอยู่ใกล้ๆ)
        return ShopManager.Instance != null && !ShopManager.Instance.IsServing;
    }

    public void Interact(PlayerInteractor interactor)
    {
        // ShopManager จะแจ้งเตือนเองถ้ายังเปิดไม่ได้
        ShopManager.Instance.TryOpenShop();
    }
}
