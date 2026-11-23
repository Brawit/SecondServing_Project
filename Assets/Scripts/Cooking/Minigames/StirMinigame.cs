using UnityEngine;
using UnityEngine.InputSystem;

// [2.5b] มินิเกม Stir: กดค้าง A / D (หรือ ← →) คุม "กระทะ" ให้อยู่ใต้ "อาหาร" ที่วิ่งไปมาเอง (ผัด คน)
// - อาหาร (เข็ม) สุ่มจุดหมายใหม่เป็นระยะ บางทีพุ่งเร็ว บางทีหยุดนิ่ง
// - กระทะ (โซนสีเขียว) มีแรงเฉื่อย: กดค้างเร่งความเร็ว ปล่อยแล้วค่อยๆ หยุด ชนขอบแถบแล้วหยุด
// - อาหารอยู่ในกระทะ = แถบความคืบหน้าเพิ่ม / หลุดกระทะ = แถบลด (เข็มเป็นสีแดงเตือน)
// - แถบเต็มเร็ว (ไม่เกิน stirPerfectTimeRatio ของเวลา) = Perfect / เต็มทันเวลา = Good / หมดเวลา = OK
// - ความยาก ★: อาหารเร็วขึ้น กระทะแคบลง เวลาน้อยลง (CookingSettings)
// ค่าปกติอยู่ใน CookingSettings หัวข้อ Stir defaults / ปรับต่อขั้นได้ด้วย zoneWidth (กระทะ) needleSpeed (ความเร็วอาหาร) timeLimit
public class StirMinigame : ICookingMinigame
{
    private CookingSettings settings;
    private CookingMinigameView view;

    private float panWidth;
    private float foodSpeed;
    private float timeLimit;
    private float perfectTime;

    private float food;
    private float foodGoal;
    private float retargetTimer;
    private float foodSpeedNow;

    private float pan = 0.5f;
    private float panVelocity;

    private float progress;
    private float elapsed;

    public DishQuality Result { get; private set; } = DishQuality.Ok;

    public void Begin(CookingStep step, CookingSettings cookingSettings, CookingMinigameView minigameView, int difficulty)
    {
        settings = cookingSettings;
        view = minigameView;

        panWidth = settings.StirPanWidth(step, difficulty);
        foodSpeed = settings.StirFoodSpeed(step, difficulty);
        timeLimit = settings.StirTimeLimit(step, difficulty);
        perfectTime = timeLimit * settings.stirPerfectTimeRatio;

        // เริ่มให้อาหารอยู่ในกระทะ จะได้ไม่เสียแถบตั้งแต่วินาทีแรก
        pan = 0.5f;
        panVelocity = 0f;
        food = 0.5f;
        PickNewGoal();

        progress = settings.stirStartProgress;
        elapsed = 0f;
        Result = DishQuality.Ok;

        if (view != null)
        {
            view.SetHint("Hold [A] / [D] to keep the food in the pan");
            Refresh(true);
        }
    }

    public bool Tick(float deltaTime, Keyboard keyboard)
    {
        elapsed += deltaTime;

        MoveFood(deltaTime);
        MovePan(deltaTime, keyboard);

        bool inside = Mathf.Abs(food - pan) <= panWidth * 0.5f;
        progress += (inside ? settings.stirFillPerSecond : -settings.stirDrainPerSecond) * deltaTime;
        progress = Mathf.Clamp01(progress);

        if (view != null)
        {
            Refresh(inside);
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

    // ---------- อาหาร ----------

    private void MoveFood(float deltaTime)
    {
        retargetTimer -= deltaTime;
        if (retargetTimer <= 0f || Mathf.Approximately(food, foodGoal))
        {
            PickNewGoal();
        }

        food = Mathf.MoveTowards(food, foodGoal, foodSpeedNow * deltaTime);
    }

    // สุ่มจุดหมายและความเร็วใหม่ (บางครั้งพุ่งเร็ว บางครั้งช้า เหมือนของเด้งในกระทะ)
    private void PickNewGoal()
    {
        foodGoal = Random.Range(0.05f, 0.95f);
        foodSpeedNow = foodSpeed * Random.Range(0.6f, 1.6f);

        float min = Mathf.Min(settings.stirFoodRetargetMin, settings.stirFoodRetargetMax);
        float max = Mathf.Max(settings.stirFoodRetargetMin, settings.stirFoodRetargetMax);
        retargetTimer = Random.Range(min, max);
    }

    // ---------- กระทะ ----------

    private void MovePan(float deltaTime, Keyboard keyboard)
    {
        float input = 0f;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input += 1f;
        }

        if (input != 0f)
        {
            panVelocity += input * settings.stirPanAcceleration * deltaTime;
        }
        else
        {
            // ปล่อยปุ่ม = ค่อยๆ หยุด
            panVelocity = Mathf.MoveTowards(panVelocity, 0f, settings.stirPanFriction * deltaTime);
        }

        panVelocity = Mathf.Clamp(panVelocity, -settings.stirPanMaxSpeed, settings.stirPanMaxSpeed);
        pan += panVelocity * deltaTime;

        // ชนขอบแถบ = หยุด
        float half = panWidth * 0.5f;
        if (pan < half) { pan = half; panVelocity = 0f; }
        if (pan > 1f - half) { pan = 1f - half; panVelocity = 0f; }
    }

    // ---------- หน้าตา ----------

    private void Refresh(bool inside)
    {
        // ความคืบหน้าอยู่แถบแยกด้านล่าง แถบหลักเหลือแค่กระทะกับอาหาร จะได้ดูง่าย
        view.SetProgress(progress);
        view.SetZone(pan, panWidth, 0f);
        view.SetNeedle(food);

        // อาหารหลุดกระทะ = เข็มสีแดง
        if (!inside) view.PulseNeedle(view.WrongKeyColor);
    }
}
