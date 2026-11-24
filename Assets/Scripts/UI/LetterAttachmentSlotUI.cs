using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [2.5c] ช่องของแนบ 1 ช่องด้านล่างจดหมาย (แบบ Stardew) คลิก = รับของ
// MailUI สร้างช่องนี้จากแม่แบบ (template)
public class LetterAttachmentSlotUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ปุ่มของช่องนี้ (บนตัวเดียวกัน)")]
    [SerializeField] private Button button;

    [Tooltip("ไอคอน (ยังไม่มีภาพ = ซ่อน)")]
    [SerializeField] private Image iconImage;

    [Tooltip("ชื่อของ + จำนวน")]
    [SerializeField] private TMP_Text labelText;

    [Tooltip("กรอบเรืองแสงตอนยังไม่ได้รับ (เว้นว่างได้)")]
    [SerializeField] private GameObject highlight;

    [Header("Text")]
    [Tooltip("ของแนบเป็นสูตร ({0} = ชื่อสูตร)")]
    [SerializeField] private string recipeFormat = "<size=75%>Recipe</size>\n{0}";

    [Tooltip("ของแนบเป็นไอเทม ({0} = ชื่อ, {1} = จำนวน)")]
    [SerializeField] private string itemFormat = "{0}\n<size=80%>x{1}</size>";

    [Tooltip("ต่อท้ายเมื่อรับแล้ว")]
    [SerializeField] private string claimedSuffix = "\n<size=70%>(taken)</size>";

    [Tooltip("สีจางเมื่อรับแล้ว")]
    [SerializeField] private Color claimedTint = new Color(1f, 1f, 1f, 0.35f);

    public int Index { get; private set; }

    // ส่งสัญญาณเมื่อคลิกช่องนี้ (index ของของแนบ)
    public event Action<int> OnClicked;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.AddListener(() => OnClicked?.Invoke(Index));
        }
    }

    public void Show(LetterAttachment attachment, int index, bool claimed)
    {
        Index = index;

        Sprite icon = null;
        string label = "";
        if (attachment != null)
        {
            if (attachment.kind == LetterAttachmentKind.Recipe && attachment.recipe != null)
            {
                icon = attachment.recipe.icon != null ? attachment.recipe.icon :
                       attachment.recipe.dishItem != null ? attachment.recipe.dishItem.icon : null;
                label = string.Format(recipeFormat, attachment.recipe.recipeName);
            }
            else if (attachment.kind == LetterAttachmentKind.Item && attachment.item != null)
            {
                icon = attachment.item.icon;
                label = string.Format(itemFormat, attachment.item.itemName, Mathf.Max(1, attachment.amount));
            }
        }

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.color = claimed ? claimedTint : Color.white;
        }

        if (labelText != null)
        {
            labelText.text = claimed ? label + claimedSuffix : label;
            labelText.alpha = claimed ? 0.5f : 1f;
        }

        if (highlight != null)
        {
            highlight.SetActive(!claimed);
        }

        if (button != null)
        {
            button.interactable = !claimed;
        }
    }
}
