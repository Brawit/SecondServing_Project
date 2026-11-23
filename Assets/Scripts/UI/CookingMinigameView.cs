using TMPro;
using UnityEngine;
using UnityEngine.UI;

// หน้าตาของมินิเกมทำอาหาร (แผงเดียวใช้ร่วมทุกมินิเกม) มินิเกมสั่งให้แสดง/ซ่อนชิ้นส่วนที่ต้องใช้
// - แถบ (Bar): Timing ใช้โซน + โซน Perfect + เข็ม / Mash ใช้แถบเติม
// - ข้อความปุ่ม (Keys): Sequence
// - [2.5b] แถบความคืบหน้าแยก (Progress) ใต้แถบหลัก: Stir (แถบหลักใช้แสดงกระทะกับอาหาร)
// - ภาพ (Visual): ภาพของขั้นตอน ถ้าไม่มีภาพจะซ่อนไว้ (placeholder = แถบสี่เหลี่ยม)
// ตำแหน่งในแถบใช้ Anchor X (0–1) ไม่ต้องคำนวณพิกเซล
// ซ่อนด้วย CanvasGroup (สคริปต์ยังทำงานอยู่) ไม่รับคลิก
// สร้างและผูกอัตโนมัติได้จาก Tools > Second Serving > Build Cooking System
[RequireComponent(typeof(CanvasGroup))]
public class CookingMinigameView : MonoBehaviour
{
    [Header("Texts")]
    [Tooltip("หัวข้อ: ชื่อจาน + ขั้นที่เท่าไร")]
    [SerializeField] private TMP_Text headerText;

    [Tooltip("ชื่อท่า เช่น Pound! (ตอนจบขั้นแสดงผล Perfect / Good / OK)")]
    [SerializeField] private TMP_Text labelText;

    [Tooltip("ข้อความวิธีเล่นใต้แถบ")]
    [SerializeField] private TMP_Text hintText;

    [Tooltip("ลำดับปุ่มของ Sequence")]
    [SerializeField] private TMP_Text keysText;

    [Header("Visual")]
    [Tooltip("ภาพของขั้นตอน (ซ่อนเมื่อไม่มีภาพ)")]
    [SerializeField] private Image visualImage;

    [Header("Bar")]
    [Tooltip("พื้นแถบ (กระพริบสีตามผล)")]
    [SerializeField] private Image barBackground;

    [SerializeField] private RectTransform zone;
    [SerializeField] private RectTransform perfectZone;
    [SerializeField] private RectTransform needle;
    [SerializeField] private RectTransform fill;

    [Tooltip("แถบเวลาที่เหลือ (บางๆ ใต้แถบหลัก)")]
    [SerializeField] private RectTransform timerFill;

    [Tooltip("[2.5b] แถบความคืบหน้าแยก (ทั้งแถบ ซ่อนเมื่อไม่ใช้) เว้นว่าง = ใช้แถบเติมในแถบหลักแทน")]
    [SerializeField] private GameObject progressRoot;

    [Tooltip("[2.5b] ส่วนเติมของแถบความคืบหน้าแยก")]
    [SerializeField] private RectTransform progressFill;

    [Header("Colors")]
    [SerializeField] private Color barColor = new Color(0.15f, 0.15f, 0.18f, 0.95f);
    [SerializeField] private Color needleColor = Color.white;
    [SerializeField] private Color currentKeyColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color correctKeyColor = new Color(0.5f, 0.88f, 0.45f);
    [SerializeField] private Color wrongKeyColor = new Color(1f, 0.4f, 0.35f);

    [Header("Needle")]
    [Tooltip("ความกว้างเข็ม (พิกเซล)")]
    [Min(1f)] [SerializeField] private float needleWidth = 10f;

    [Tooltip("เข็มยื่นเลยขอบบน/ล่างของแถบกี่พิกเซล (มองเห็นง่ายขึ้น)")]
    [Min(0f)] [SerializeField] private float needleOvershoot = 8f;

    [Tooltip("เข็มเปลี่ยนสีตามผลแต่ละครั้งที่กด นานกี่วินาที")]
    [Min(0f)] [SerializeField] private float needlePulseDuration = 0.25f;

    private CanvasGroup canvasGroup;
    private Image needleImage;
    private float needlePulseTimer;

    public Color CurrentKeyColor => currentKeyColor;
    public Color CorrectKeyColor => correctKeyColor;
    public Color WrongKeyColor => wrongKeyColor;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (needle != null) needleImage = needle.GetComponent<Image>();

        if (barBackground == null) Debug.LogWarning("CookingMinigameView: Bar Background is not assigned.", this);
        if (zone == null || needle == null || fill == null) Debug.LogWarning("CookingMinigameView: Zone / Needle / Fill is not assigned.", this);

