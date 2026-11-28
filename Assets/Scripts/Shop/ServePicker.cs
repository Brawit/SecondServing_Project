using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// [2.5] แถบเลือกจานตอนเสิร์ฟ (แสดงในข้อความเหนือหัวลูกค้า)
// - เปิดเมื่อกด E ที่ลูกค้าตอนมือว่าง และในกระเป๋ามีจานที่ลูกค้าสั่งให้เลือกมากกว่า 1 ตัวเลือก
// - [v2] ตัวเลือก = แบบ × คุณภาพ เช่น "Salmon · Perfect x1" (จานที่ไม่มีแบบ = "Perfect x1" เหมือนเดิม)
//   เรียงคุณภาพดีสุดก่อน แล้วตามราคาที่ลูกค้าจะจ่าย (แพงก่อน) / ตัวเลือกเดียว = เสิร์ฟทันที (Customer ตัดสิน)
// - [v2] แบบ carousel เหมือนแถบของเตา: แสดงทีละ Visible Options ตัวรอบตัวที่เลือก + ตำแหน่ง "4/12" / คลิกลูกศร < > = เลื่อนไป 1 ตัว
// - เลือกตัวดีที่สุดไว้ให้ก่อน / scroll เมาส์ = เลื่อน / คลิกซ้ายที่ตัวเลือก = เลือกตัวนั้น
// - กด E หรือคลิกซ้ายซ้ำที่ตัวที่เลือกอยู่แล้ว = เสิร์ฟ
// - กด Esc / เดินออกห่างจากลูกค้า / ลูกค้าไม่ได้รออาหารแล้ว = ปิด
// ไม่ต้องวางในฉาก: Customer สร้างให้เองตอนใช้ครั้งแรก (วางเองก็ได้ ถ้าอยากปรับค่าใน Inspector)
public class ServePicker : MonoBehaviour
{
    public static ServePicker Instance { get; private set; }

    // [v2] ตัวเลือก 1 ตัว = จาน 1 แบบ × 1 คุณภาพ
    public struct Option
    {
        public ItemData dish;
        public DishQuality quality;

        public Option(ItemData dish, DishQuality quality)
        {
            this.dish = dish;
            this.quality = quality;
        }

        public bool SameAs(Option other)
        {
            return dish == other.dish && quality == other.quality;
        }
    }

    [Header("Close")]
    [Tooltip("เดินห่างจากลูกค้าเกินระยะนี้ (หน่วย) = ปิดแถบเลือก")]
    [Min(0.5f)] [SerializeField] private float closeDistance = 1.8f;

    [Header("Input")]
    [Tooltip("ปกติ scroll ขึ้น = เลื่อนไปทางซ้าย (ตัวที่ดีกว่า) ติ๊กเพื่อกลับทิศ")]
    [SerializeField] private bool invertScroll = false;

    [Header("Text")]
    [Tooltip("ข้อความบอกวิธีใช้ใต้ตัวเลือก (เว้นว่าง = ไม่แสดง)")]
    [SerializeField] private string hintText = "Scroll / click to pick · Click again or E to serve · Esc to cancel";

    [Tooltip("[v2] ข้อความตัวเลือกของจานแบบ {0} = ชื่อวัตถุดิบ, {1} = คุณภาพ, {2} = จำนวน")]
    [SerializeField] private string variantOptionFormat = "{0} · {1} x{2}";

    [Tooltip("[v2] ข้อความตัวเลือกของจานที่ไม่มีแบบ {0} = คุณภาพ, {1} = จำนวน")]
    [SerializeField] private string plainOptionFormat = "{0} x{1}";

    [Tooltip("[v2] ชื่อแทนวัตถุดิบ ของจานแม่ในสูตรที่มีช่องหมวด (เช่น จานจากเซฟเก่า)")]
    [SerializeField] private string baseDishLabel = "Basic";

    [Tooltip("[v2] จำนวนตัวเลือกที่แสดงพร้อมกัน (ที่เหลือเลื่อนดูด้วย scroll หรือคลิกลูกศร)")]
    [Min(1)] [SerializeField] private int visibleOptions = 3;

    [Tooltip("[v2] ตัวบอกตำแหน่งเมื่อมีตัวเลือกมากกว่าที่แสดง ({0} = ลำดับที่เลือก, {1} = ทั้งหมด) เว้นว่าง = ไม่แสดง")]
    [SerializeField] private string positionFormat = "{0}/{1}";

    // ลูกค้าที่เปิดแถบเลือกอยู่ (null = ปิด)
    public Customer Target { get; private set; }
    public bool IsOpen => Target != null;

    private readonly List<Option> options = new List<Option>();
    private int selectedIndex;
    private Transform player;
    private Inventory inventory;

