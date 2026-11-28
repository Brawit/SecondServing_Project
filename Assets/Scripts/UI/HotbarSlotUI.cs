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
// [v2] วางแถวจาน (แบบ × คุณภาพ) จากแผงรายละเอียดของกระเป๋า = ช่องจำคุณภาพนั้น / ของหมด = ไอคอนสีเทา (ช่องยังอยู่)
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
    [SerializeField] private float emptyAlpha = 0.6f;

    [Tooltip("[v2] สีเทาของไอคอน เมื่อของในกระเป๋าเหลือ 0 (ช่องไม่ถูกล้าง ทำ/เก็บของมาใหม่ = กลับมาเป็นสีปกติ)")]
    [Range(0f, 1f)]
    [SerializeField] private float emptyGray = 0.35f;

    [Tooltip("[v2] ระบายสีตัวเลขจำนวนตามคุณภาพ เมื่อช่องจำคุณภาพจานไว้")]
    [SerializeField] private bool tintCountByQuality = true;

    [Tooltip("ความโปร่งของไอคอนในช่องต้นทาง ระหว่างกำลังลาก")]
    [Range(0f, 1f)]
    [SerializeField] private float draggingAlpha = 0.3f;

    [Header("Messages")]
    [Tooltip("ข้อความตอนลากของที่ใส่ Hotbar ไม่ได้มาวาง ({0} = ชื่อ)")]
    [SerializeField] private string cannotAssignFormat = "{0} can't go in the hotbar.";

    [Tooltip("[v2] ข้อความตอนลากช่องรวมของจานหลายแบบมาวาง (ต้องลากแถวในแผงรายละเอียดแทน)")]
    [SerializeField] private string pickRowMessage = "Click the dish, then drag a row from the details panel.";

    // ช่องนี้คือช่องที่เท่าไร (0–9) ตั้งโดย HotbarUI
    public int Index { get; private set; }

    public ItemData CurrentItem { get; private set; }

    // [v2] คุณภาพที่ช่องนี้จำไว้ (null = ไม่ระบุ)
    public DishQuality? CurrentQuality { get; private set; }

    // ส่งสัญญาณเมื่อคลิกช่องนี้
    public event Action<HotbarSlotUI> OnClicked;

    private Hotbar hotbar;
    private bool isDragging;

    // [v2] กรอบไฮไลท์ตอนเมาส์วาง (ใส่ด้วยโค้ดตอนวาดครั้งแรก)
    private HoverHighlight hover;
    private HoverHighlight Hover => hover != null ? hover : (hover = HoverHighlight.Ensure(gameObject));

    // [v2] สีเดิมของตัวเลขจำนวน (จำไว้ตอนวาดครั้งแรก ใช้คืนค่าเมื่อช่องไม่ได้จำคุณภาพ)
    private Color baseCountColor = Color.white;
    private bool countColorCached;

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
        SetItem(item, count, null);
    }

    // [v2] แสดงไอเทมพร้อมจำนวน + คุณภาพที่ช่องจำไว้ (null = ไม่ระบุ)
    public void SetItem(ItemData item, int count, DishQuality? quality)
    {
        CurrentItem = item;
        CurrentQuality = item != null ? quality : null;

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
                    // [v2] หมดแล้ว = สีเทา (ช่องยังอยู่)
                    color = new Color(color.r * emptyGray, color.g * emptyGray, color.b * emptyGray, emptyAlpha);
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

            // [v2] สีตัวเลขบอกคุณภาพที่ช่องจำไว้
            if (!countColorCached)
            {
                baseCountColor = countText.color;
                countColorCached = true;
            }
            countText.color = tintCountByQuality && CurrentQuality.HasValue ? DishQualityUtil.GetColor(CurrentQuality.Value) : baseCountColor;
        }

        // [v2] กรอบไฮไลท์: ช่องที่มีทางลัด = ขึ้น (รวมช่องสีเทาที่ของหมด เพราะยังเลือก/ลากได้) / ช่องว่าง = ไม่ขึ้น
        Hover.Interactable = item != null;
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
            SetItem(hotbar.GetSlot(Index), hotbar.GetCount(Index), hotbar.GetQuality(Index));
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
            // [v2] ช่องรวมของจานหลายแบบ (Sushi ทุกแบบ) ใส่ Hotbar ไม่ได้ ต้องเลือกแบบ × คุณภาพ จากแถว
            if (!fromBag.AllowHotbarDrag)
            {
                NotificationUI.Post(pickRowMessage);
                return;
            }

            if (!Hotbar.CanAssign(fromBag.CurrentItem))
            {
                NotificationUI.Post(string.Format(cannotAssignFormat, fromBag.CurrentItem.itemName));
                return;
            }

            hotbar.SetSlot(Index, fromBag.CurrentItem);
            hotbar.Select(Index);
            Debug.Log($"Hotbar: Put {fromBag.CurrentItem.itemName} in slot {Index + 1}.");
            return;
        }

        // [v2] มาจากแถวจานในแผงรายละเอียดของกระเป๋า (แบบ × คุณภาพ)
        DishVariantRowUI fromRow = eventData.pointerDrag.GetComponent<DishVariantRowUI>();
        if (fromRow != null && fromRow.Item != null)
        {
            if (!Hotbar.CanAssign(fromRow.Item))
            {
                NotificationUI.Post(string.Format(cannotAssignFormat, fromRow.Item.itemName));
                return;
            }

            hotbar.SetSlot(Index, fromRow.Item, fromRow.Quality);
            hotbar.Select(Index);
            Debug.Log($"Hotbar: Put {fromRow.Item.itemName} ({DishQualityUtil.Label(fromRow.Quality)}) in slot {Index + 1}.");
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
