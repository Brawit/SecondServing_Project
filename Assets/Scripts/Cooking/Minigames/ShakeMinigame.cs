using UnityEngine.InputSystem;

// [2.5b] มินิเกม Shake: กดสลับขึ้น-ลง (↑ ↓ หรือ W S) ให้แถบเต็มภายในเวลา (เขย่า)
// การเล่นอยู่ใน AlternateKeysMinigame / ค่าปกติอยู่ใน CookingSettings หัวข้อ Shake defaults
public class ShakeMinigame : AlternateKeysMinigame
{
    protected override int GetPresses(CookingStep step, int difficulty) => settings.ShakePresses(step, difficulty);
    protected override float GetTimeLimit(CookingStep step, int difficulty) => settings.ShakeTimeLimit(step, difficulty);
    protected override float PerfectTimeRatio => settings.shakePerfectTimeRatio;
    protected override float DecayPerSecond => settings.shakeDecayPerSecond;
    protected override float DecayDelay => settings.shakeDecayDelay;

    protected override bool FirstPressed(Keyboard keyboard) => keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame;
    protected override bool SecondPressed(Keyboard keyboard) => keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame;

    protected override string FirstKeyText => settings.keyUpText;
    protected override string SecondKeyText => settings.keyDownText;
    protected override string HintFormat => "Shake  {0}    {1}  (W / S)";
}
