using UnityEngine;

// ของป่าบนพื้น ผู้เล่นกด E เก็บผ่าน PlayerInteractor
public class ForageItem : MonoBehaviour, IInteractable
{
    [Header("Item")]
    [SerializeField] private ItemData itemData; // วัตถุดิบชนิดไหน
    [SerializeField] private int amount = 1;    // ให้กี่ชิ้น

    // ให้ระบบอื่นอ่านได้ว่าเป็นของอะไร (ระบบเซฟใช้ itemId จากตรงนี้)
    public ItemData ItemData => itemData;
    public int Amount => amount;

    // ---------- IInteractable ----------

    public bool CanInteract(PlayerInteractor interactor)
    {
        return itemData != null && interactor != null && interactor.Inventory != null;
    }

    public void Interact(PlayerInteractor interactor)
    {
        Collect(interactor.Inventory);
    }

    // ---------- การเก็บ ----------

    // เก็บของเข้ากระเป๋า คืนค่า true ถ้าเก็บสำเร็จ
    public bool Collect(Inventory inventory)
    {
        if (itemData == null)
        {
            Debug.LogWarning("ForageItem has no ItemData assigned!", this);
            return false;
        }

        if (!inventory.AddItem(itemData, amount)) return false;

        Debug.Log($"Collected: {itemData.itemName} x{amount}");
        Destroy(gameObject); // ของหายไปจากพื้น
        return true;
    }
}
