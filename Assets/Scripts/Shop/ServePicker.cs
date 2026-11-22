using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// [2.5] แถบเลือกคุณภาพจานตอนเสิร์ฟ (แสดงในข้อความเหนือหัวลูกค้า)
// - เปิดเมื่อกด E ที่ลูกค้าตอนมือว่าง และในกระเป๋ามีจานที่ลูกค้าสั่งมากกว่า 1 คุณภาพ
// - เลือกคุณภาพดีที่สุดไว้ให้ก่อน / scroll เมาส์ = เลื่อน / คลิกซ้ายที่ตัวเลือก = เลือกตัวนั้น
// - กด E หรือคลิกซ้ายซ้ำที่ตัวที่เลือกอยู่แล้ว = เสิร์ฟ
// - กด Esc / เดินออกห่างจากลูกค้า / ลูกค้าไม่ได้รออาหารแล้ว = ปิด
// ไม่ต้องวางในฉาก: Customer สร้างให้เองตอนใช้ครั้งแรก (วางเองก็ได้ ถ้าอยากปรับค่าใน Inspector)
public class ServePicker : MonoBehaviour
{
    public static ServePicker Instance { get; private set; }

    [Header("Close")]
    [Tooltip("เดินห่างจากลูกค้าเกินระยะนี้ (หน่วย) = ปิดแถบเลือก")]
    [Min(0.5f)] [SerializeField] private float closeDistance = 1.8f;

    [Header("Input")]
    [Tooltip("ปกติ scroll ขึ้น = เลื่อนไปทางซ้าย (คุณภาพดีกว่า) ติ๊กเพื่อกลับทิศ")]
    [SerializeField] private bool invertScroll = false;

    [Header("Text")]
    [Tooltip("ข้อความบอกวิธีใช้ใต้ตัวเลือก (เว้นว่าง = ไม่แสดง)")]
    [SerializeField] private string hintText = "Scroll / click to pick · Click again or E to serve · Esc to cancel";

    // ลูกค้าที่เปิดแถบเลือกอยู่ (null = ปิด)
    public Customer Target { get; private set; }
    public bool IsOpen => Target != null;

    private readonly List<DishQuality> options = new List<DishQuality>();
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

    // ---------- เปิด / ปิด ----------

    // เปิดแถบเลือกให้ลูกค้าคนนี้ คืนค่า false ถ้ามีจานไม่ถึง 2 คุณภาพ (ไม่ต้องเลือก)
    public bool Open(Customer customer, Transform playerTransform, Inventory bag)
    {
        if (customer == null || customer.Order == null || customer.Order.dishItem == null || bag == null)
        {
            return false;
        }

        if (bag.GetAvailableQualities(customer.Order.dishItem, options) < 2)
        {
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

    // คุณภาพที่เลือกอยู่
    public DishQuality? SelectedQuality =>
        IsOpen && selectedIndex >= 0 && selectedIndex < options.Count ? options[selectedIndex] : (DishQuality?)null;

    // เสิร์ฟด้วยคุณภาพที่เลือก (กด E หรือคลิกซ้ำที่ตัวที่เลือก)
    public void Confirm()
    {
        if (!IsOpen)
        {
            return;
        }

        Customer customer = Target;
        DishQuality? quality = SelectedQuality;
        Close();

        if (customer != null)
        {
            customer.TryServe(quality);
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

        // จานอาจถูกเสิร์ฟให้คนอื่นไประหว่างนี้ ตรวจตัวเลือกใหม่ทุกเฟรม (จำคุณภาพที่เลือกไว้)
        DishQuality? previous = SelectedQuality;
        if (inventory == null || inventory.GetAvailableQualities(Target.Order.dishItem, options) == 0)
        {
            Close();
            return;
        }

        selectedIndex = previous.HasValue ? Mathf.Max(0, options.IndexOf(previous.Value)) : 0;

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

        // คลิกซ้ายที่ตัวเลือกในข้อความเหนือหัว: ตัวใหม่ = เลือกตัวนั้น / ตัวที่เลือกอยู่แล้ว = เสิร์ฟ
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

    // หาว่าคลิกโดนตัวเลือกไหน (ตัวเลือกแต่ละตัวห่อด้วย <link="q0">, <link="q1"> ...) คืนค่า -1 ถ้าไม่โดน
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
        if (id != null && id.Length > 1 && id[0] == 'q' && int.TryParse(id.Substring(1), out int option))
        {
            return option;
        }
        return -1;
    }

    // ---------- ข้อความ ----------

    // บรรทัดตัวเลือก เช่น "< [Perfect x2]  Good x5  OK x1 >" (ตัวที่เลือกอยู่ในวงเล็บและตัวหนา)
    public string GetLine(Customer customer)
    {
        if (!IsOpenFor(customer) || customer.Order == null || inventory == null)
        {
            return "";
        }

        StringBuilder sb = new StringBuilder("< ");
        for (int i = 0; i < options.Count; i++)
        {
            DishQuality q = options[i];
            string text = $"{DishQualityUtil.ColoredLabel(q)} x{inventory.GetCount(customer.Order.dishItem, q)}";
            if (i == selectedIndex) text = $"<b>[{text}]</b>";
            text = $"<link=\"q{i}\">{text}</link>";
            if (i > 0) sb.Append("  ");
            sb.Append(text);
        }
        sb.Append(" >");

        if (!string.IsNullOrEmpty(hintText))
        {
            sb.Append($"\n<size=70%>{hintText}</size>");
        }

        return sb.ToString();
    }
}
