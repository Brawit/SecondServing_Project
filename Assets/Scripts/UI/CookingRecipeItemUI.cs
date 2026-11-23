using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [2.5] สูตร 1 รายการในหน้าต่างทำอาหารนอกเวลาร้าน (CookingMenuUI) คลิก = เริ่มทำ
// CookingMenuUI สร้างรายการนี้จากแม่แบบ (template)
public class CookingRecipeItemUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ปุ่มของรายการนี้ (บนตัวเดียวกัน)")]
    [SerializeField] private Button button;

    [Tooltip("ชื่อสูตร")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("ข้อมูลเพิ่ม เช่น ทำได้กี่จาน และมีในกระเป๋ากี่จาน")]
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

    // cookable = วัตถุดิบในกระเป๋าทำได้กี่จาน, inBag = จานนี้ที่ทำไว้แล้วในกระเป๋า
    public void Show(RecipeData recipe, int cookable, int inBag)
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
            else
            {
                string bagNote = inBag > 0 ? $" · In bag {inBag}" : "";
                infoText.text = $"{StarsOf(recipe)}  {recipe.cuisine} · {recipe.course} · Can cook {cookable}{bagNote}";
            }
        }

        if (button != null)
        {
            button.interactable = recipe != null && cookable > 0;
        }
    }

    // [2.5b] ดาวความยาก (ข้อความตั้งใน CookingSettings)
    private static string StarsOf(RecipeData recipe)
    {
        CookingSettings settings = CookingSession.Instance != null ? CookingSession.Instance.Settings : null;
        string stars = settings != null ? settings.Stars(recipe.Difficulty) : $"Lv{recipe.Difficulty}";
        return $"<noparse>{stars}</noparse>";
    }
}
