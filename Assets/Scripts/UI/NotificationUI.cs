using TMPro;
using UnityEngine;

// กล่องข้อความแจ้งเตือนชั่วคราว แสดงไม่กี่วินาทีแล้วหายไปเอง
// เรียกจากที่ไหนก็ได้ผ่าน NotificationUI.Post("ข้อความ")
// หมายเหตุ: ต้องวางสคริปต์นี้บนวัตถุที่เปิดอยู่ตลอด (ไม่ใช่ตัวกล่องที่ถูกซ่อน)
public class NotificationUI : MonoBehaviour
{
    public static NotificationUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("ตัวกล่องข้อความที่จะซ่อน/แสดง")]
    [SerializeField] private GameObject windowRoot;

    [Tooltip("ข้อความในกล่อง")]
    [SerializeField] private TMP_Text messageText;

    [Header("Settings")]
    [Tooltip("แสดงนานกี่วินาที ถ้าไม่ได้ระบุตอนเรียก")]
    [SerializeField] private float defaultDuration = 3f;

    // เวลาที่เหลือก่อนซ่อน (นับเวลาจริง ไม่หยุดตามนาฬิกาในเกม)
    private float hideTimer = 0f;

    private void Awake()
    {
        // ป้องกันไม่ให้มีกล่องแจ้งเตือนซ้ำสองตัวในฉาก
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("NotificationUI: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (windowRoot == null) Debug.LogWarning("NotificationUI: Window Root is not assigned.", this);
        if (messageText == null) Debug.LogWarning("NotificationUI: Message Text is not assigned.", this);

        // ป้องกันการวางสคริปต์ผิดที่ (บนตัวกล่องเอง)
        if (windowRoot == gameObject)
        {
            Debug.LogWarning("NotificationUI: Do not put this script on the Window Root itself. Put it on its parent.", this);
        }

        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (hideTimer <= 0f)
        {
            return;
        }

        hideTimer -= Time.unscaledDeltaTime;
        if (hideTimer <= 0f)
        {
            SetVisible(false);
        }
    }

    // แสดงข้อความ (duration <= 0 คือใช้ค่าเริ่มต้น) ถ้ามีข้อความเดิมอยู่จะถูกแทนที่
    // [2.5c] เฟรมที่แสดงข้อความล่าสุด (ใช้ต่อข้อความที่มาพร้อมกัน)
    private int lastShowFrame = -1;

    public void Show(string message, float duration = -1f)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        // [2.5c] หลายข้อความในเฟรมเดียวกัน (เช่น ตอนเช้า: จดหมาย + เมนูนิยม) = ต่อกันคนละบรรทัด ไม่ทับกัน
        bool sameFrame = Time.frameCount == lastShowFrame && messageText != null && !string.IsNullOrEmpty(messageText.text);
        lastShowFrame = Time.frameCount;

        if (messageText != null)
        {
            messageText.text = sameFrame ? messageText.text + "\n" + message : message;
        }

        float newTimer = duration > 0f ? duration : defaultDuration;
        hideTimer = sameFrame ? Mathf.Max(hideTimer, newTimer) : newTimer;
        SetVisible(true);
    }

    // ซ่อนทันที
    public void Hide()
    {
        hideTimer = 0f;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (windowRoot != null)
        {
            windowRoot.SetActive(visible);
        }
    }

    // ---------- ทางลัดให้ระบบอื่นเรียก ----------

    // ส่งข้อความแจ้งเตือน พิมพ์ลง Console ด้วยเสมอ (ถ้าไม่มีกล่องในฉาก ก็ยังเห็นใน Console)
    public static void Post(string message, float duration = -1f)
    {
        Debug.Log($"Notification: {message}");

        if (Instance != null)
        {
            Instance.Show(message, duration);
        }
    }
}
