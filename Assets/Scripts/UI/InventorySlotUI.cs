using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ช่องไอเทม 1 ช่องในหน้าต่างกระเป๋า: แสดงไอคอน ชื่อ และจำนวน
public class InventorySlotUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("รูปไอคอนของไอเทม")]
    [SerializeField] private Image iconImage;

    [Tooltip("ข้อความชื่อไอเทม")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("ข้อความจำนวน เช่น x3")]
    [SerializeField] private TMP_Text countText;

    [Header("Placeholder")]
    [Tooltip("สีสี่เหลี่ยมแทนไอคอน เมื่อ ItemData ยังไม่มีรูป")]
    [SerializeField] private Color placeholderColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    // ไอเทมที่ช่องนี้แสดงอยู่ (null = ช่องว่าง)
    public ItemData CurrentItem { get; private set; }

    // ตั้งให้ช่องนี้แสดงไอเทมและจำนวน
    public void SetItem(ItemData item, int count)
    {
        if (item == null)
        {
            SetEmpty();
            return;
        }

        CurrentItem = item;

        if (iconImage != null)
        {
            iconImage.enabled = true;

            if (item.icon != null)
            {
                // มีรูปจริง: แสดงรูปด้วยสีปกติ
                iconImage.sprite = item.icon;
                iconImage.color = Color.white;
                iconImage.preserveAspect = true;
            }
            else
            {
                // ยังไม่มีรูป: แสดงเป็นสี่เหลี่ยมสีเทาแทน
                iconImage.sprite = null;
                iconImage.color = placeholderColor;
            }
        }

        if (nameText != null)
        {
            nameText.text = item.itemName;
        }

        if (countText != null)
        {
            // ถ้ามีแค่ชิ้นเดียวไม่ต้องแสดงตัวเลข จะได้ดูสะอาดตา
            countText.text = count > 1 ? $"x{count}" : "";
        }
    }

    // ตั้งให้ช่องนี้เป็นช่องว่าง
    public void SetEmpty()
    {
        CurrentItem = null;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (nameText != null)
        {
            nameText.text = "";
        }

        if (countText != null)
        {
            countText.text = "";
        }
    }
}
