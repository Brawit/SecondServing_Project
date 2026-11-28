using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// [2.5] สูตร 1 รายการในหน้าต่างทำอาหารนอกเวลาร้าน (CookingMenuUI) คลิก = เริ่มทำ
// CookingMenuUI สร้างรายการนี้จากแม่แบบ (template)
// [v2] สูตรที่มีช่องหมวด (any_fish ฯลฯ): มีตัวเลือกวัตถุดิบ "< Salmon x3 >" มุมขวาบน คลิกลูกศร = เปลี่ยนตัวที่จะใส่
//      ตัวเลือกสร้างด้วยโค้ดตอน Awake ถ้ายังไม่ได้ผูก (ไม่ต้องแก้ฉาก/แม่แบบ) / ลำดับ = มีมากสุดก่อน แล้วตาม itemId
public class CookingRecipeItemUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ปุ่มของรายการนี้ (บนตัวเดียวกัน)")]
    [SerializeField] private Button button;

    [Tooltip("ชื่อสูตร")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("ข้อมูลเพิ่ม เช่น ทำได้กี่จาน และมีในกระเป๋ากี่จาน")]
    [SerializeField] private TMP_Text infoText;

    [Header("Ingredient Choice (v2)")]
    [Tooltip("ที่วางตัวเลือกวัตถุดิบ (เว้นว่าง = สร้างให้เองมุมขวาบน)")]
    [SerializeField] private RectTransform choiceRoot;

    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text choiceText;

    [Tooltip("ความกว้างตัวเลือกที่สร้างเอง (px)")]
    [Min(120f)] [SerializeField] private float choiceWidth = 250f;

    [Tooltip("ข้อความตัวเลือก ({0} = ชื่อวัตถุดิบ, {1} = จำนวนในกระเป๋า)")]
    [SerializeField] private string choiceFormat = "{0} x{1}";

    [Tooltip("ข้อความบนปุ่มลูกศร (ฟอนต์ TMP อาจไม่มีตัว ‹ › จึงใช้ < > แทน)")]
    [SerializeField] private string prevLabel = "<";
    [SerializeField] private string nextLabel = ">";

    public RecipeData Recipe { get; private set; }

    // [v2] วัตถุดิบที่เลือกใส่ช่องหมวดอยู่ตอนนี้ (null = สูตรไม่มีช่องหมวด)
    public ItemData Choice { get; private set; }

    // ส่งสัญญาณเมื่อคลิกรายการนี้ (สูตร, วัตถุดิบที่เลือก)
    public event Action<RecipeData, ItemData> OnCookClicked;

    // ส่งสัญญาณเมื่อคลิกรายการนี้ (แบบเดิม ไม่มีวัตถุดิบที่เลือก)
    public event Action<RecipeData> OnClicked;

    // [v2] ส่งสัญญาณเมื่อเปลี่ยนวัตถุดิบด้วยลูกศร (สูตร, ตัวใหม่)
    public event Action<RecipeData, ItemData> OnChoiceChanged;

    private readonly List<ItemData> candidates = new List<ItemData>();
    private Inventory inventory;
    private int cookable;
    private Vector2 nameOffsetMax;
    private bool nameOffsetSaved;

    // [v2] กรอบไฮไลท์ตอนเมาส์วาง (ใส่ด้วยโค้ด)
    private HoverHighlight hover;
    private HoverHighlight Hover => hover != null ? hover : (hover = HoverHighlight.Ensure(gameObject));

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.AddListener(HandleClicked);
        }

        EnsureChoiceSelector();
        if (prevButton != null) prevButton.onClick.AddListener(() => Step(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
        SetChoiceVisible(false);
    }

    private void HandleClicked()
    {
        // [แก้ 28 พ.ย.] ปุ่มที่คลิกแล้วค้างสถานะ "ถูกเลือก" ไว้ในระบบ UI → เปิดหน้าต่างใหม่แล้วปุ่มไม่เปลี่ยนสีตอนเมาส์วาง จึงล้างทิ้ง
        if (button != null && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == button.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        OnClicked?.Invoke(Recipe);
        OnCookClicked?.Invoke(Recipe, Choice);
    }

    // cookable = วัตถุดิบในกระเป๋าทำได้กี่จาน, inBag = จานนี้ที่ทำไว้แล้วในกระเป๋า
    public void Show(RecipeData recipe, int cookable, int inBag)
    {
        Show(recipe, cookable, inBag, null, null);
    }

    // [v2] inv = กระเป๋า (ใช้หาวัตถุดิบในหมวด) / choice = ตัวที่จะเลือกไว้ก่อน (ใช้ไม่ได้ = ตัวแรกของรายการ)
    public void Show(RecipeData recipe, int cookable, int inBag, Inventory inv, ItemData choice)
    {
        Recipe = recipe;
        inventory = inv;
        this.cookable = cookable;

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

        // ตัวเลือกวัตถุดิบ
        candidates.Clear();
        Choice = null;
        bool hasSlot = recipe != null && recipe.HasCategorySlot && inv != null;
        if (hasSlot)
        {
            candidates.AddRange(RecipeRequirements.GetCandidates(recipe, inv));
            Choice = candidates.Contains(choice) ? choice : (candidates.Count > 0 ? candidates[0] : null);
        }
        SetChoiceVisible(hasSlot);
        RefreshChoice();
    }

    // เลื่อนตัวเลือก (วนรอบ)
    private void Step(int delta)
    {
        if (candidates.Count <= 1)
        {
            return;
        }

        int index = Mathf.Max(0, candidates.IndexOf(Choice));
        index = (index + delta + candidates.Count) % candidates.Count;
        Choice = candidates[index];
        RefreshChoice();
        OnChoiceChanged?.Invoke(Recipe, Choice);
    }

    private void RefreshChoice()
    {
        if (choiceText != null)
        {
            choiceText.text = Choice != null
                ? string.Format(choiceFormat, Choice.itemName, inventory != null ? inventory.GetCount(Choice) : 0)
                : (Recipe != null && Recipe.HasCategorySlot ? $"No {RecipeSlot.CategoryLabel(Recipe.CategorySlot.category)}" : "");
        }

        bool canStep = candidates.Count > 1;
        if (prevButton != null) prevButton.interactable = canStep;
        if (nextButton != null) nextButton.interactable = canStep;

        // สูตรที่มีช่องหมวดต้องมีวัตถุดิบที่เลือกและพอทำ (ไม่มีของในหมวดเลย = กดไม่ได้ ไม่ error)
        bool ready = Recipe != null && cookable > 0;
        if (ready && Recipe.HasCategorySlot)
        {
            ready = Choice != null && RecipeRequirements.HasAll(Recipe, inventory, Choice);
        }

        if (button != null)
        {
            button.interactable = ready;
        }

        // [v2] กรอบไฮไลท์: เฉพาะสูตรที่กดทำได้ตอนนี้ (วัตถุดิบไม่พอ = ไม่ขึ้น)
        Hover.Interactable = ready;
    }

    private void SetChoiceVisible(bool visible)
    {
        if (choiceRoot != null)
        {
            choiceRoot.gameObject.SetActive(visible);
        }

        // หลบชื่อสูตรไม่ให้ทับตัวเลือก
        if (nameText != null && choiceRoot != null)
        {
            RectTransform rt = nameText.rectTransform;
            if (!nameOffsetSaved)
            {
                nameOffsetMax = rt.offsetMax;
                nameOffsetSaved = true;
            }
            rt.offsetMax = visible ? new Vector2(nameOffsetMax.x - choiceRoot.sizeDelta.x - 8f, nameOffsetMax.y) : nameOffsetMax;
        }
    }

    // ---------- สร้างตัวเลือกด้วยโค้ด ----------

    private void EnsureChoiceSelector()
    {
        if (choiceRoot != null && choiceText != null)
        {
            return;
        }

        RectTransform self = transform as RectTransform;
        if (self == null)
        {
            return;
        }

        float height = 30f;
        GameObject rootGo = new GameObject("ChoiceSelector", typeof(RectTransform));
        choiceRoot = (RectTransform)rootGo.transform;
        choiceRoot.SetParent(transform, false);
        choiceRoot.anchorMin = new Vector2(1f, 1f);
        choiceRoot.anchorMax = new Vector2(1f, 1f);
        choiceRoot.pivot = new Vector2(1f, 1f);
        choiceRoot.anchoredPosition = new Vector2(-12f, -4f);
        choiceRoot.sizeDelta = new Vector2(choiceWidth, height);

        prevButton = CreateArrow("PrevButton", prevLabel, 0f, height);
        nextButton = CreateArrow("NextButton", nextLabel, 1f, height);

        GameObject labelGo = new GameObject("ChoiceText", typeof(RectTransform));
        RectTransform labelRt = (RectTransform)labelGo.transform;
        labelRt.SetParent(choiceRoot, false);
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.offsetMin = new Vector2(height + 4f, 0f);
        labelRt.offsetMax = new Vector2(-height - 4f, 0f);

        TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(label);
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        choiceText = label;
    }

    // ปุ่มลูกศร anchorX 0 = ซ้าย / 1 = ขวา
    private Button CreateArrow(string objectName, string text, float anchorX, float size)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(choiceRoot, false);
        rt.anchorMin = new Vector2(anchorX, 0.5f);
        rt.anchorMax = new Vector2(anchorX, 0.5f);
        rt.pivot = new Vector2(anchorX, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);

        Image image = go.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.18f);

        Button arrow = go.AddComponent<Button>();
        arrow.targetGraphic = image;

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        RectTransform textRt = (RectTransform)textGo.transform;
        textRt.SetParent(rt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI label = textGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(label);
        label.richText = false; // ให้ "<" แสดงเป็นตัวอักษร ไม่ถูกอ่านเป็นแท็ก
        label.text = text;
        label.fontSize = 20f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return arrow;
    }

    private void ApplyFont(TMP_Text text)
    {
        if (nameText != null && nameText.font != null) text.font = nameText.font;
        else if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
    }

    // [2.5b] ดาวความยาก (ข้อความตั้งใน CookingSettings)
    private static string StarsOf(RecipeData recipe)
    {
        CookingSettings settings = CookingSession.Instance != null ? CookingSession.Instance.Settings : null;
        string stars = settings != null ? settings.Stars(recipe.Difficulty) : $"Lv{recipe.Difficulty}";
        return $"<noparse>{stars}</noparse>";
    }
}
