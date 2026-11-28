using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// [2.6b] แถวของหนึ่งกองในแผงขาย (SellPanelUI สร้างจากแถวต้นแบบ)
// จานแยกแถวตามคุณภาพ / [v2] และตามแบบ เช่น "Sushi (Salmon) · Perfect" ราคาต่อชิ้นจาก DishPricing (ราคาแบบ × ดาว × คุณภาพ)
// คลิกซ้ายหรือขวาส่งกลับไปให้แผงตัดสินว่าขายกี่ชิ้น
public class SellItemSlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [Tooltip("ใช้แค่ให้มีสีตอนชี้/กด (ไม่ได้ผูก onClick)")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;

    [Tooltip("จำนวน + ราคาต่อชิ้น")]
    [SerializeField] private TMP_Text countText;

    [Tooltip("กรอบตอนชี้ (ไม่จำเป็น)")]
    [SerializeField] private GameObject selectionFrame;

    [Header("Text")]
    [Tooltip("{0} = จำนวน, {1} = ราคาต่อชิ้น")]
    [SerializeField] private string countFormat = "x{0}   {1} each";

    [Tooltip("[v2] ชื่อแถวของจาน {0} = ชื่อจาน (รวมแบบ เช่น Sushi (Salmon)), {1} = คุณภาพ")]
    [SerializeField] private string dishNameFormat = "{0} · {1}";

    private Action<SellItemSlotUI, PointerEventData.InputButton> onClicked;

    // [v2] กรอบไฮไลท์ตอนเมาส์วาง (ใส่ด้วยโค้ด)
    private HoverHighlight hover;
    private HoverHighlight Hover => hover != null ? hover : (hover = HoverHighlight.Ensure(gameObject));

    public ItemData Item { get; private set; }
    public DishQuality Quality { get; private set; }
    public int Count { get; private set; }

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (selectionFrame != null) selectionFrame.SetActive(false);
    }

    public void Bind(ItemData item, DishQuality quality, int count, Action<SellItemSlotUI, PointerEventData.InputButton> clickCallback)
    {
        Item = item;
        Quality = quality;
        Count = count;
        onClicked = clickCallback;

        // [v2] กรอบไฮไลท์: แถวที่มีของ = ขึ้น (คลิกขายได้)
        Hover.Interactable = item != null;

        if (item == null) return;

        string itemName = item.itemName;
        if (Inventory.TracksQuality(item)) itemName = string.Format(dishNameFormat, item.itemName, DishQualityUtil.ColoredLabel(quality));

        if (nameText != null) nameText.text = itemName;
        if (countText != null) countText.text = string.Format(countFormat, count, PlayerWallet.Format(SalesLedger.UnitSellPrice(item, quality)));
        if (iconImage != null)
        {
            iconImage.sprite = item.icon;
            iconImage.enabled = item.icon != null;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Item == null || eventData == null) return;
        if (eventData.button == PointerEventData.InputButton.Middle) return;
        onClicked?.Invoke(this, eventData.button);
    }
}