        Hide();
    }

    private void Update()
    {
        if (needlePulseTimer > 0f)
        {
            needlePulseTimer -= Time.deltaTime;
            if (needlePulseTimer <= 0f && needleImage != null)
            {
                needleImage.color = needleColor;
            }
        }
    }

    // ---------- แสดง / ซ่อน ----------

    public void Show()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
    }

    public void Hide()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
    }

    // ล้างทุกชิ้นก่อนเริ่มขั้นใหม่ (มินิเกมค่อยเปิดชิ้นที่ใช้)
    public void ResetParts()
    {
        SetActive(zone, false);
        SetActive(perfectZone, false);
        SetActive(needle, false);
        SetActive(fill, false);
        if (progressRoot != null) progressRoot.SetActive(false);
        if (keysText != null) keysText.gameObject.SetActive(false);
        SetBarVisible(true);
        ResetBarColor();
        SetTimer(1f);
        needlePulseTimer = 0f;
        if (needleImage != null) needleImage.color = needleColor;
        if (labelText != null) labelText.color = Color.white;
    }

    // ---------- ข้อความ ----------

    public void SetHeader(string text)
    {
        if (headerText != null) headerText.text = text;
    }

    public void SetLabel(string text)
    {
        SetLabel(text, Color.white);
    }

    public void SetLabel(string text, Color color)
    {
        if (labelText == null) return;
        labelText.text = text;
        labelText.color = color;
    }

    public void SetHint(string text)
    {
        if (hintText != null) hintText.text = text;
    }

    public void SetKeys(string richText)
    {
        if (keysText == null) return;
        keysText.gameObject.SetActive(true);
        keysText.text = richText;
    }

    // ---------- ภาพ ----------

    public void SetVisual(Sprite sprite)
    {
        if (visualImage == null) return;
        visualImage.sprite = sprite;
        visualImage.preserveAspect = true;
        visualImage.gameObject.SetActive(sprite != null);
    }

    // ---------- แถบ ----------

    public void SetBarVisible(bool visible)
    {
        if (barBackground != null) barBackground.gameObject.SetActive(visible);
    }

    // โซน Good และ Perfect (center / width เป็นสัดส่วน 0–1 ของแถบ)
    public void SetZone(float center, float width, float perfectWidth)
    {
        SetActive(zone, true);
        SetActive(perfectZone, true);
        SetSpan(zone, center - width * 0.5f, center + width * 0.5f);
        SetSpan(perfectZone, center - perfectWidth * 0.5f, center + perfectWidth * 0.5f);
    }

    // เข็ม (position = 0–1)
    public void SetNeedle(float position)
    {
        if (needle == null) return;
        SetActive(needle, true);
        float x = Mathf.Clamp01(position);
        needle.anchorMin = new Vector2(x, 0f);
        needle.anchorMax = new Vector2(x, 1f);
        needle.pivot = new Vector2(0.5f, 0.5f);
        needle.sizeDelta = new Vector2(needleWidth, needleOvershoot * 2f);
        needle.anchoredPosition = Vector2.zero;
    }

    // เข็มเปลี่ยนสีชั่วครู่ (บอกผลแต่ละครั้งที่กด)
    public void PulseNeedle(Color color)
    {
        if (needleImage == null) return;
        needleImage.color = color;
        needlePulseTimer = needlePulseDuration;
    }

    // แถบเติม (ratio = 0–1)
    public void SetFill(float ratio)
    {
        SetActive(fill, true);
        SetSpan(fill, 0f, Mathf.Clamp01(ratio));
    }

    // [2.5b] ความคืบหน้าในแถบแยกใต้แถบหลัก (ratio = 0–1) ไม่มีแถบแยก = ใช้แถบเติมในแถบหลัก
    public void SetProgress(float ratio)
    {
        if (progressRoot == null || progressFill == null)
        {
            SetFill(ratio);
            return;
        }

        if (!progressRoot.activeSelf) progressRoot.SetActive(true);
        SetSpan(progressFill, 0f, Mathf.Clamp01(ratio));
    }

    // เวลาที่เหลือ (ratio = 0–1)
    public void SetTimer(float ratio)
    {
        if (timerFill == null) return;
        SetSpan(timerFill, 0f, Mathf.Clamp01(ratio));
    }

    // แถบกระพริบสีตามผล
    public void FlashBar(Color color)
    {
        if (barBackground != null) barBackground.color = color;
    }

    public void ResetBarColor()
    {
        if (barBackground != null) barBackground.color = barColor;
    }

    // ---------- ภายใน ----------

    private static void SetActive(RectTransform rt, bool active)
    {
        if (rt != null && rt.gameObject.activeSelf != active) rt.gameObject.SetActive(active);
    }

    // ยืดวัตถุในแถบจาก min ถึง max (0–1) ด้วย Anchor X
    private static void SetSpan(RectTransform rt, float min, float max)
    {
        if (rt == null) return;
        min = Mathf.Clamp01(min);
        max = Mathf.Clamp01(Mathf.Max(min, max));
        rt.anchorMin = new Vector2(min, 0f);
        rt.anchorMax = new Vector2(max, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
