using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// [2.6b] แผงขายของแบบคลิกเดียว ใช้ร่วมกันระหว่างกล่องขายข้ามคืนและร้านของชำ
// - แท็บ Ingredients (ของที่ไม่ใช่จาน) / Dishes (แยกแถวตามคุณภาพ) / แสดงเฉพาะของที่ขายได้ (sellPrice > 0)
// - กล่องและร้านของชำใช้ปุ่มเหมือนกัน (ผู้ใช้ขอ): คลิกซ้าย = ทั้งกอง / คลิกขวา = 1 / Shift + คลิกขวา = 10
// - ตัวจริงที่ขาย/ใส่กล่อง ส่งผ่าน callback (เจ้าของแผงตัดสินว่าจะทำอะไร)
public class SellPanelUI : MonoBehaviour
{
    private enum Tab { Ingredients, Dishes }

    private struct Stack
    {
        public ItemData item;
        public DishQuality quality;
        public int count;
    }

    [Header("References")]
    [Tooltip("กระเป๋าของผู้เล่น (เว้นว่าง = หาเอง)")]
    [SerializeField] private Inventory inventory;

    [Header("Tabs")]
    [SerializeField] private Button ingredientsTabButton;
    [SerializeField] private Button dishesTabButton;
    [SerializeField] private Color activeTabColor = new Color(0.95f, 0.8f, 0.4f);
    [SerializeField] private Color inactiveTabColor = new Color(0.35f, 0.35f, 0.4f);

    [Header("List")]
    [SerializeField] private RectTransform listContent;
    [Tooltip("แถวต้นแบบ (ปิดไว้ในฉาก)")]
    [SerializeField] private SellItemSlotUI rowTemplate;
    [SerializeField] private TMP_Text emptyText;
    [Tooltip("บรรทัดบอกวิธีคลิก")]
    [SerializeField] private TMP_Text hintText;

    [Header("Amounts")]
    [Tooltip("ค้าง Shift = ขายครั้งละเท่านี้")]
    [Min(1)] [SerializeField] private int shiftAmount = 10;

    [Header("Text")]
    [SerializeField] private string emptyIngredientsMessage = "Nothing to sell here.";
    [SerializeField] private string emptyDishesMessage = "No dishes to sell. Cook some first!";
    [Tooltip("{0} = คำกริยา (ship / sell)")]
    [SerializeField] private string hintFormat = "Click = {0} all   ·   Right-click = {0} 1   ·   Shift + right-click = {0} 10";

    private readonly List<SellItemSlotUI> rows = new List<SellItemSlotUI>();
    private readonly List<Stack> buffer = new List<Stack>();

    private Func<ItemData, int, DishQuality, bool> onSell;
    private Tab tab = Tab.Ingredients;
    private bool subscribed;

    private void Awake()
    {
        if (inventory == null)
        {
            PlayerInteractor player = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
            if (player != null) inventory = player.GetComponent<Inventory>();
        }
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);

        if (ingredientsTabButton != null) ingredientsTabButton.onClick.AddListener(() => SetTab(Tab.Ingredients));
        if (dishesTabButton != null) dishesTabButton.onClick.AddListener(() => SetTab(Tab.Dishes));
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    // ตั้งค่าแผง: sellCallback คืน true ถ้าสำเร็จ / verb = คำในบรรทัดบอกวิธี (ship / sell)
    public void Configure(Func<ItemData, int, DishQuality, bool> sellCallback, string verb)
    {
        onSell = sellCallback;
        if (hintText != null) hintText.text = string.Format(hintFormat, string.IsNullOrEmpty(verb) ? "sell" : verb);
        Subscribe();
        Rebuild();
    }

    private void Subscribe()
    {
        if (subscribed || inventory == null || !isActiveAndEnabled) return;
        inventory.OnChanged += Rebuild;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (inventory != null) inventory.OnChanged -= Rebuild;
        subscribed = false;
    }

    private void SetTab(Tab newTab)
    {
        if (tab == newTab) return;
        tab = newTab;
        Rebuild();
    }

    // ---------- รายการ ----------

    public void Rebuild()
    {
        buffer.Clear();
        if (inventory != null)
        {
            foreach (KeyValuePair<ItemData, int> pair in inventory.Items)
            {
                ItemData item = pair.Key;
                if (pair.Value <= 0 || !SalesLedger.CanSell(item)) continue;
                bool isDish = Inventory.TracksQuality(item);
                if ((tab == Tab.Dishes) != isDish) continue;

                if (!isDish)
                {
                    buffer.Add(new Stack { item = item, quality = DishQuality.Good, count = pair.Value });
                    continue;
                }

                // จาน: แยกแถวตามคุณภาพ ดีสุดก่อน
                foreach (DishQuality q in DishQualityUtil.BestFirst)
                {
                    int n = inventory.GetCount(item, q);
                    if (n > 0) buffer.Add(new Stack { item = item, quality = q, count = n });
                }
            }
        }

        buffer.Sort((a, b) =>
        {
            int byName = string.Compare(a.item.itemName, b.item.itemName, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : b.quality.CompareTo(a.quality);
        });

        while (rows.Count < buffer.Count && rowTemplate != null && listContent != null)
        {
            rows.Add(Instantiate(rowTemplate, listContent));
        }

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < buffer.Count;
            rows[i].gameObject.SetActive(used);
            if (used) rows[i].Bind(buffer[i].item, buffer[i].quality, buffer[i].count, HandleRowClicked);
        }

        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(buffer.Count == 0);
            emptyText.text = tab == Tab.Dishes ? emptyDishesMessage : emptyIngredientsMessage;
        }

        SetButtonColor(ingredientsTabButton, tab == Tab.Ingredients ? activeTabColor : inactiveTabColor);
        SetButtonColor(dishesTabButton, tab == Tab.Dishes ? activeTabColor : inactiveTabColor);
    }

    // ---------- คลิก ----------

    private void HandleRowClicked(SellItemSlotUI row, PointerEventData.InputButton mouseButton)
    {
        if (row == null || row.Item == null || onSell == null) return;

        Keyboard keyboard = Keyboard.current;
        bool shift = keyboard != null && keyboard.shiftKey.isPressed;

        // คลิกซ้าย = ทั้งกอง / คลิกขวา = 1 (ค้าง Shift = 10)
        int amount = mouseButton == PointerEventData.InputButton.Left
            ? row.Count
            : (shift ? Mathf.Max(1, shiftAmount) : 1);

        amount = Mathf.Min(amount, row.Count);
        if (amount <= 0) return;

        // callback เปลี่ยนกระเป๋า → OnChanged → Rebuild เอง
        onSell(row.Item, amount, row.Quality);
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button != null && button.targetGraphic != null) button.targetGraphic.color = color;
    }
}
