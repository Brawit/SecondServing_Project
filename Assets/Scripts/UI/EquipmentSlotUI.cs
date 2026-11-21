using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ช่องสวมใส่ 1 ช่องในหน้าต่างกระเป๋า
// - ลากของจากกระเป๋ามาวาง = สวม (ของเดิมในช่องกลับเข้ากระเป๋า)
// - ลากช่องนี้ไปวางอีกช่องที่รับได้ (เช่น แหวน 1 ↔ แหวน 2) = สลับ
// - ลากช่องนี้ออกไปที่อื่นแล้วปล่อย = ถอด (กลับเข้ากระเป๋า)
// - คลิกซ้าย = ดูรายละเอียดในแผงด้านขวา
public class EquipmentSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("References")]
    [Tooltip("รูปไอคอนของไอเทมที่สวมอยู่")]
    [SerializeField] private Image iconImage;

    [Tooltip("ข้อความชื่อช่อง เช่น Ring, Head (แสดงตอนช่องว่าง)")]
    [SerializeField] private TMP_Text labelText;

    [Tooltip("กรอบที่แสดงตอนช่องนี้ถูกเลือก (เว้นว่างได้)")]
    [SerializeField] private GameObject selectionFrame;

    [Header("Colors")]
    [Tooltip("สีสี่เหลี่ยมแทนไอคอน เมื่อยังไม่มีรูป")]
    [SerializeField] private Color placeholderColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    [Tooltip("ความโปร่งของไอคอนในช่องต้นทาง ระหว่างกำลังลาก")]
    [Range(0f, 1f)]
    [SerializeField] private float draggingAlpha = 0.3f;

    [Tooltip("ความโปร่งของชื่อช่อง เมื่อช่องว่าง")]
    [Range(0f, 1f)]
    [SerializeField] private float emptyLabelAlpha = 0.5f;

    // ช่องนี้คือช่องไหน ตั้งโดย EquipmentPanelUI
    public EquipSlot Slot { get; private set; }

    public ItemData CurrentItem { get; private set; }

    // ส่งสัญญาณเมื่อคลิกช่องนี้
    public event Action<EquipmentSlotUI> OnClicked;

    private PlayerEquipment equipment;
    private bool isDragging;

    // การลากล่าสุดถูกวางลงช่องสวมใส่สำเร็จหรือไม่ (OnDrop ของปลายทางถูกเรียกก่อน OnEndDrag ของต้นทาง)
    private static bool lastDropHandled;

    public void Setup(EquipSlot slot, PlayerEquipment owner)
    {
        Slot = slot;
        equipment = owner;
    }

    // แสดงไอเทมที่สวมอยู่ (item = null คือช่องว่าง)
    public void SetItem(ItemData item)
    {
        CurrentItem = item;

        if (iconImage != null)
        {
            if (item == null)
            {
                iconImage.enabled = false;
                iconImage.sprite = null;
            }
            else
            {
                iconImage.enabled = true;
                iconImage.sprite = item.icon;
                iconImage.preserveAspect = item.icon != null;

                Color color = item.icon != null ? Color.white : placeholderColor;
                if (isDragging)
                {
                    color.a = draggingAlpha;
                }
                iconImage.color = color;
            }
        }

        if (labelText != null)
        {
            // ช่องว่าง: แสดงชื่อช่องจางๆ / มีของ: แสดงชื่อไอเทม
            labelText.text = item != null ? item.itemName : PlayerEquipment.GetSlotLabel(Slot);
            Color c = labelText.color;
            c.a = item != null ? 1f : emptyLabelAlpha;
            labelText.color = c;
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectionFrame != null)
        {
            selectionFrame.SetActive(selected);
        }
    }

    // ---------- คลิก ----------

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            OnClicked?.Invoke(this);
        }
    }

    // ---------- ลากออกจากช่องนี้ ----------

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || CurrentItem == null || equipment == null)
        {
            return;
        }

        isDragging = true;
        lastDropHandled = false;
        DragIconUI.Begin(this, CurrentItem, eventData.position);
        SetItem(CurrentItem);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isDragging)
        {
            DragIconUI.Move(eventData.position);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        DragIconUI.End();

        // ไม่ได้วางลงช่องสวมใส่ช่องไหนเลย = ถอดกลับเข้ากระเป๋า
        if (!lastDropHandled && equipment != null)
        {
            equipment.TryUnequip(Slot);
        }

        lastDropHandled = false;

        // วาดช่องนี้ใหม่ (คืนความทึบของไอคอน)
        if (equipment != null)
        {
            SetItem(equipment.GetSlot(Slot));
        }
    }

    // ---------- รับของที่ลากมาวาง ----------

    public void OnDrop(PointerEventData eventData)
    {
        if (equipment == null || eventData.pointerDrag == null)
        {
            return;
        }

        // มาจากช่องสวมใส่อื่น = สลับ
        EquipmentSlotUI fromSlot = eventData.pointerDrag.GetComponent<EquipmentSlotUI>();
        if (fromSlot != null)
        {
            if (!fromSlot.isDragging)
            {
                return;
            }

            // วางลงช่องสวมใส่แล้ว ไม่ต้องถอด (แม้สลับไม่ได้ ของจะอยู่ช่องเดิม)
            lastDropHandled = true;
            if (fromSlot != this)
            {
                equipment.TrySwap(fromSlot.Slot, Slot);
            }
            return;
        }

        // มาจากกระเป๋า = สวม
        InventorySlotUI fromBag = eventData.pointerDrag.GetComponent<InventorySlotUI>();
        if (fromBag != null && fromBag.CurrentItem != null)
        {
            equipment.TryEquip(Slot, fromBag.CurrentItem);
        }
    }

    // ถ้าช่องถูกปิดระหว่างลาก (เช่น ปิดกระเป๋า) ให้ซ่อนไอคอน ของยังสวมอยู่เหมือนเดิม
    private void OnDisable()
    {
        if (isDragging)
        {
            isDragging = false;
            lastDropHandled = false;
            DragIconUI.End();
        }
    }
}
