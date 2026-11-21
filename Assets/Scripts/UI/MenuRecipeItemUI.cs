using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// สูตร 1 รายการในหน้าต่างเลือกเมนู (ฝั่งขวา) คลิกเพื่อใส่ลงช่องเมนูที่ว่าง
// MenuSelectionUI สร้างรายการนี้จากแม่แบบ (template)
public class MenuRecipeItemUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ปุ่มของรายการนี้ (บนตัวเดียวกัน)")]
    [SerializeField] private Button button;

    [Tooltip("ชื่อสูตร")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("ข้อมูลเพิ่ม เช่น ประเทศ และทำได้กี่จาน")]
    [SerializeField] private TMP_Text infoText;

    public RecipeData Recipe { get; private set; }

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
    }

    // available = ทำได้กี่จาน (หักของที่เมนูจองไว้แล้ว), onMenu = อยู่ในเมนูแล้ว
    public void Show(RecipeData recipe, int available, bool onMenu)
    {
        Recipe = recipe;

        if (nameText != null)
        {
            nameText.text = recipe != null ? recipe.recipeName : "";
        }

        if (infoText != null)
        {
            if (recipe == null)
            {
                infoText.text = "";
            }
            else if (onMenu)
            {
                infoText.text = $"{recipe.cuisine} · {recipe.course} · On the menu";
            }
            else if (available > 0)
            {
                infoText.text = $"{recipe.cuisine} · {recipe.course} · Can serve {available}";
            }
            else
            {
                infoText.text = $"{recipe.cuisine} · {recipe.course} · Not enough ingredients";
            }
        }

        if (button != null)
        {
            button.interactable = recipe != null && !onMenu && available > 0;
        }
    }
}
