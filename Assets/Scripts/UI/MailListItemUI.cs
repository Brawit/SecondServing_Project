using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [2.5c] จดหมาย 1 รายการในกล่องจดหมายที่โต๊ะ (MailUI โหมด Archive) คลิก = เปิดอ่าน
// MailUI สร้างรายการนี้จากแม่แบบ (template)
public class MailListItemUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ปุ่มของรายการนี้ (บนตัวเดียวกัน)")]
    [SerializeField] private Button button;

    [Tooltip("บรรทัดบน: NEW + ชื่อผู้ส่ง")]
    [SerializeField] private TMP_Text titleText;

    [Tooltip("บรรทัดล่าง: หัวข้อ · วันที่ได้ (+ มีของรอรับ)")]
    [SerializeField] private TMP_Text infoText;

    [Tooltip("กรอบ/พื้นที่แสดงว่ากำลังอ่านฉบับนี้ (เว้นว่างได้)")]
    [SerializeField] private GameObject selectedFrame;

    [Header("Text")]
    [Tooltip("ป้ายหน้าชื่อเมื่อยังไม่อ่าน")]
    [SerializeField] private string newTag = "<color=#FFD24A><b>NEW</b></color>  ";

    [Tooltip("บรรทัดล่าง ({0} = หัวข้อ, {1} = วันที่ได้)")]
    [SerializeField] private string infoFormat = "{0} · Day {1}";

    [Tooltip("ต่อท้ายเมื่อยังมีของแนบที่ไม่ได้รับ")]
    [SerializeField] private string giftTag = "  <color=#FFD24A>· gift</color>";

    public MailLetter Letter { get; private set; }

    // ส่งสัญญาณเมื่อคลิกรายการนี้
    public event Action<MailLetter> OnClicked;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.AddListener(() => OnClicked?.Invoke(Letter));
        }
    }

    public void Show(MailLetter letter, bool selected)
    {
        Letter = letter;

        if (titleText != null)
        {
            string sender = letter != null ? letter.Sender : "";
            titleText.text = letter != null && !letter.isRead ? newTag + sender : sender;
        }

        if (infoText != null)
        {
            string text = letter != null ? string.Format(infoFormat, letter.Subject, letter.dayReceived) : "";
            if (letter != null && letter.HasUnclaimed) text += giftTag;
            infoText.text = text;
        }

        if (selectedFrame != null)
        {
            selectedFrame.SetActive(selected);
        }
    }
}
