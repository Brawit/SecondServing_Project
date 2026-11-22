using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// สถานะของช่องในกระเป๋าแบบ Collectibles
public enum SlotState
{
    Owned,           // มีอยู่ในกระเป๋า (หรือสูตรที่รู้แล้ว)
    DiscoveredEmpty, // เคยเจอแล้ว แต่ตอนนี้มี 0 ชิ้น
    Undiscovered     // ยังไม่เคยเจอ แสดงเป็นเงาดำ
}

// ช่อง 1 ช่องในหน้าต่างกระเป๋า: แสดงไอคอน ชื่อ และจำนวน (มุมขวาบน) คลิกเพื่อดูรายละเอียด
// ลากไอเทมไปวางที่ Hotbar ได้ (เฉพาะของที่ใส่ Hotbar ได้: อาวุธ เครื่องมือ ของกินได้)
// ลากของสวมใส่ไปวางที่ช่องสวมใส่ได้ (ต้องมีในกระเป๋าอย่างน้อย 1 ชิ้น)
// ถ้าลากช่องที่ลากไม่ได้ จะส่งการลากต่อให้ Scroll View (เลื่อนรายการได้ตามเดิม)
// [2.5] เอาเมาส์วางบนช่อง = ส่ง OnHoverEnter / OnHoverExit (InventoryUI ใช้แสดงกล่องคุณภาพจาน)
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [Tooltip("รูปไอคอนของไอเทม")]
    [SerializeField] private Image iconImage;

    [Tooltip("ข้อความชื่อไอเทม")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("ข้อความจำนวน (วางไว้มุมขวาบนของช่อง)")]
    [SerializeField] private TMP_Text countText;

    [Tooltip("กรอบที่แสดงตอนช่องนี้ถูกเลือก (เว้นว่างได้)")]
    [SerializeField] private GameObject selectionFrame;

    [Header("Colors")]
    [Tooltip("สีสี่เหลี่ยมแทนไอคอน เมื่อยังไม่มีรูป")]
    [SerializeField] private Color placeholderColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    [Tooltip("ความโปร่งของไอคอน เมื่อเคยเจอแต่ตอนนี้มี 0 ชิ้น")]
    [Range(0f, 1f)]
    [SerializeField] private float emptyAlpha = 0.35f;

    [Tooltip("สีเงาของไอเทมที่ยังไม่เคยเจอ")]
    [SerializeField] private Color undiscoveredColor = new Color(0.08f, 0.08f, 0.1f, 1f);

    [Header("Text")]
    [Tooltip("ชื่อที่แสดงแทนไอเทมที่ยังไม่เคยเจอ")]
    [SerializeField] private string unknownName = "???";

    // สิ่งที่ช่องนี้แสดงอยู่ (มีอย่างใดอย่างหนึ่ง หรือเป็น null ทั้งคู่ถ้าช่องว่าง)
    public ItemData CurrentItem { get; private set; }
    public RecipeData CurrentRecipe { get; private set; }
    public SlotState CurrentState { get; private set; }

    // ส่งสัญญาณเมื่อผู้เล่นคลิกช่องนี้
    public event Action<InventorySlotUI> OnClicked;

    // [2.5] ส่งสัญญาณเมื่อเมาส์เข้า / ออกจากช่องนี้
    public event Action<InventorySlotUI> OnHoverEnter;
    public event Action<InventorySlotUI> OnHoverExit;

    private bool isHovered;

    // ---------- ตั้งค่าการแสดงผล ----------

    // แบบเดิม: แสดงไอเทมพร้อมจำนวน (ถือว่ามีอยู่ในกระเป๋า)
    public void SetItem(ItemData item, int count)
    {
        SetItemEntry(item, count, count > 0 ? SlotState.Owned : SlotState.DiscoveredEmpty);
    }

    // แสดงไอเทมตามสถานะ
    public void SetItemEntry(ItemData item, int count, SlotState state)
    {
        if (item == null)
        {
            SetEmpty();
            return;
        }

        CurrentItem = item;
        CurrentRecipe = null;
        Apply(item.icon, item.itemName, count, state);
    }

    // แสดงสูตรอาหาร count = จำนวนจานที่ทำได้ตอนนี้ (0 = ไม่แสดงตัวเลข)
    public void SetRecipeEntry(RecipeData recipe, int count, SlotState state)
    {
        if (recipe == null)
        {
            SetEmpty();
            return;
        }

        CurrentItem = null;
        CurrentRecipe = recipe;

        // ใช้ไอคอนของสูตร ถ้าไม่มีใช้ไอคอนของจานอาหาร
        Sprite icon = recipe.icon;
        if (icon == null && recipe.dishItem != null)
        {
            icon = recipe.dishItem.icon;
        }

        Apply(icon, recipe.recipeName, count, state);
    }

    // ตั้งให้ช่องนี้เป็นช่องว่าง
    public void SetEmpty()
    {
        CurrentItem = null;
        CurrentRecipe = null;
        CurrentState = SlotState.Undiscovered;

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

        SetSelected(false);
    }

    // แสดง/ซ่อนกรอบเลือก
    public void SetSelected(bool selected)
    {
        if (selectionFrame != null)
        {
            selectionFrame.SetActive(selected);
        }
    }

    private void Apply(Sprite icon, string label, int count, SlotState state)
    {
        CurrentState = state;

        if (iconImage != null)
        {
            iconImage.enabled = true;
            iconImage.sprite = icon;
            iconImage.preserveAspect = icon != null;

            // สีพื้นฐาน: รูปจริง = ขาว, ยังไม่มีรูป = สีเทาแทน
            Color baseColor = icon != null ? Color.white : placeholderColor;

            switch (state)
            {
                case SlotState.Owned:
                    iconImage.color = baseColor;
                    break;
                case SlotState.DiscoveredEmpty:
                    baseColor.a = emptyAlpha;
                    iconImage.color = baseColor;
                    break;
                case SlotState.Undiscovered:
                    // ถ้ามีรูป จะกลายเป็นเงาดำตามรูปทรงของรูป
                    iconImage.color = undiscoveredColor;
                    break;
            }
        }

        if (nameText != null)
        {
            nameText.text = state == SlotState.Undiscovered ? unknownName : label;
        }

        if (countText != null)
        {
            countText.text = state != SlotState.Undiscovered && count > 0 ? count.ToString() : "";
        }
    }

    // ---------- คลิก ----------

    // ---------- [2.5] เมาส์วาง ----------

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        OnHoverEnter?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isHovered)
        {
            return;
        }

        isHovered = false;
        OnHoverExit?.Invoke(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            OnClicked?.Invoke(this);
        }
    }

    // ---------- ลากไป Hotbar ----------

    // การลากครั้งนี้เป็นการลากไอเทม (true) หรือส่งต่อให้ Scroll View (false)
    private bool isDraggingItem;
    private GameObject forwardedDragTarget;

    // ช่องนี้ลากไปใส่ Hotbar ได้หรือไม่
    public bool CanDragToHotbar =>
        CurrentItem != null &&
        CurrentState != SlotState.Undiscovered &&
        Hotbar.CanAssign(CurrentItem);

    // ช่องนี้ลากไปสวมได้หรือไม่ (ต้องมีของจริงในกระเป๋า เพราะสวม = ย้ายออกจากกระเป๋า)
    public bool CanDragToEquipment =>
        CurrentItem != null &&
        CurrentState == SlotState.Owned &&
        CurrentItem.IsEquippable;

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDraggingItem = eventData.button == PointerEventData.InputButton.Left && (CanDragToHotbar || CanDragToEquipment);

        if (isDraggingItem)
        {
            DragIconUI.Begin(this, CurrentItem, eventData.position);
            return;
        }

        // ลากไม่ได้: ส่งต่อให้ตัวที่รับการลากซึ่งอยู่เหนือขึ้นไป (เช่น Scroll View)
        forwardedDragTarget = transform.parent != null
            ? ExecuteEvents.GetEventHandler<IBeginDragHandler>(transform.parent.gameObject)
            : null;

        if (forwardedDragTarget != null)
        {
            ExecuteEvents.Execute(forwardedDragTarget, eventData, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(forwardedDragTarget, eventData, ExecuteEvents.beginDragHandler);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isDraggingItem)
        {
            DragIconUI.Move(eventData.position);
            return;
        }

        if (forwardedDragTarget != null)
        {
            ExecuteEvents.Execute(forwardedDragTarget, eventData, ExecuteEvents.dragHandler);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isDraggingItem)
        {
            // การวางจริงจัดการที่ HotbarSlotUI.OnDrop / EquipmentSlotUI.OnDrop (ถูกเรียกก่อนฟังก์ชันนี้)
            isDraggingItem = false;
            DragIconUI.End();
            return;
        }

        if (forwardedDragTarget != null)
        {
            ExecuteEvents.Execute(forwardedDragTarget, eventData, ExecuteEvents.endDragHandler);
            forwardedDragTarget = null;
        }
    }

    // ถ้าช่องถูกปิด/ล้างระหว่างลาก ให้ซ่อนไอคอน
    private void OnDisable()
    {
        if (isDraggingItem)
        {
            isDraggingItem = false;
            DragIconUI.End();
        }

        // ช่องถูกซ่อนตอนเมาส์ยังวางอยู่ (เช่น ปิดกระเป๋า / เปลี่ยนแท็บ) = ถือว่าเมาส์ออกแล้ว
        if (isHovered)
        {
            isHovered = false;
            OnHoverExit?.Invoke(this);
        }
    }
}
