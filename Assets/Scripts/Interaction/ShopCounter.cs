using UnityEngine;

// เคาน์เตอร์ร้าน: กด E ช่วงที่ร้านเปิดได้ (17:00–19:00) เพื่อเปิดหน้าต่างเลือกเมนู แล้วกด Open Shop เพื่อเปิดร้าน
// ถ้าในฉากยังไม่มีหน้าต่างเลือกเมนู จะเปิดร้านทันทีเหมือนเดิม (มีคำเตือนใน Console)
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
        // ระหว่างเปิดหน้าต่างเลือกเมนู ก็ไม่รับ E ซ้ำ
        return ShopManager.Instance != null && !ShopManager.Instance.IsServing && !MenuSelectionUI.IsAnyOpen;
    }

    public void Interact(PlayerInteractor interactor)
    {
        ShopManager shop = ShopManager.Instance;

        // ช่วงที่เปิดร้านได้: ให้เลือกเมนูก่อน
        if (shop.State == ShopState.CanOpen)
        {
            if (MenuSelectionUI.Instance != null && MenuSelectionUI.Instance.Open())
            {
                return;
            }

            Debug.LogWarning("ShopCounter: No Daily Menu window found. Opening the shop without a menu.", this);
        }

        // ช่วงอื่น ShopManager จะแจ้งเตือนเองว่ายังเปิดไม่ได้
        shop.TryOpenShop();
    }
}
