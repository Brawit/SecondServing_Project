using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// [v2] แถว 1 แถวในแผงรายละเอียดของกระเป๋า = จาน 1 แบบ × 1 คุณภาพ เช่น "Salmon · Perfect x1"
// - ปุ่ม Eat = กินจานแบบนี้ คุณภาพนี้ (InventoryUI เป็นคนเรียก PlayerStats.TryEat)
// - ลากแถวไปวางที่ Hotbar ได้ (HotbarSlotUI.OnDrop อ่าน Item / Quality จากแถวนี้)
// - ลากแถวที่ลากไม่ได้ = ส่งการลากต่อให้ Scroll View (เลื่อนรายการได้ตามเดิม)
// - เอาเมาส์วางบนแถว = ส่ง OnHoverEnter / OnHoverExit (InventoryUI ใช้แสดงกล่องค่าฟื้นพลัง)
// สร้างด้วยโค้ดทั้งหมด (Create) ไม่ต้องมี prefab
public class DishVariantRowUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    // จานที่แถวนี้แสดง (จานแม่หรือจานแบบ)
    public ItemData Item { get; private set; }

    // คุณภาพของแถวนี้
    public DishQuality Quality { get; private set; }

    // ส่งสัญญาณเมื่อกดปุ่ม Eat
    public event Action<DishVariantRowUI> OnEatClicked;

    // ส่งสัญญาณเมื่อเมาส์เข้า / ออกจากแถวนี้
    public event Action<DishVariantRowUI> OnHoverEnter;
    public event Action<DishVariantRowUI> OnHoverExit;

    private bool isHovered;

    private Image background;
    private Image iconImage;
    private TMP_Text labelText;
    private Button eatButton;

    // กรอบไฮไลท์ตอนเมาส์วาง (ใส่ด้วยโค้ด)
    private HoverHighlight hover;

    private static readonly Color PlaceholderColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.06f);
    private static readonly Color ButtonColor = new Color(0.32f, 0.55f, 0.35f, 1f);

    // การลากครั้งนี้เป็นการลากจาน (true) หรือส่งต่อให้ Scroll View (false)
    private bool isDraggingItem;
    private GameObject forwardedDragTarget;

    // แถวนี้ลากไปใส่ Hotbar ได้หรือไม่
    public bool CanDragToHotbar => Item != null && Hotbar.CanAssign(Item);

    // ---------- สร้าง ----------

    // สร้างแถวใหม่ใต้ parent (font null = ฟอนต์ตั้งต้นของ TMP)
    public static DishVariantRowUI Create(Transform parent, TMP_FontAsset font, float height, float fontSize, string eatLabel)
    {
        GameObject go = new GameObject("DishVariantRow", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;

        // พื้นหลังของแถว (รับเมาส์ ใช้เริ่มลาก)
        Image bg = go.AddComponent<Image>();
        bg.color = RowColor;
        bg.raycastTarget = true;

        LayoutElement rowLayout = go.AddComponent<LayoutElement>();
        rowLayout.minHeight = height;
        rowLayout.preferredHeight = height;

        HorizontalLayoutGroup h = go.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(6, 6, 4, 4);
        h.spacing = 8f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        float iconSize = Mathf.Max(16f, height - 10f);

        // ไอคอนจาน
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(go.transform, false);
        iconGo.layer = go.layer;
        Image icon = iconGo.AddComponent<Image>();
        icon.raycastTarget = false;
        LayoutElement iconLayout = iconGo.AddComponent<LayoutElement>();
        iconLayout.minWidth = iconSize;
        iconLayout.preferredWidth = iconSize;
        iconLayout.minHeight = iconSize;
        iconLayout.preferredHeight = iconSize;

        // ข้อความ "Salmon · Perfect x1"
        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        labelGo.layer = go.layer;
        TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.richText = true;
        label.raycastTarget = false;
        LayoutElement labelLayout = labelGo.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;
        labelLayout.minHeight = iconSize;

        // ปุ่ม Eat
        GameObject buttonGo = new GameObject("EatButton", typeof(RectTransform));
        buttonGo.transform.SetParent(go.transform, false);
        buttonGo.layer = go.layer;
        Image buttonImage = buttonGo.AddComponent<Image>();
        buttonImage.color = ButtonColor;
        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        LayoutElement buttonLayout = buttonGo.AddComponent<LayoutElement>();
        buttonLayout.minWidth = 72f;
        buttonLayout.preferredWidth = 72f;
        buttonLayout.minHeight = iconSize;
        buttonLayout.preferredHeight = iconSize;

        GameObject buttonTextGo = new GameObject("Text", typeof(RectTransform));
        buttonTextGo.transform.SetParent(buttonGo.transform, false);
        buttonTextGo.layer = go.layer;
        TextMeshProUGUI buttonText = buttonTextGo.AddComponent<TextMeshProUGUI>();
        if (font != null) buttonText.font = font;
        buttonText.fontSize = fontSize;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.raycastTarget = false;
        buttonText.text = eatLabel;
        RectTransform btrt = buttonText.rectTransform;
        btrt.anchorMin = Vector2.zero;
        btrt.anchorMax = Vector2.one;
        btrt.offsetMin = Vector2.zero;
        btrt.offsetMax = Vector2.zero;

        DishVariantRowUI row = go.AddComponent<DishVariantRowUI>();
        row.background = bg;
        row.iconImage = icon;
        row.labelText = label;
        row.eatButton = button;
        button.onClick.AddListener(row.HandleEatClicked);
        row.hover = HoverHighlight.Ensure(go);
        return row;
    }

    // ---------- ตั้งค่าการแสดงผล ----------

    public void Set(ItemData item, DishQuality quality, string text)
    {
        Item = item;
        Quality = quality;

        if (iconImage != null)
        {
            Sprite icon = item != null ? item.icon : null;
            iconImage.sprite = icon;
            iconImage.preserveAspect = icon != null;
            iconImage.color = icon != null ? Color.white : PlaceholderColor;
        }

        if (labelText != null)
        {
            labelText.text = text;
        }

        // กินไม่ได้ (ค่าฟื้นเป็น 0 ทั้งหมด) = ปุ่มกดไม่ได้
        if (eatButton != null)
        {
            eatButton.interactable = item != null && item.IsEdible;
        }

        // กรอบไฮไลท์: แถวที่มีจาน = ขึ้น (ลากลง Hotbar / กดปุ่ม Eat ได้)
        if (hover == null) hover = HoverHighlight.Ensure(gameObject);
        hover.Interactable = item != null;
    }

    private void HandleEatClicked()
    {
        if (Item != null)
        {
            OnEatClicked?.Invoke(this);
        }
    }

    // ---------- เมาส์วาง ----------

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

    // ---------- ลากไป Hotbar ----------

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDraggingItem = eventData.button == PointerEventData.InputButton.Left && CanDragToHotbar;

        if (isDraggingItem)
        {
            DragIconUI.Begin(this, Item, eventData.position);
            return;
        }

        // ลากไม่ได้: ส่งต่อให้ตัวที่รับการลากซึ่งอยู่เหนือขึ้นไป (Scroll View ของรายการ)
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
            // การวางจริงจัดการที่ HotbarSlotUI.OnDrop (ถูกเรียกก่อนฟังก์ชันนี้)
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

    // ถ้าแถวถูกซ่อนระหว่างลาก (เช่น กินจนหมด / ปิดกระเป๋า) ให้ซ่อนไอคอนที่ลอยอยู่
    private void OnDisable()
    {
        if (isDraggingItem)
        {
            isDraggingItem = false;
            DragIconUI.End();
        }

        // แถวถูกซ่อนตอนเมาส์ยังวางอยู่ (เช่น กินจนหมด / เปลี่ยนจาน) = ถือว่าเมาส์ออกแล้ว
        if (isHovered)
        {
            isHovered = false;
            OnHoverExit?.Invoke(this);
        }
    }
}
