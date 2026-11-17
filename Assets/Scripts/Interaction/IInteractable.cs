// สิ่งที่ผู้เล่นกด E ใช้งานได้ เช่น ของป่า ประตู เคาน์เตอร์ เตียง
// วัตถุที่ใช้ interface นี้ต้องมี Collider2D (ติ๊ก Is Trigger) เพื่อให้ PlayerInteractor หาเจอ
public interface IInteractable
{
    // ตอนนี้ใช้งานได้หรือไม่ (ถ้า false ผู้เล่นจะข้ามไปหาชิ้นที่ใกล้รองลงมา)
    bool CanInteract(PlayerInteractor interactor);

    // ทำงานเมื่อผู้เล่นกด E
    void Interact(PlayerInteractor interactor);
}
