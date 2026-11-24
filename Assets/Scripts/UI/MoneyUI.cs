using TMPro;
using UnityEngine;

// [2.6] เงินบนจอ (ใต้นาฬิกา) เช่น "B 1,250" แสดงตลอด รวมตอนเปิดร้าน
// - เงินเปลี่ยน = ตัวเลขวิ่งค่อยๆ เพิ่ม/ลดไปหาค่าจริง (ผู้ใช้ขอ) ระหว่างวิ่ง: เงินเข้า = สีทอง / เงินออก = สีแดง
// - เงินเปลี่ยนซ้อนระหว่างวิ่ง = วิ่งต่อจากตัวเลขที่แสดงอยู่ไปหาค่าใหม่
// สร้างและผูกอัตโนมัติได้จาก Tools > Second Serving > Build Economy
public class MoneyUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ข้อความเงิน")]
    [SerializeField] private TMP_Text moneyText;

    [Header("Rolling Number")]
    [Tooltip("เวลาที่ตัวเลขวิ่งจนถึงค่าจริง (วินาที)")]
    [Min(0.05f)] [SerializeField] private float rollSeconds = 0.8f;

    [Tooltip("วิ่งนานขึ้นเมื่อเงินเปลี่ยนเยอะ: เพิ่มวินาทีต่อ B 10,000 (0 = เท่ากันทุกครั้ง)")]
    [Min(0f)] [SerializeField] private float extraSecondsPer10k = 0.4f;

    [Tooltip("วิ่งนานสุดกี่วินาที")]
    [Min(0.05f)] [SerializeField] private float maxRollSeconds = 2f;

    [Header("Look")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color gainColor = new Color(1f, 0.82f, 0.29f);
    [SerializeField] private Color lossColor = new Color(1f, 0.45f, 0.4f);

    private PlayerWallet wallet;
    private float shown;      // ตัวเลขที่แสดงอยู่
    private float rollFrom;
    private int rollTo;
    private float rollTime;
    private float rollDuration;
    private bool rolling;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
        SnapToWallet();
    }

    private void OnDisable()
    {
        if (wallet != null)
        {
            wallet.OnMoneyChanged -= HandleMoneyChanged;
        }
        wallet = null;
    }

    private void TrySubscribe()
    {
        if (wallet != null || PlayerWallet.Instance == null)
        {
            return;
        }

        wallet = PlayerWallet.Instance;
        wallet.OnMoneyChanged += HandleMoneyChanged;
    }

    // ตั้งตัวเลขตรงกับเงินจริงทันที (เริ่มเกม / โหลดเซฟก่อน UI พร้อม)
    private void SnapToWallet()
    {
        rolling = false;
        shown = wallet != null ? wallet.Money : 0;
        Draw(normalColor);
    }

    private void HandleMoneyChanged(int oldValue, int newValue)
    {
        // เริ่มวิ่งจากตัวเลขที่แสดงอยู่ (ถ้ากำลังวิ่งอยู่ก็ต่อจากตรงนั้น)
        rollFrom = rolling ? shown : oldValue;
        rollTo = newValue;
        rollTime = 0f;
        float extra = extraSecondsPer10k * Mathf.Abs(newValue - rollFrom) / 10000f;
        rollDuration = Mathf.Min(maxRollSeconds, rollSeconds + extra);
        rolling = true;
    }

    private void Update()
    {
        if (wallet == null)
        {
            TrySubscribe();
            if (wallet != null) SnapToWallet();
            return;
        }

        if (!rolling)
        {
            return;
        }

        // ใช้เวลาจริง (นาฬิกาเกม/หน้าต่างหยุดเวลาก็ยังวิ่ง)
        rollTime += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(rollTime / rollDuration);
        float eased = 1f - (1f - t) * (1f - t); // เร็วตอนต้น ช้าลงตอนท้าย
        shown = Mathf.Lerp(rollFrom, rollTo, eased);

        if (t >= 1f)
        {
            rolling = false;
            shown = rollTo;
            Draw(normalColor);
            return;
        }

        Draw(rollTo >= rollFrom ? gainColor : lossColor);
    }

    private void Draw(Color color)
    {
        if (moneyText == null)
        {
            return;
        }

        moneyText.text = PlayerWallet.Format(Mathf.RoundToInt(shown));
        moneyText.color = color;
    }
}
