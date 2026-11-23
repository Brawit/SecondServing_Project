using UnityEngine;
using UnityEngine.InputSystem;

// มินิเกม Mash: รัว Space ให้แถบเต็มภายในเวลา (หั่น ตำ นวด คั้น)
// - กด 1 ครั้ง = แถบขึ้น 1/presses (กดไม่กี่ครั้งก็เต็ม ไม่ต้องรัวจนปุ่มพัง)
// - หยุดกดเกิน mashDecayDelay วินาที = แถบค่อยๆ ลดลง (mashDecayPerSecond ต่อวินาที)
// - เต็มเร็ว (ใช้เวลาไม่เกิน mashPerfectTimeRatio ของเวลาทั้งหมด) = Perfect / เต็มทันเวลา = Good / ไม่ทัน = OK
public class MashMinigame : ICookingMinigame
{
    private CookingMinigameView view;

    private int required;
    private float perPress;
    private float timeLimit;
    private float perfectTime;
    private float decayPerSecond;
    private float decayDelay;

    private float progress;
    private float elapsed;
    private float sinceLastPress;

    public DishQuality Result { get; private set; } = DishQuality.Ok;

    public void Begin(CookingStep step, CookingSettings settings, CookingMinigameView minigameView, int difficulty)
    {
        view = minigameView;

        required = settings.MashPresses(step, difficulty);
        perPress = 1f / required;
        timeLimit = settings.MashTimeLimit(step, difficulty);
        perfectTime = timeLimit * settings.mashPerfectTimeRatio;
        decayPerSecond = settings.mashDecayPerSecond;
        decayDelay = settings.mashDecayDelay;

        progress = 0f;
        elapsed = 0f;
        sinceLastPress = 0f;
        Result = DishQuality.Ok;

        if (view != null)
        {
            view.SetFill(0f);
            view.SetHint("Mash [Space]!");
        }
    }

    public bool Tick(float deltaTime, Keyboard keyboard)
    {
        elapsed += deltaTime;
        sinceLastPress += deltaTime;

        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            progress += perPress;
            sinceLastPress = 0f;
        }
        else if (sinceLastPress > decayDelay)
        {
            // ไม่กด = ค่อยๆ ลด
            progress = Mathf.Max(0f, progress - decayPerSecond * deltaTime);
        }

        if (view != null)
        {
            view.SetFill(progress);
            view.SetTimer(1f - elapsed / timeLimit);
        }

        // เผื่อปัดเศษ (เช่น 1/3 × 3 = 0.9999)
        if (progress >= 0.999f)
        {
            Result = elapsed <= perfectTime ? DishQuality.Perfect : DishQuality.Good;
            return true;
        }

        if (elapsed >= timeLimit)
        {
            // ไม่ทัน: ยังได้จาน OK (ทำพลาดไม่เสียวัตถุดิบ)
            Result = DishQuality.Ok;
            return true;
        }

        return false;
    }
}
