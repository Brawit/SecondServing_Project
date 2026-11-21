using TMPro;
using UnityEngine;

// หน้าจอจับเวลาระหว่างเปิดร้าน: ซ่อนนาฬิกาปกติ แล้วแสดงเวลานับถอยหลังของรอบขาย (นาที:วินาที.เสี้ยววินาที)
// พร้อมบอกสถานะ: จานที่เหลือ / ลูกค้าที่นั่งรออาหาร / ขายหมดแล้ว / หมดเวลาแล้ว
// ปิดร้านแล้วกลับไปแสดงนาฬิกาปกติ
// หมายเหตุ: วางสคริปต์นี้บนวัตถุที่เปิดอยู่ตลอด (ไม่ใช่ตัว Timer Panel ที่ถูกซ่อน)
// สร้างและผูกอัตโนมัติได้จากเมนู Tools > Second Serving > Build Service Timer
public class ServiceTimerUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("แผงนาฬิกาปกติ (ClockPanel) จะถูกซ่อนระหว่างเปิดร้าน")]
    [SerializeField] private GameObject clockPanel;

    [Tooltip("แผงจับเวลาร้าน แสดงเฉพาะตอนเปิดร้าน")]
    [SerializeField] private GameObject timerPanel;

    [Tooltip("ข้อความเวลานับถอยหลัง เช่น 02:59.87")]
    [SerializeField] private TMP_Text timerText;

    [Tooltip("ข้อความสถานะ เช่น Dishes left 6 · Customers 2")]
    [SerializeField] private TMP_Text statusText;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;

    [Tooltip("สีตอนเวลาเหลือน้อย")]
    [SerializeField] private Color warningColor = new Color(1f, 0.8f, 0.3f);

    [Tooltip("สีตอนใกล้หมดเวลามาก / หมดเวลาแล้ว")]
    [SerializeField] private Color dangerColor = new Color(1f, 0.35f, 0.3f);

    [Tooltip("สีตอนขายหมด")]
    [SerializeField] private Color soldOutColor = new Color(0.45f, 1f, 0.55f);

    [Header("Thresholds (seconds)")]
    [Tooltip("เหลือเวลาน้อยกว่านี้ = สีเตือน")]
    [Min(0f)] [SerializeField] private float warningSeconds = 60f;

    [Tooltip("เหลือเวลาน้อยกว่านี้ = สีแดง และตัวเลขเต้น")]
    [Min(0f)] [SerializeField] private float dangerSeconds = 20f;

    [Header("Pulse")]
    [Tooltip("ตัวเลขเต้นตอนใกล้หมดเวลา (ขยายสูงสุดเท่าไร)")]
    [Min(0f)] [SerializeField] private float pulseAmount = 0.12f;

    [Tooltip("ความเร็วการเต้น (ครั้งต่อวินาที)")]
    [Min(0f)] [SerializeField] private float pulseSpeed = 2f;

    private bool wasServing;

    private void Start()
    {
        if (timerPanel == null) Debug.LogWarning("ServiceTimerUI: Timer Panel is not assigned.", this);
        if (timerPanel == gameObject) Debug.LogWarning("ServiceTimerUI: Do not put this script on the Timer Panel itself. Put it on its parent.", this);
        if (clockPanel == null) Debug.LogWarning("ServiceTimerUI: Clock Panel is not assigned. The normal clock will stay visible.", this);

        ApplyMode(IsServing());
    }

    private void Update()
    {
        bool serving = IsServing();
        if (serving != wasServing)
        {
            ApplyMode(serving);
        }

        if (serving)
        {
            Draw(ShopManager.Instance);
        }
    }

    private static bool IsServing()
    {
        return ShopManager.Instance != null && ShopManager.Instance.IsServing;
    }

    // สลับระหว่างนาฬิกาปกติ กับหน้าจับเวลาร้าน
    private void ApplyMode(bool serving)
    {
        wasServing = serving;

        if (clockPanel != null) clockPanel.SetActive(!serving);
        if (timerPanel != null) timerPanel.SetActive(serving);

        if (!serving && timerText != null)
        {
            timerText.transform.localScale = Vector3.one;
        }
    }

    private void Draw(ShopManager shop)
    {
        float remaining = Mathf.Max(0f, shop.ServiceTimeRemaining);
        // นับเฉพาะลูกค้าที่นั่งแล้วและรออาหาร (คนที่ยังเดินไปนั่ง หรือได้อาหารแล้ว ไม่นับ)
        int customers = CustomerManager.Instance != null ? CustomerManager.Instance.SeatedWaitingCount : shop.ActiveCustomers;
        DailyMenu menu = DailyMenu.Instance;
        int dishesLeft = menu != null && menu.IsActive ? menu.TotalRemaining : 0;

        // ---------- เวลา ----------
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);
            int hundredths = Mathf.FloorToInt((remaining * 100f) % 100f);
            // <mspace> ให้ตัวเลขกว้างเท่ากันทุกตัว ตัวเลขจะไม่ขยับไปมาตอนนับถอยหลังเร็วๆ
            timerText.text = $"<mspace=0.6em>{minutes:00}:{seconds:00}.{hundredths:00}</mspace>";

            Color color;
            if (shop.EndedBySellOut) color = soldOutColor;
            else if (shop.IsServiceTimeUp || remaining <= dangerSeconds) color = dangerColor;
            else if (remaining <= warningSeconds) color = warningColor;
            else color = normalColor;
            timerText.color = color;

            // ใกล้หมดเวลา: ตัวเลขเต้น
            float scale = 1f;
            if (!shop.IsServiceTimeUp && remaining <= dangerSeconds && pulseAmount > 0f)
            {
                scale += pulseAmount * Mathf.Abs(Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI));
            }
            timerText.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // ---------- สถานะ ----------
        if (statusText != null)
        {
            string people = customers == 1 ? "1 customer" : $"{customers} customers";

            if (shop.EndedBySellOut)
            {
                statusText.text = customers > 0 ? $"SOLD OUT! Waiting for {people}" : "SOLD OUT!";
            }
            else if (shop.IsServiceTimeUp)
            {
                statusText.text = customers > 0 ? $"TIME'S UP! Waiting for {people}" : "TIME'S UP!";
            }
            else
            {
                statusText.text = $"Dishes left {dishesLeft}  ·  Waiting {customers}";
            }
        }
    }
}
