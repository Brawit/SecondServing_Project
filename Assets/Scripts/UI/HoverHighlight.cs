using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// [v2] กรอบไฮไลท์ตอนเอาเมาส์วาง บอกผู้เล่นว่า "ช่อง/แถวนี้กดได้"
// - ใส่บนช่อง/แถวใดก็ได้ใน UI (ผู้ใช้เรียก HoverHighlight.Ensure(gameObject) ตอนวาด ไม่ต้องแก้ prefab หรือฉาก)
// - สร้างกรอบลูกชื่อ "HoverFrame" เอง (เส้น 4 ด้าน) ตอนเมาส์วางครั้งแรก / ไม่ถูก Layout Group จัด / วาดทับบนสุด
// - ผู้เรียกตั้ง Interactable = false สำหรับช่องที่ทำอะไรไม่ได้ (ช่องว่าง / ยังไม่ค้นพบ) → ไม่ขึ้นกรอบ
// - แยกจากกรอบ "เลือกอยู่" (SelectionFrame) ที่มีอยู่แล้ว
// - [แก้ 28 พ.ย.] ช่องที่ถูกซ่อนตอนเมาส์วางอยู่ (เช่น คลิกสูตรแล้วหน้าต่างทำอาหารปิด) พอเปิดใหม่ Unity อาจไม่ส่ง
//   OnPointerEnter ซ้ำ → ตอนเปิดใช้ (OnEnable) รอ 1 เฟรมแล้วตรวจเองว่าเมาส์อยู่บนช่องนี้ไหม
[DisallowMultipleComponent]
public class HoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public const string FrameObjectName = "HoverFrame";

    private const string TopName = "Top";
    private const string BottomName = "Bottom";
    private const string LeftName = "Left";
    private const string RightName = "Right";

    [Header("Look")]
    [Tooltip("สีกรอบ (ค่าเริ่มต้น ขาวนวล โปร่งเล็กน้อย)")]
    [SerializeField] private Color frameColor = new Color(1f, 0.96f, 0.85f, 0.9f);

    [Tooltip("ความหนาเส้นกรอบ (px)")]
    [Min(1f)] [SerializeField] private float thickness = 3f;

    [Tooltip("ขยายกรอบออกนอกขอบช่อง (px) / 0 = ชิดขอบด้านใน / ค่าลบ = หดเข้าด้านใน")]
    [SerializeField] private float outset = 0f;

    // กรอบที่สร้างแล้ว (เก็บไว้ให้ตอน Instantiate จากแม่แบบใช้กรอบเดิม ไม่สร้างซ้ำ)
    [SerializeField, HideInInspector] private RectTransform frameRoot;

    private bool interactable = true;

    // ผลยิงเมาส์ล่าสุด ใช้ร่วมกันทุกช่องในเฟรมเดียวกัน (เปิดกระเป๋าทีเดียวร้อยช่อง = ยิงครั้งเดียว)
    private static int probeFrame = -1;
    private static GameObject probeHit;
    private static readonly List<RaycastResult> probeResults = new List<RaycastResult>();

    // ช่องนี้กดได้ไหม (false = ไม่ขึ้นกรอบแม้เมาส์วางอยู่)
    public bool Interactable
    {
        get => interactable;
        set
        {
            if (interactable == value)
            {
                return;
            }

            interactable = value;
            Refresh();
        }
    }

    // เมาส์วางอยู่บนช่องนี้ไหม
    public bool IsHovered { get; private set; }

    // ---------- ใช้งาน ----------

    // หา HoverHighlight บนวัตถุนี้ ไม่มี = ใส่ให้
    public static HoverHighlight Ensure(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        HoverHighlight highlight = target.GetComponent<HoverHighlight>();
        if (highlight == null)
        {
            highlight = target.AddComponent<HoverHighlight>();
        }
        return highlight;
    }

    // ตั้งหน้าตากรอบด้วยโค้ด (ไม่จำเป็น ใช้ค่าใน Inspector ได้)
    public void SetLook(Color color, float lineThickness, float frameOutset)
    {
        frameColor = color;
        thickness = Mathf.Max(1f, lineThickness);
        outset = frameOutset;
        if (frameRoot != null)
        {
            ApplyLook();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        IsHovered = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsHovered = false;
        Refresh();
    }

    private void OnEnable()
    {
        if (!IsHovered)
        {
            StartCoroutine(ProbePointerNextFrame());
        }
    }

    // รอ 1 เฟรมให้ Layout จัดตำแหน่งเสร็จ แล้วดูว่าเมาส์วางอยู่บนช่องนี้ (หรือลูกของช่องนี้) ไหม
    private IEnumerator ProbePointerNextFrame()
    {
        yield return null;

        if (IsHovered)
        {
            yield break;
        }

        GameObject hit = PointerTopHit();
        if (hit != null && hit.transform.IsChildOf(transform))
        {
            IsHovered = true;
            Refresh();
        }
    }

    // UI ชิ้นบนสุดที่อยู่ใต้เมาส์ตอนนี้ (null = ไม่มี)
    private static GameObject PointerTopHit()
    {
        if (probeFrame == Time.frameCount)
        {
            return probeHit;
        }

        probeFrame = Time.frameCount;
        probeHit = null;

        EventSystem eventSystem = EventSystem.current;
        Mouse mouse = Mouse.current;
        if (eventSystem == null || mouse == null)
        {
            return null;
        }

        PointerEventData data = new PointerEventData(eventSystem) { position = mouse.position.ReadValue() };
        probeResults.Clear();
        eventSystem.RaycastAll(data, probeResults);
        if (probeResults.Count > 0)
        {
            probeHit = probeResults[0].gameObject;
        }
        probeResults.Clear();
        return probeHit;
    }

    private void OnDisable()
    {
        // ช่องถูกซ่อนตอนเมาส์ยังวางอยู่ (ปิดกระเป๋า / เปลี่ยนแท็บ / ของหมด) = ถือว่าเมาส์ออกแล้ว
        IsHovered = false;
        SetFrameVisible(false);
    }

    private void Refresh()
    {
        SetFrameVisible(IsHovered && interactable && isActiveAndEnabled);
    }

    // ---------- กรอบ ----------

    private void SetFrameVisible(bool visible)
    {
        if (!visible)
        {
            if (frameRoot == null)
            {
                frameRoot = FindExistingFrame();
            }
            if (frameRoot != null && frameRoot.gameObject.activeSelf)
            {
                frameRoot.gameObject.SetActive(false);
            }
            return;
        }

        EnsureFrame();
        if (frameRoot == null)
        {
            return;
        }

        ApplyLook();

        // วาดทับเนื้อหาอื่นของช่อง (ย้ายเฉพาะเมื่อยังไม่อยู่ท้ายสุด ไม่ให้ Layout ถูกสั่งจัดใหม่ทุกครั้ง)
        if (frameRoot.GetSiblingIndex() != transform.childCount - 1)
        {
            frameRoot.SetAsLastSibling();
        }

        frameRoot.gameObject.SetActive(true);
    }

    private RectTransform FindExistingFrame()
    {
        Transform existing = transform.Find(FrameObjectName);
        return existing as RectTransform;
    }

    private void EnsureFrame()
    {
        if (frameRoot == null)
        {
            frameRoot = FindExistingFrame();
        }

        if (frameRoot != null)
        {
            return;
        }

        if (!(transform is RectTransform))
        {
            Debug.LogWarning($"HoverHighlight: {name} is not a UI object (no RectTransform).");
            return;
        }

        GameObject rootGo = new GameObject(FrameObjectName, typeof(RectTransform));
        rootGo.layer = gameObject.layer;
        frameRoot = (RectTransform)rootGo.transform;
        frameRoot.SetParent(transform, false);
        frameRoot.anchorMin = Vector2.zero;
        frameRoot.anchorMax = Vector2.one;
        frameRoot.pivot = new Vector2(0.5f, 0.5f);

        // ไม่ให้ Layout Group ของช่อง (เช่น แถวจาน) จัดกรอบนี้เป็นลูกตัวหนึ่ง
        LayoutElement layout = rootGo.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;

        CreateEdge(TopName);
        CreateEdge(BottomName);
        CreateEdge(LeftName);
        CreateEdge(RightName);

        rootGo.SetActive(false);
    }

    private void CreateEdge(string edgeName)
    {
        GameObject go = new GameObject(edgeName, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(frameRoot, false);

        Image image = go.AddComponent<Image>();
        image.raycastTarget = false; // ไม่บังการคลิก/ลาก
    }

    private void ApplyLook()
    {
        frameRoot.offsetMin = new Vector2(-outset, -outset);
        frameRoot.offsetMax = new Vector2(outset, outset);

        float t = thickness;

        // บน/ล่าง เต็มความกว้าง / ซ้าย/ขวา เว้นมุมไว้ ไม่ให้มุมทับกันจนเข้มกว่าเส้น
        SetEdge(TopName, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -t), new Vector2(0f, 0f));
        SetEdge(BottomName, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, t));
        SetEdge(LeftName, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, t), new Vector2(t, -t));
        SetEdge(RightName, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-t, t), new Vector2(0f, -t));
    }

    private void SetEdge(string edgeName, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Transform edge = frameRoot.Find(edgeName);
        if (edge == null)
        {
            return;
        }

        RectTransform rt = (RectTransform)edge;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        Image image = edge.GetComponent<Image>();
        if (image != null)
        {
            image.color = frameColor;
        }
    }
}
