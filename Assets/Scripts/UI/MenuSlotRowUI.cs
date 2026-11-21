using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// แถวช่องเมนู 1 ช่องในหน้าต่างเลือกเมนู: ชื่อเมนู, ที่มาของจาน, ปุ่ม − / + , จำนวน และปุ่มเอาออก
// MenuSelectionUI สร้างแถวนี้จากแม่แบบ (template) ตามจำนวนช่องเมนู
public class MenuSlotRowUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ชื่อเมนู (ช่องว่างจะแสดงข้อความแนะนำ)")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("ข้อมูลเพิ่ม เช่น ทำไว้แล้วกี่จาน ทำได้สูงสุดกี่จาน")]
    [SerializeField] private TMP_Text infoText;

    [Tooltip("ตัวเลขจำนวนจานที่ตั้งไว้")]
    [SerializeField] private TMP_Text countText;

    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;

    [Tooltip("ปุ่มเอาเมนูออกจากช่อง")]
    [SerializeField] private Button removeButton;

    [Header("Text")]
    [Tooltip("ข้อความในช่องว่าง ({0} = เลขช่อง)")]
    [SerializeField] private string emptyFormat = "Slot {0} — pick a recipe on the right";

    // ช่องนี้คือช่องที่เท่าไร (0 = ช่องแรก)
    public int Index { get; private set; }

    // ส่งสัญญาณเมื่อกดปุ่ม (ส่งเลขช่อง)
    public event Action<int> OnMinus;
    public event Action<int> OnPlus;
    public event Action<int> OnRemove;

    private void Awake()
    {
        if (minusButton != null) minusButton.onClick.AddListener(() => OnMinus?.Invoke(Index));
        if (plusButton != null) plusButton.onClick.AddListener(() => OnPlus?.Invoke(Index));
        if (removeButton != null) removeButton.onClick.AddListener(() => OnRemove?.Invoke(Index));
    }

    public void SetIndex(int index)
    {
        Index = index;
    }

    // แสดงข้อมูลช่อง: entry = ข้อมูลช่อง, max = ตั้งได้สูงสุด, dishesInBag = จานที่ทำไว้แล้ว
    public void Show(DailyMenu.MenuEntry entry, int max, int dishesInBag)
    {
        bool empty = entry == null || entry.IsEmpty;

        if (nameText != null)
        {
            nameText.text = empty ? string.Format(emptyFormat, Index + 1) : entry.Recipe.recipeName;
            Color c = nameText.color;
            c.a = empty ? 0.5f : 1f;
            nameText.color = c;
        }

        if (infoText != null)
        {
            if (empty)
            {
                infoText.text = "";
            }
            else
            {
                int fromDishes = Mathf.Min(entry.Planned, dishesInBag);
                int toCook = entry.Planned - fromDishes;
                infoText.text = $"{entry.Recipe.cuisine} · ready {fromDishes} + cook {toCook}  (max {max})";
            }
        }

        if (countText != null)
        {
            countText.text = empty ? "" : entry.Planned.ToString();
        }

        if (minusButton != null)
        {
            minusButton.gameObject.SetActive(!empty);
            minusButton.interactable = !empty && entry.Planned > 0;
        }

        if (plusButton != null)
        {
            plusButton.gameObject.SetActive(!empty);
            plusButton.interactable = !empty && entry.Planned < max;
        }

        if (removeButton != null)
        {
            removeButton.gameObject.SetActive(!empty);
        }
    }
}
