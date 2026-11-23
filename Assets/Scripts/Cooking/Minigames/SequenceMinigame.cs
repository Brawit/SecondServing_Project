using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// มินิเกม Sequence: กดลูกศร (หรือ WASD) ตามลำดับที่แสดง (ปรุง จัดจาน แต่งหน้า)
// - กดผิด = นับเป็นพลาดแล้วไปปุ่มถัดไป (ไม่ต้องเริ่มใหม่)
// - เวลานับถอยหลังรวมทั้งลำดับ (ความยาว × เวลาต่อปุ่ม หรือ timeLimit ของขั้น) หมดเวลา = ปุ่มที่เหลือนับเป็นพลาด
// - ไม่พลาดเลย = Perfect / พลาดไม่เกิน sequenceMistakesForGood = Good / มากกว่านั้น = OK
public class SequenceMinigame : ICookingMinigame
{
    private enum Dir { Up, Down, Left, Right }

    private CookingSettings settings;
    private CookingMinigameView view;

    private readonly List<Dir> keys = new List<Dir>();
    private readonly List<bool> correct = new List<bool>();

    private float totalTime;
    private float elapsed;
    private int index;
    private int mistakes;

    public DishQuality Result { get; private set; } = DishQuality.Ok;

    public void Begin(CookingStep step, CookingSettings cookingSettings, CookingMinigameView minigameView, int difficulty)
    {
        settings = cookingSettings;
        view = minigameView;

        int length = settings.SequenceLength(step, difficulty);

        // เวลารวมทั้งลำดับ: ตั้ง timeLimit ของขั้นไว้ = ใช้ค่านั้น / ไม่งั้น = ความยาว × เวลาต่อปุ่ม
        totalTime = step != null && step.timeLimit > 0f
            ? step.timeLimit * settings.TimeScale(difficulty)
            : length * settings.SequenceTimePerKey(step, difficulty);

        keys.Clear();
        correct.Clear();
        for (int i = 0; i < length; i++)
        {
            // ไม่ให้ปุ่มเดียวกันติดกันเกิน 2 ครั้ง
            Dir next;
            int guard = 0;
            do
            {
                next = (Dir)Random.Range(0, 4);
                guard++;
            }
            while (guard < 10 && i >= 2 && keys[i - 1] == next && keys[i - 2] == next);
            keys.Add(next);
        }

        index = 0;
        mistakes = 0;
        elapsed = 0f;
        Result = DishQuality.Ok;

        if (view != null)
        {
            view.SetBarVisible(false);
            view.SetHint("Arrow keys / WASD");
            RefreshKeys();
        }
    }

    public bool Tick(float deltaTime, Keyboard keyboard)
    {
        if (index >= keys.Count)
        {
            return true;
        }

        elapsed += deltaTime;
        if (view != null) view.SetTimer(1f - elapsed / totalTime);

        Dir? pressed = ReadPressed(keyboard);
        if (pressed.HasValue)
        {
            Advance(pressed.Value == keys[index]);
        }
        else if (elapsed >= totalTime)
        {
            // หมดเวลา: ปุ่มที่เหลือนับเป็นพลาดทั้งหมด
            while (index < keys.Count) Advance(false);
        }

        if (index >= keys.Count)
        {
            Result = mistakes == 0 ? DishQuality.Perfect :
                     mistakes <= settings.sequenceMistakesForGood ? DishQuality.Good : DishQuality.Ok;
            return true;
        }
        return false;
    }

    private void Advance(bool wasCorrect)
    {
        correct.Add(wasCorrect);
        if (!wasCorrect) mistakes++;
        index++;
        RefreshKeys();
    }

    private static Dir? ReadPressed(Keyboard keyboard)
    {
        if (keyboard == null) return null;
        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) return Dir.Up;
        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) return Dir.Down;
        if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) return Dir.Left;
        if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) return Dir.Right;
        return null;
    }

    // แสดงลำดับปุ่ม: กดแล้ว = เขียว/แดง, ปุ่มปัจจุบัน = เหลืองตัวใหญ่, ที่เหลือ = ขาวจาง
    private void RefreshKeys()
    {
        if (view == null)
        {
            return;
        }

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < keys.Count; i++)
        {
            if (i > 0) sb.Append("  ");
            // <noparse> กันตัว < > ถูกอ่านเป็นแท็ก rich text
            string text = $"<noparse>{KeyText(keys[i])}</noparse>";

            if (i < correct.Count)
            {
                Color c = correct[i] ? view.CorrectKeyColor : view.WrongKeyColor;
                sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{text}</color>");
            }
            else if (i == index)
            {
                sb.Append($"<size=130%><b><color=#{ColorUtility.ToHtmlStringRGB(view.CurrentKeyColor)}>[{text}]</color></b></size>");
            }
            else
            {
                sb.Append($"<alpha=#99>{text}<alpha=#FF>");
            }
        }

        view.SetKeys(sb.ToString());
    }

    private string KeyText(Dir dir)
    {
        switch (dir)
        {
            case Dir.Up: return settings.keyUpText;
            case Dir.Down: return settings.keyDownText;
            case Dir.Left: return settings.keyLeftText;
            default: return settings.keyRightText;
        }
    }
}
