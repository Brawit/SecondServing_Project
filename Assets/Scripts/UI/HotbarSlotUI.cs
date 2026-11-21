using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ช่อง 1 ช่องของ Hotbar: ไอคอน จำนวน เลขปุ่ม และกรอบตอนถูกเลือก
// - คลิกซ้าย = เลือกช่องนี้
// - วางของจากกระเป๋าลงช่องนี้ = ใส่ทางลัด (ถ้าของนั้นอยู่ช่องอื่น จะย้ายมาช่องนี้)
// - ลากช่องนี้ไปวางอีกช่อง = สลับกัน
// - ลากช่องนี้ออกไปนอก Hotbar แล้วปล่อย = เอาทางลัดออก (ของในกระเป๋าไม่หาย)
// ลากไม่ได้ระหว่างเปิดร้าน
public class HotbarSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("References")]
    [Tooltip("รูปไอคอนของไอเทม")]
    [SerializeField] private Image iconImage;

    [Tooltip("ข้อความจำนวน (มุมขวาล่าง)")]
    [SerializeField] private TMP_Text countText;

    [Tooltip("ข้อความเลขปุ่ม (มุมซ้ายบน)")]
    [SerializeField] private TMP_Text keyText;

    [Tooltip("กรอบที่แสดงตอนช่องนี้ถูกเลือก")]
    [SerializeField] private GameObject selectionFrame;

    [Header("Colors")]
    [Tooltip("สีสี่เหลี่ยมแทนไอคอน เมื่อยังไม่มีรูป")]
    [SerializeField] private Color placeholderColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    [Tooltip("ความโปร่งของไอคอน เมื่อของในกระเป๋าเหลือ 0")]
    [Range(0f, 1f)]
    [SerializeField] private float emptyAlpha = 0.3f;

    [Tooltip("ความโปร่งของไอคอนในช่องต้นทาง ระหว่างกำลังลาก")]
    [Range(0f, 1f)]
    [SerializeField] private float draggingAlpha = 0.3f;

    [Header("Messages")]
    [Tooltip("ข้อความตอนลากของที่ใส่ Hotbar ไม่ได้มาวาง ({0} = ชื่อ)")]
    [SerializeField] private string cannotAssignFormat = "{0} can't go in the hotbar.";

    // ช่องนี้คือช่องที่เท่าไร (0–9) ตั้งโดย HotbarUI
    public int Index { get; private set; }

    public ItemData CurrentItem { get; private set; }

    // ส่งสัญญาณเมื่อคลิกช่องนี้
    public event Action<HotbarSlotUI> OnClicked;

    private Hotbar hotbar;
    private bool isDragging;

    // การลากล่าสุดถูกวางลงช่อง Hotbar สำเร็จหรือไม่ (OnDrop ของปลายทางถูกเรียกก่อน OnEndDrag ของต้นทาง)
    private static bool lastDropHandled;

    // ตั้งเลขช่อง ข้อความปุ่ม และ Hotbar ที่ช่องนี้แสดง (ช่อง 10 ใช้ปุ่ม 0)
    public void Setup(int index, Hotbar owner)
    {
        Index = index;
        hotbar = owner;

        if (keyText != null)
        {
            keyText.text = index == 9 ? "0" : (index + 1).ToString();
        }
    }

    // แสดงไอเทมพร้อมจำนวนในกระเป๋า (item = null คือช่องว่าง)
    public void SetItem(ItemData item, int count)
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
                if (count <= 0)
                {
                    color.a = emptyAlpha;
                }
                if (isDragging)
                {
                    color.a = Mathf.Min(color.a, draggingAlpha);
                }
                iconImage.color = color;
            }
        }

        if (countText != null)
        {
            // แสดงตัวเลขเมื่อมีมากกว่า 1 ชิ้น หรือเหลือ 0 (บอกว่าหมดแล้ว)
            bool show = item != null && count != 1;
            countText.text = show ? count.ToString() : "";
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
        if (eventData.button != PointerEventData.InputButton.Left || CurrentItem == null || hotbar == null || IsLocked())
        {
            return;
        }

        isDragging = true;
        lastDropHandled = false;
        DragIconUI.Begin(this, CurrentItem, eventData.position);
        SetIconAlpha(draggingAlpha);
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

        // ไม่ได้วางลงช่อง Hotbar ไหนเลย = ลากออก เอาทางลัดออก
        if (!lastDropHandled && hotbar != null)
        {
            Debug.Log($"Hotbar: Removed {CurrentItem?.itemName} from slot {Index + 1}.");
            hotbar.ClearSlot(Index);
        }

        lastDropHandled = false;

        // วาดช่องนี้ใหม่ (คืนความทึบของไอคอน)
        if (hotbar != null)
        {
            SetItem(hotbar.GetSlot(Index), hotbar.GetCount(Index));
        }
    }

    // ---------- รับของที่ลากมาวาง ----------

    public void OnDrop(PointerEventData eventData)
    {
        if (hotbar == null || eventData.pointerDrag == null || IsLocked())
        {
            return;
        }

        // มาจากช่องอื่นใน Hotbar = สลับ
        HotbarSlotUI fromHotbar = eventData.pointerDrag.GetComponent<HotbarSlotUI>();
        if (fromHotbar != null)
        {
            if (!fromHotbar.isDragging)
            {
                return;
            }

            lastDropHandled = true;
            if (fromHotbar != this)
            {
                hotbar.SwapSlots(fromHotbar.Index, Index);
                hotbar.Select(Index);
            }
            return;
        }

        // มาจากกระเป๋า = ใส่ทางลัด
        InventorySlotUI fromBag = eventData.pointerDrag.GetComponent<InventorySlotUI>();
        if (fromBag != null && fromBag.CurrentItem != null)
        {
            if (!Hotbar.CanAssign(fromBag.CurrentItem))
            {
                NotificationUI.Post(string.Format(cannotAssignFormat, fromBag.CurrentItem.itemName));
                return;
            }

            hotbar.SetSlot(Index, fromBag.CurrentItem);
            hotbar.Select(Index);
            Debug.Log($"Hotbar: Put {fromBag.CurrentItem.itemName} in slot {Index + 1}.");
        }
    }

    // ---------- ภายใน ----------

    private void SetIconAlpha(float alpha)
    {
        if (iconImage == null || !iconImage.enabled)
        {
            return;
        }

        Color color = iconImage.color;
        color.a = Mathf.Min(color.a, alpha);
        iconImage.color = color;
    }

    private static bool IsLocked()
    {
        return ShopManager.Instance != null && ShopManager.Instance.IsServing;
    }

    // ถ้าช่องถูกปิดระหว่างลาก ให้ซ่อนไอคอน
    private void OnDisable()
    {
        if (isDragging)
        {
            isDragging = false;
            DragIconUI.End();
        }
    }
}
