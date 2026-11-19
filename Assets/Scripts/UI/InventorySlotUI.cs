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
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            OnClicked?.Invoke(this);
        }
    }
}
