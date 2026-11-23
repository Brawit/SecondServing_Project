using UnityEngine;
using UnityEngine.InputSystem;

// [2.5b] ฐานของมินิเกมแบบ "กดสลับ 2 ปุ่ม" ให้แถบเต็มภายในเวลา (Stir = ซ้าย-ขวา / Shake = ขึ้น-ลง)
// - กดถูกข้าง = แถบขึ้น 1/presses / กดข้างเดิมซ้ำ = ไม่นับ
// - หยุดกดเกิน decayDelay วินาที = แถบค่อยๆ ลด
// - เต็มเร็ว (ไม่เกิน perfectTimeRatio ของเวลา) = Perfect / เต็มทันเวลา = Good / ไม่ทัน = OK
// - ปุ่มที่ต้องกดถัดไปแสดงในบรรทัดวิธีเล่นใต้แถบ (แถบหลักใช้แสดงความคืบหน้า)
// คลาสลูกบอกแค่ว่าใช้ปุ่มไหนและค่าจาก CookingSettings ตัวไหน
public abstract class AlternateKeysMinigame : ICookingMinigame
{
    protected CookingSettings settings;
    private CookingMinigameView view;

    private float perPress;
    private float timeLimit;
    private float perfectTime;
    private float decayPerSecond;
    private float decayDelay;

    private float progress;
    private float elapsed;
    private float sinceLastPress;
    private bool expectFirst = true;

    public DishQuality Result { get; private set; } = DishQuality.Ok;

    // ---------- ให้คลาสลูกกำหนด ----------

    protected abstract int GetPresses(CookingStep step, int difficulty);
    protected abstract float GetTimeLimit(CookingStep step, int difficulty);
    protected abstract float PerfectTimeRatio { get; }
    protected abstract float DecayPerSecond { get; }
    protected abstract float DecayDelay { get; }

    // ปุ่มแรก / ปุ่มที่สอง (กดสลับกัน เริ่มจากปุ่มแรก)
    protected abstract bool FirstPressed(Keyboard keyboard);
    protected abstract bool SecondPressed(Keyboard keyboard);

    // ข้อความแทนปุ่ม และข้อความวิธีเล่น เช่น "Alternate {0} {1}  (A / D)"
    protected abstract string FirstKeyText { get; }
    protected abstract string SecondKeyText { get; }
    protected abstract string HintFormat { get; }

    // ---------- ICookingMinigame ----------

    public void Begin(CookingStep step, CookingSettings cookingSettings, CookingMinigameView minigameView, int difficulty)
    {
        settings = cookingSettings;
        view = minigameView;

        perPress = 1f / Mathf.Max(2, GetPresses(step, difficulty));
        timeLimit = Mathf.Max(0.5f, GetTimeLimit(step, difficulty));
        perfectTime = timeLimit * PerfectTimeRatio;
        decayPerSecond = DecayPerSecond;
        decayDelay = DecayDelay;

        progress = 0f;
        elapsed = 0f;
        sinceLastPress = 0f;
        expectFirst = true;
        Result = DishQuality.Ok;

        if (view != null)
        {
            view.SetFill(0f);
            RefreshHint();
        }
    }

    public bool Tick(float deltaTime, Keyboard keyboard)
    {
        elapsed += deltaTime;
        sinceLastPress += deltaTime;

        bool first = keyboard != null && FirstPressed(keyboard);
        bool second = keyboard != null && SecondPressed(keyboard);

        if ((first && expectFirst) || (second && !expectFirst))
        {
            progress += perPress;
            sinceLastPress = 0f;
            expectFirst = !expectFirst;
            RefreshHint();
        }
        else if (sinceLastPress > decayDelay)
        {
            progress = Mathf.Max(0f, progress - decayPerSecond * deltaTime);
        }

        if (view != null)
        {
            view.SetFill(progress);
            view.SetTimer(1f - elapsed / timeLimit);
        }

        if (progress >= 0.999f)
        {
            Result = elapsed <= perfectTime ? DishQuality.Perfect : DishQuality.Good;
            return true;
        }

        if (elapsed >= timeLimit)
        {
            Result = DishQuality.Ok;
            return true;
        }

        return false;
    }

    // แสดงปุ่มที่ต้องกดถัดไป ตัวที่ต้องกด = เหลืองตัวใหญ่ อีกตัว = จาง
    private void RefreshHint()
    {
        if (view == null)
        {
            return;
        }

        string color = ColorUtility.ToHtmlStringRGB(view.CurrentKeyColor);
        string a = $"<noparse>{FirstKeyText}</noparse>";
        string b = $"<noparse>{SecondKeyText}</noparse>";

        string active(string t) => $"<size=130%><b><color=#{color}>[{t}]</color></b></size>";
        string idle(string t) => $"<alpha=#66>{t}<alpha=#FF>";

        string first = expectFirst ? active(a) : idle(a);
        string second = expectFirst ? idle(b) : active(b);
        view.SetHint(string.Format(HintFormat, first, second));
    }
}
