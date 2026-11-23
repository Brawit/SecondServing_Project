using UnityEngine.InputSystem;

// [2.5b] มินิเกม Whisk: กดสลับซ้าย-ขวา (← → หรือ A D) ให้แถบเต็มภายในเวลา (ตี ผสม)
// การเล่นอยู่ใน AlternateKeysMinigame / ค่าปกติอยู่ใน CookingSettings หัวข้อ Whisk defaults
public class WhiskMinigame : AlternateKeysMinigame
{
    protected override int GetPresses(CookingStep step, int difficulty) => settings.WhiskPresses(step, difficulty);
    protected override float GetTimeLimit(CookingStep step, int difficulty) => settings.WhiskTimeLimit(step, difficulty);
    protected override float PerfectTimeRatio => settings.whiskPerfectTimeRatio;
    protected override float DecayPerSecond => settings.whiskDecayPerSecond;
    protected override float DecayDelay => settings.whiskDecayDelay;

    protected override bool FirstPressed(Keyboard keyboard) => keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame;
    protected override bool SecondPressed(Keyboard keyboard) => keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame;

    protected override string FirstKeyText => settings.keyLeftText;
    protected override string SecondKeyText => settings.keyRightText;
    protected override string HintFormat => "Whisk  {0}    {1}  (A / D)";
}
