using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// แถบค่าสถานะบนจอ: Health / Stamina / Mana / FEVER
// แต่ละแถบ: รูปแถบสี (Fill) วางเป็นลูกของพื้นหลัง แล้วสคริปต์จะยืด/หดด้วย Anchor Max X
// (ใช้วิธีนี้แทน Image แบบ Filled เพราะ Filled ต้องมี Source Image ถึงจะทำงาน)
// สร้างให้อัตโนมัติได้จากเมนู Tools > Second Serving > Build Stats HUD
public class StatsUI : MonoBehaviour
{
    // ส่วนประกอบของแถบ 1 แถบ
    [Serializable]
    public class StatBar
    {
        [Tooltip("รูปแถบสีที่ยืด/หด (ต้องเป็นลูกของพื้นหลังแถบ)")]
        public Image fillImage;

        [Tooltip("ข้อความตัวเลข เช่น 80/100 (เว้นว่างได้)")]
        public TMP_Text valueText;

        [Tooltip("สีของแถบ")]
        public Color fillColor = Color.white;
    }

    [Header("References")]
    [Tooltip("ค่าสถานะของผู้เล่น (เว้นว่างไว้ จะหาในฉากให้เอง)")]
    [SerializeField] private PlayerStats playerStats;

    [Header("Bars")]
    [SerializeField] private StatBar healthBar = new StatBar { fillColor = new Color(0.85f, 0.25f, 0.25f, 1f) };
    [SerializeField] private StatBar staminaBar = new StatBar { fillColor = new Color(0.35f, 0.8f, 0.35f, 1f) };
    [SerializeField] private StatBar manaBar = new StatBar { fillColor = new Color(0.3f, 0.5f, 0.95f, 1f) };
    [SerializeField] private StatBar feverBar = new StatBar { fillColor = new Color(1f, 0.55f, 0.1f, 1f) };

    [Header("FEVER Full Effect")]
    [Tooltip("สีกะพริบของแถบ FEVER ตอนเต็ม")]
    [SerializeField] private Color feverFullColor = new Color(1f, 0.9f, 0.3f, 1f);

    [Tooltip("ความเร็วกะพริบตอน FEVER เต็ม")]
    [Min(0f)] [SerializeField] private float feverPulseSpeed = 4f;

    private void Awake()
    {
        if (playerStats == null)
        {
            playerStats = FindFirstObjectByType<PlayerStats>();
        }

        if (playerStats == null)
        {
            Debug.LogWarning("StatsUI: No PlayerStats found in the scene.", this);
        }
    }

    private void OnEnable()
    {
        if (playerStats != null)
        {
            playerStats.OnStatsChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (playerStats != null)
        {
            playerStats.OnStatsChanged -= Refresh;
        }
    }

    private void Update()
    {
        // แถบ FEVER กะพริบตอนเต็ม (บอกผู้เล่นว่าใช้ท่าไม้ตายได้แล้ว)
        if (playerStats == null || feverBar.fillImage == null)
        {
            return;
        }

        if (playerStats.IsFeverFull)
        {
            float t = (Mathf.Sin(Time.unscaledTime * feverPulseSpeed) + 1f) * 0.5f;
            feverBar.fillImage.color = Color.Lerp(feverBar.fillColor, feverFullColor, t);
        }
    }

    // อัปเดตทุกแถบตามค่าปัจจุบัน
    public void Refresh()
    {
        if (playerStats == null)
        {
            return;
        }

        ApplyBar(healthBar, playerStats.Health, playerStats.MaxHealth);
        ApplyBar(staminaBar, playerStats.Stamina, playerStats.MaxStamina);
        ApplyBar(manaBar, playerStats.Mana, playerStats.MaxMana);
        ApplyBar(feverBar, playerStats.Fever, playerStats.MaxFever);
    }

    private static void ApplyBar(StatBar bar, float current, int max)
    {
        if (bar == null)
        {
            return;
        }

        if (bar.fillImage != null)
        {
            float ratio = max > 0 ? Mathf.Clamp01(current / max) : 0f;

            // ยึดซ้าย ยืดเต็มความสูง แล้วให้ความกว้างเท่ากับสัดส่วนของค่า
            RectTransform rt = bar.fillImage.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(ratio, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            bar.fillImage.color = bar.fillColor;
        }

        if (bar.valueText != null)
        {
            // ปัดลงเพื่อไม่ให้แสดงว่าเต็มก่อนเต็มจริง
            bar.valueText.text = $"{Mathf.FloorToInt(current)}/{max}";
        }
    }
}
