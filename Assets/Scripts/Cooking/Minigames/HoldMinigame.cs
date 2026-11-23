using UnityEngine;
using UnityEngine.InputSystem;

// [2.5b] มินิเกม Hold: กด Space ค้าง แถบจะค่อยๆ เติมขึ้น ปล่อยให้ตรงเส้นเป้า (เท ตวง)
// - ปล่อยในช่วง Perfect = Perfect / ในช่วง Good = Good / ขาดหรือเกินมาก = OK
// - กดค้างจนแถบเต็ม (ล้น) = OK ทันที / ไม่เริ่มกดภายในเวลา = OK
// - ใช้ชิ้นส่วนบน view: แถบเติม (Fill) + โซน Good (Zone) + โซน Perfect (PerfectZone) รอบเส้นเป้า
public class HoldMinigame : ICookingMinigame
{
    private CookingMinigameView view;

    private float fillSeconds;
    private float zoneWidth;
    private float perfectWidth;
    private float timeLimit;
    private float target;

    private float progress;
    private float waitTimer;
    private bool holding;

    public DishQuality Result { get; private set; } = DishQuality.Ok;

    public void Begin(CookingStep step, CookingSettings settings, CookingMinigameView minigameView, int difficulty)
    {
        view = minigameView;

        fillSeconds = settings.HoldFillSeconds(step, difficulty);
        zoneWidth = settings.HoldZoneWidth(step, difficulty);
        perfectWidth = settings.HoldPerfectWidth(step, difficulty);
        timeLimit = settings.HoldTimeLimit(step, difficulty);

        // เส้นเป้าสุ่ม ไม่ให้โซนเลยขอบแถบ
        float min = Mathf.Min(settings.holdTargetMin, settings.holdTargetMax);
        float max = Mathf.Max(settings.holdTargetMin, settings.holdTargetMax);
        float half = zoneWidth * 0.5f;
        target = Mathf.Clamp(Random.Range(min, max), half, 1f - half);

        progress = 0f;
        waitTimer = 0f;
        holding = false;
        Result = DishQuality.Ok;

        if (view != null)
        {
            view.SetFill(0f);
            view.SetZone(target, zoneWidth, perfectWidth);
            view.SetHint("Hold [Space], release at the line");
        }
    }

    public bool Tick(float deltaTime, Keyboard keyboard)
    {
        bool pressed = keyboard != null && keyboard.spaceKey.isPressed;

        if (!holding)
        {
            // รอเริ่มกด
            waitTimer += deltaTime;
            if (view != null) view.SetTimer(1f - waitTimer / timeLimit);

            if (pressed)
            {
                holding = true;
            }
            else if (waitTimer >= timeLimit)
            {
                Result = DishQuality.Ok;
                return true;
            }
            else
            {
                return false;
            }
        }

        if (pressed)
        {
            progress += deltaTime / fillSeconds;
            if (view != null)
            {
                view.SetFill(progress);
                view.SetTimer(1f);
            }

            // ล้น
            if (progress >= 1f)
            {
                if (view != null) view.SetFill(1f);
                Result = DishQuality.Ok;
                return true;
            }
            return false;
        }

        // ปล่อยแล้ว: วัดระยะจากเส้นเป้า
        float distance = Mathf.Abs(progress - target);
        Result = distance <= perfectWidth * 0.5f ? DishQuality.Perfect :
                 distance <= zoneWidth * 0.5f ? DishQuality.Good : DishQuality.Ok;
        return true;
    }
}
