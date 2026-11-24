using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [2.6b] แถวของหนึ่งรายการในกล่องขายข้ามคืน (ShippingBoxUI สร้างจากแถวต้นแบบ)
// ชื่อ (+ คุณภาพ) x จำนวน = ราคา / ปุ่มเอาคืน 1 และเอาคืนทั้งหมด
public class BoxEntryRowUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button takeOneButton;
    [SerializeField] private Button takeAllButton;

    private int index;
    private Action<int, int> onTakeBack;

    private void Awake()
    {
        if (takeOneButton != null) takeOneButton.onClick.AddListener(() => onTakeBack?.Invoke(index, 1));
        if (takeAllButton != null) takeAllButton.onClick.AddListener(() => onTakeBack?.Invoke(index, int.MaxValue));
    }

    public void Bind(int entryIndex, BoxEntry entry, Action<int, int> takeBackCallback)
    {
        index = entryIndex;
        onTakeBack = takeBackCallback;

        string itemName = entry.item != null ? entry.item.itemName : "(missing)";
        if (Inventory.TracksQuality(entry.item)) itemName += $" ({DishQualityUtil.Label(entry.quality)})";

        if (nameText != null) nameText.text = $"{itemName} x{entry.amount}";
        if (priceText != null) priceText.text = PlayerWallet.Format(SalesLedger.UnitSellPrice(entry.item, entry.quality) * entry.amount);
    }
}
