using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// สูตร 1 รายการในหน้าต่างเลือกเมนู (ฝั่งขวา) คลิกเพื่อใส่ลงช่องเมนูที่ว่าง
// MenuSelectionUI สร้างรายการนี้จากแม่แบบ (template)
// [2.4b] เมนูนิยมของวันนี้: มีเครื่องหมายนำหน้าชื่อ และชื่อเป็นสีทอง
public class MenuRecipeItemUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ปุ่มของรายการนี้ (บนตัวเดียวกัน)")]
    [SerializeField] private Button button;

    [Tooltip("ชื่อสูตร")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("ข้อมูลเพิ่ม เช่น ประเทศ และทำได้กี่จาน")]
    [SerializeField] private TMP_Text infoText;

    [Header("Popular")]
    [Tooltip("สีชื่อของเมนูนิยมวันนี้")]
    [SerializeField] private Color popularColor = new Color(1f, 0.82f, 0.3f);

    public RecipeData Recipe { get; private set; }

    // สีชื่อปกติ (จำไว้ตอนเริ่ม)
    private Color normalNameColor = Color.white;
    private bool hasNormalColor;

    // ส่งสัญญาณเมื่อคลิกรายการนี้
    public event Action<RecipeData> OnClicked;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.AddListener(() => OnClicked?.Invoke(Recipe));
        }

        RememberNormalColor();
    }

    private void RememberNormalColor()
    {
        if (!hasNormalColor && nameText != null)
        {
            normalNameColor = nameText.color;
            hasNormalColor = true;
        }
    }

    // available = ทำได้กี่จาน (หักของที่เมนูจองไว้แล้ว), onMenu = อยู่ในเมนูแล้ว
    // popular = เมนูนิยมวันนี้, marker = เครื่องหมายหน้าชื่อเมนูนิยม
    public void Show(RecipeData recipe, int available, bool onMenu, bool popular = false, string marker = "*")
    {
        Recipe = recipe;
        RememberNormalColor();

        if (nameText != null)
        {
            string displayName = recipe != null ? recipe.recipeName : "";
            nameText.text = popular && recipe != null ? $"{marker} {displayName}" : displayName;
            nameText.color = popular ? popularColor : normalNameColor;
        }

        if (infoText != null)
        {
            string popularNote = popular ? " · Popular today!" : "";

            if (recipe == null)
            {
                infoText.text = "";
            }
            else if (onMenu)
            {
                infoText.text = $"{recipe.cuisine} · {recipe.course} · On the menu{popularNote}";
            }
            else if (available > 0)
            {
                infoText.text = $"{recipe.cuisine} · {recipe.course} · Can serve {available}{popularNote}";
            }
            else
            {
                infoText.text = $"{recipe.cuisine} · {recipe.course} · Not enough ingredients{popularNote}";
            }
        }

        if (button != null)
        {
            button.interactable = recipe != null && !onMenu && available > 0;
        }
    }
}