    // หาตัวที่มีอยู่ หรือสร้างใหม่
    public static ServePicker GetOrCreate()
    {
        if (Instance == null)
        {
            ServePicker existing = FindFirstObjectByType<ServePicker>();
            if (existing != null)
            {
                Instance = existing;
            }
            else
            {
                new GameObject("ServePicker").AddComponent<ServePicker>();
            }
        }
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ---------- [v2] ตัวเลือก ----------

    // ใส่ตัวเลือก แบบ × คุณภาพ ของสูตรนี้ที่มีในกระเป๋าลง result (ดีสุดก่อน แล้วราคาแพงก่อน) คืนค่าจำนวน
    public static int BuildOptions(RecipeData recipe, Inventory bag, List<Option> result)
    {
        result.Clear();
        if (recipe == null || bag == null)
        {
            return 0;
        }

        List<DishQuality> qualities = new List<DishQuality>();
        foreach (ItemData dish in DishVariantRegistry.DishItemsIn(recipe, bag))
        {
            bag.GetAvailableQualities(dish, qualities);
            foreach (DishQuality q in qualities)
            {
                result.Add(new Option(dish, q));
            }
        }

        // คุณภาพดีกว่าก่อน → ราคาที่ลูกค้าจ่ายแพงกว่าก่อน → มีมากกว่าก่อน → ตามรหัส (ให้ลำดับคงที่)
        result.Sort((a, b) =>
        {
            int c = ((int)b.quality).CompareTo((int)a.quality);
            if (c != 0) return c;
            c = DishPricing.SalePrice(b.dish, b.quality).CompareTo(DishPricing.SalePrice(a.dish, a.quality));
            if (c != 0) return c;
            c = bag.GetCount(b.dish, b.quality).CompareTo(bag.GetCount(a.dish, a.quality));
            if (c != 0) return c;
            return string.CompareOrdinal(a.dish.itemId, b.dish.itemId);
        });

        return result.Count;
    }

    // ---------- เปิด / ปิด ----------

    // เปิดแถบเลือกให้ลูกค้าคนนี้ คืนค่า false ถ้ามีตัวเลือกไม่ถึง 2 ตัว (ไม่ต้องเลือก)
    public bool Open(Customer customer, Transform playerTransform, Inventory bag)
    {
        if (customer == null || customer.Order == null || bag == null)
        {
            return false;
        }

        if (BuildOptions(customer.Order, bag, options) < 2)
        {
            options.Clear();
            return false;
        }

        // ปิดของลูกค้าคนเดิมก่อน (ให้ข้อความเหนือหัวกลับเป็นปกติ)
        Customer previous = Target;
        Target = customer;
        player = playerTransform;
        inventory = bag;
        selectedIndex = 0; // ดีที่สุดก่อน
        if (previous != null && previous != customer) previous.RefreshBubble();

        customer.RefreshBubble();
        return true;
    }

    public void Close()
    {
        Customer previous = Target;
        Target = null;
        options.Clear();
        if (previous != null) previous.RefreshBubble();
    }

    public bool IsOpenFor(Customer customer)
    {
        return customer != null && Target == customer;
    }

    private bool HasSelection => IsOpen && selectedIndex >= 0 && selectedIndex < options.Count;

    // คุณภาพที่เลือกอยู่
    public DishQuality? SelectedQuality => HasSelection ? options[selectedIndex].quality : (DishQuality?)null;

    // [v2] จานแบบที่เลือกอยู่
    public ItemData SelectedDish => HasSelection ? options[selectedIndex].dish : null;

    // เสิร์ฟตัวที่เลือก (กด E หรือคลิกซ้ำที่ตัวที่เลือก)
    public void Confirm()
    {
        if (!IsOpen)
        {
            return;
        }

        Customer customer = Target;
        ItemData dish = SelectedDish;
        DishQuality? quality = SelectedQuality;
        Close();

        if (customer != null)
        {
            customer.TryServe(dish, quality);
        }
    }

    // ---------- อัปเดต ----------

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        // ลูกค้าไม่ได้รออาหารแล้ว / ผู้เล่นเดินห่าง = ปิด
        if (Target == null || Target.State != CustomerState.WaitingForFood || Target.Order == null || player == null ||
            Vector2.Distance(player.position, Target.transform.position) > closeDistance)
        {
            Close();
            return;
        }

        // จานอาจถูกเสิร์ฟให้คนอื่นไประหว่างนี้ ตรวจตัวเลือกใหม่ทุกเฟรม (จำตัวที่เลือกไว้)
        bool hadSelection = HasSelection;
        Option previous = hadSelection ? options[selectedIndex] : default;
        if (inventory == null || BuildOptions(Target.Order, inventory, options) == 0)
        {
            Close();
            return;
        }

        selectedIndex = 0;
        if (hadSelection)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].SameAs(previous))
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        // Esc = ยกเลิก
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            // scroll ขึ้น = ไปทางซ้าย (ดีกว่า)
            int direction = scroll > 0f ? -1 : 1;
            if (invertScroll) direction = -direction;
            selectedIndex = Mathf.Clamp(selectedIndex + direction, 0, options.Count - 1);
            Target.RefreshBubble();
        }

        // คลิกซ้ายที่ตัวเลือกในข้อความเหนือหัว: ตัวใหม่ = เลือกตัวนั้น / ตัวที่เลือกอยู่แล้ว = เสิร์ฟ / ลูกศร = เลื่อน 1 ตัว
        if (mouse.leftButton.wasPressedThisFrame)
        {
            int clicked = FindClickedOption(mouse.position.ReadValue());
            if (clicked >= 0 && clicked < options.Count)
            {
                if (clicked == selectedIndex)
                {
                    Confirm();
                    return;
                }

                selectedIndex = clicked;
                Target.RefreshBubble();
            }
        }
    }

    // หาว่าคลิกโดนตัวเลือกไหน (ตัวเลือกห่อด้วย <link="q0">, <link="q1"> ... / ลูกศร = "prev" / "next") คืนค่า -1 ถ้าไม่โดน
    private int FindClickedOption(Vector2 screenPosition)
    {
        TMP_Text bubble = Target != null ? Target.BubbleText : null;
        Camera cam = Camera.main;
        if (bubble == null || cam == null)
        {
            return -1;
        }

        // ข้อความ TextMeshPro แบบ 3D ในฉาก ต้องส่งกล้องที่มองฉาก / แบบ UI บน Canvas Overlay ไม่ต้องส่งกล้อง
        Canvas canvas = bubble.canvas;
        Camera useCam = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(bubble, screenPosition, useCam);
        if (linkIndex < 0 || linkIndex >= bubble.textInfo.linkCount)
        {
            return -1;
        }

        string id = bubble.textInfo.linkInfo[linkIndex].GetLinkID();
        if (id == "prev") return selectedIndex > 0 ? selectedIndex - 1 : -1;
        if (id == "next") return selectedIndex < options.Count - 1 ? selectedIndex + 1 : -1;
        if (id != null && id.Length > 1 && id[0] == 'q' && int.TryParse(id.Substring(1), out int option))
        {
            return option;
        }
        return -1;
    }

    // ---------- ข้อความ ----------

    // ข้อความของตัวเลือก 1 ตัว เช่น "Salmon · Perfect x1" / จานที่ไม่มีแบบ = "Perfect x1"
    private string OptionText(Option option)
    {
        int count = inventory != null ? inventory.GetCount(option.dish, option.quality) : 0;
        string quality = DishQualityUtil.ColoredLabel(option.quality);

        string variantName = "";
        if (option.dish != null && option.dish.variantIngredient != null)
        {
            variantName = option.dish.variantIngredient.itemName;
        }
        else if (Target != null && Target.Order != null && Target.Order.HasCategorySlot)
        {
            variantName = baseDishLabel;
        }

        return string.IsNullOrEmpty(variantName)
            ? string.Format(plainOptionFormat, quality, count)
            : string.Format(variantOptionFormat, variantName, quality, count);
    }

    // บรรทัดตัวเลือก เช่น "< [Salmon · Perfect x1]  Tilapia · OK x2 >" (ตัวที่เลือกอยู่ในวงเล็บและตัวหนา)
    public string GetLine(Customer customer)
    {
        if (!IsOpenFor(customer) || customer.Order == null || inventory == null || options.Count == 0)
        {
            return "";
        }

        // carousel: แสดงเฉพาะช่วงรอบตัวที่เลือก (ตัวที่เลือกอยู่กลาง ถ้าทำได้) ลูกศรบอกว่ายังมีอีกด้านไหน
        int visible = Mathf.Clamp(visibleOptions, 1, Mathf.Max(1, options.Count));
        int first = Mathf.Clamp(selectedIndex - visible / 2, 0, Mathf.Max(0, options.Count - visible));
        int last = Mathf.Min(options.Count, first + visible);
        bool moreLeft = first > 0;
        bool moreRight = last < options.Count;

        // ลูกศรคลิกได้ (เลื่อนไป 1 ตัว) / ฝั่งที่ไม่มีแล้ว = จาง
        StringBuilder sb = new StringBuilder();
        sb.Append(moreLeft
            ? "<link=\"prev\"><b><noparse><</noparse></b></link> "
            : "<alpha=#40><noparse><</noparse><alpha=#FF> ");
        for (int i = first; i < last; i++)
        {
            string text = OptionText(options[i]);
            if (i == selectedIndex) text = $"<b>[{text}]</b>";
            text = $"<link=\"q{i}\">{text}</link>";
            if (i > first) sb.Append("  ");
            sb.Append(text);
        }
        sb.Append(moreRight
            ? " <link=\"next\"><b><noparse>></noparse></b></link>"
            : " <alpha=#40><noparse>></noparse><alpha=#FF>");

        // ตำแหน่ง เช่น "4/12" (เฉพาะตอนมีตัวเลือกมากกว่าที่แสดง)
        if (options.Count > visible && !string.IsNullOrEmpty(positionFormat))
        {
            sb.Append($"  <size=70%>{string.Format(positionFormat, selectedIndex + 1, options.Count)}</size>");
        }

        if (!string.IsNullOrEmpty(hintText))
        {
            sb.Append($"\n<size=70%>{hintText}</size>");
        }

        return sb.ToString();
    }
}
