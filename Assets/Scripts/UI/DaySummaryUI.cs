using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// หน้าสรุปวัน: แสดงตอนนอน/สลบ กด Space/Enter หรือปุ่ม Continue เพื่อไปวันถัดไป
// หมายเหตุ: ต้องวางสคริปต์นี้บนวัตถุที่เปิดอยู่ตลอด (ไม่ใช่ตัวหน้าต่างที่ถูกซ่อน)
public class DaySummaryUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ตัวหน้าต่างที่จะซ่อน/แสดง")]
    [SerializeField] private GameObject windowRoot;

    [Tooltip("หัวข้อ เช่น Day 3 Complete")]
    [SerializeField] private TMP_Text titleText;

    [Tooltip("เนื้อหาสรุป")]
    [SerializeField] private TMP_Text bodyText;

    [Tooltip("ปุ่มไปวันถัดไป (เว้นว่างได้ ใช้คีย์บอร์ดแทน)")]
    [SerializeField] private Button continueButton;

    [Header("Settings")]
    [Tooltip("รอกี่วินาทีก่อนรับปุ่มปิด (กันกดปิดทันทีโดยไม่ได้ตั้งใจ)")]
    [SerializeField] private float inputDelaySeconds = 0.5f;

    [Header("Text")]
    [SerializeField] private string titleFormat = "Day {0} Complete";
    [SerializeField] private string sleptLine = "You went to bed.";
    [SerializeField] private string passedOutLine = "You passed out and woke up at the clinic.";
    [SerializeField] private string shopOpenedLine = "Shop: Opened";
    [SerializeField] private string shopNotOpenedLine = "Shop: Not opened";
    [SerializeField] private string continueHintLine = "Press Space to continue";

    [Header("Text (2.6)")]
    [Tooltip("{0} = จานที่ขาย, {1} = ลูกค้าที่เดินออก")]
    [SerializeField] private string dishesFormat = "Dishes sold: {0}   Customers who left: {1}";
    [Tooltip("{0} = รายได้ร้าน, {1} = ค่าอาหาร, {2} = ทิป")]
    [SerializeField] private string shopIncomeFormat = "Restaurant (already received)   {0}   (dishes {1} + tips {2})";
    [Tooltip("{0} = รายได้ข้ามคืน")]
    [SerializeField] private string overnightFormat = "Shipping box (received now)   {0}";
    [Tooltip("{0} = รวมวันนี้")]
    [SerializeField] private string totalFormat = "Total today   {0}";
    [Tooltip("{0} = ชื่อเสียงที่ได้, {1} = หลอด, {2} = ขนาดหลอด, {3} = เลเวลร้าน")]
    [SerializeField] private string reputationFormat = "Reputation +{0}   ({1:N0} / {2:N0}, shop level {3})";
    [SerializeField] private string barFullLine = "<color=#FFD24A>Reputation bar is full! Visit the builder to upgrade your shop.</color>";
    [Tooltip("{0} = เงินรวม")]
    [SerializeField] private string moneyFormat = "Money   {0}";

    // หน้าต่างเปิดอยู่หรือไม่
    public bool IsOpen { get; private set; }

    // สิ่งที่ต้องทำหลังปิดหน้าต่าง
    private Action onClosed;

    // เวลาจริงที่เปิดหน้าต่าง
    private float openedAt;

    private void Awake()
    {
        if (windowRoot == null) Debug.LogWarning("DaySummaryUI: Window Root is not assigned.", this);
        if (titleText == null) Debug.LogWarning("DaySummaryUI: Title Text is not assigned.", this);
        if (bodyText == null) Debug.LogWarning("DaySummaryUI: Body Text is not assigned.", this);

        // ป้องกันการวางสคริปต์ผิดที่ (บนตัวหน้าต่างเอง)
        if (windowRoot == gameObject)
        {
            Debug.LogWarning("DaySummaryUI: Do not put this script on the Window Root itself. Put it on its parent.", this);
        }

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(Close);
        }

        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(Close);
        }
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        // รอให้ผ่านไปสักครู่ก่อนรับปุ่ม
        if (Time.unscaledTime - openedAt < inputDelaySeconds)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        // Space / Enter = ไปวันถัดไป
        if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    // เปิดหน้าสรุป closedCallback จะถูกเรียกหลังผู้เล่นปิด
    public void Show(DaySummaryData data, Action closedCallback)
    {
        if (data == null)
        {
            Debug.LogWarning("DaySummaryUI: Show was called with null data.", this);
            closedCallback?.Invoke();
            return;
        }

        onClosed = closedCallback;

        if (titleText != null)
        {
            titleText.text = string.Format(titleFormat, data.Day);
        }

        if (bodyText != null)
        {
            bodyText.text = BuildBody(data);
        }

        IsOpen = true;
        openedAt = Time.unscaledTime;
        SetVisible(true);
        Debug.Log($"DaySummaryUI: Showing summary for day {data.Day}.");
    }

    // สร้างข้อความสรุป (เงินและชื่อเสียงจะเพิ่มในเฟสต่อไป)
    private string BuildBody(DaySummaryData data)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(data.Reason == DayEndReason.PassedOut ? passedOutLine : sleptLine);
        sb.AppendLine(data.OpenedShop ? shopOpenedLine : shopNotOpenedLine);

        // [2.6] ร้าน (เงินเข้าไปแล้วตอนขาย แค่สรุป)
        if (data.OpenedShop)
        {
            sb.AppendLine(string.Format(dishesFormat, data.DishesSold, data.CustomersLeft));
            sb.AppendLine(string.Format(shopIncomeFormat, PlayerWallet.Format(data.ShopIncome), PlayerWallet.Format(data.ShopIncome - data.Tips), PlayerWallet.Format(data.Tips)));
        }

        // [2.6b] รายได้ข้ามคืน (เข้ากระเป๋าตอนนี้)
        if (data.OvernightIncome > 0)
        {
            sb.AppendLine(string.Format(overnightFormat, PlayerWallet.Format(data.OvernightIncome)));
        }
        if (data.OpenedShop || data.OvernightIncome > 0)
        {
            sb.AppendLine(string.Format(totalFormat, PlayerWallet.Format(data.ShopIncome + data.OvernightIncome)));
        }

        // [2.6] ชื่อเสียง + เงินรวม
        if (data.BarSize > 0)
        {
            sb.AppendLine(string.Format(reputationFormat, data.ReputationGained, data.Reputation, data.BarSize, data.ShopLevel));
        }
        if (data.BarFull)
        {
            sb.AppendLine(barFullLine);
        }
        sb.AppendLine(string.Format(moneyFormat, PlayerWallet.Format(data.Money)));
        sb.AppendLine();
        sb.Append(continueHintLine);
        return sb.ToString();
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        SetVisible(false);
        Debug.Log("DaySummaryUI: Closed.");

        // เคลียร์ก่อนเรียก กันถูกเรียกซ้ำ
        Action callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    private void SetVisible(bool visible)
    {
        if (windowRoot != null)
        {
            windowRoot.SetActive(visible);
        }
    }
}
