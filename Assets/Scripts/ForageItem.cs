using UnityEngine;

public class ForageItem : MonoBehaviour
{
    [Header("Item")]
    [SerializeField] private ItemData itemData; // วัตถุดิบชนิดไหน
    [SerializeField] private int amount = 1;    // ให้กี่ชิ้น

    // ผู้เล่นเรียกใช้ตอนกดเก็บ คืนค่า true ถ้าเก็บสำเร็จ
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