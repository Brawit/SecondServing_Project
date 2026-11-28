using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [2.6b] แถวของหนึ่งรายการในกล่องขายข้ามคืน (ShippingBoxUI สร้างจากแถวต้นแบบ)
// ชื่อ (+ คุณภาพ) x จำนวน = ราคา / ปุ่มเอาคืน 1 และเอาคืนทั้งหมด
// [v2] จานแบบแยกแถว เช่น "Sushi (Tilapia) · OK x2" ราคาจาก DishPricing
public class BoxEntryRowUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button takeOneButton;
    [SerializeField] private Button takeAllButton;

    [Header("Text")]
    [Tooltip("[v2] ชื่อแถวของจาน {0} = ชื่อจาน (รวมแบบ เช่น Sushi (Salmon)), {1} = คุณภาพ")]
    [SerializeField] private string dishNameFormat = "{0} · {1}";

    private int index;
    private Action<int, int> onTakeBack;

    // [v2] กรอบไฮไลท์ตอนเมาส์วาง (ใส่ด้วยโค้ด)
    private HoverHighlight hover;

    private void Awake()
    {
        if (takeOneButton != null) takeOneButton.onClick.AddListener(() => onTakeBack?.Invoke(index, 1));
        if (takeAllButton != null) takeAllButton.onClick.AddListener(() => onTakeBack?.Invoke(index, int.MaxValue));
    }

    public void Bind(int entryIndex, BoxEntry entry, Action<int, int> takeBackCallback)
    {
        index = entryIndex;
        onTakeBack = takeBackCallback;

        // [v2] กรอบไฮไลท์: ทุกแถวในกล่องขึ้น (มีปุ่มเอาคืนกดได้เสมอ)
        if (hover == null) hover = HoverHighlight.Ensure(gameObject);
        hover.Interactable = true;

        string itemName = entry.item != null ? entry.item.itemName : "(missing)";
        if (Inventory.TracksQuality(entry.item)) itemName = string.Format(dishNameFormat, itemName, DishQualityUtil.ColoredLabel(entry.quality));

        if (nameText != null) nameText.text = $"{itemName} x{entry.amount}";
        if (priceText != null) priceText.text = PlayerWallet.Format(SalesLedger.UnitSellPrice(entry.item, entry.quality) * entry.amount);
    }
}
