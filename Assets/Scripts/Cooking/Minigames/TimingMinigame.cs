using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// มินิเกม Timing: เข็มวิ่งไป-กลับบนแถบ กด Space ให้หยุดในโซน (ผัด ทอด อบ พลิก)
// - กดในโซน Perfect = 2 / ในโซน Good = 1 / นอกโซนหรือไม่กดในเวลา = 0
// - hits > 1 = กดหลายจังหวะ โซนย้ายที่ทุกครั้ง / movingZone = โซนเลื่อนไปมา
// - ผลของขั้น = คะแนนเฉลี่ยทุกครั้ง (≥ 1.5 Perfect / ≥ 0.5 Good / ต่ำกว่า OK)
public class TimingMinigame : ICookingMinigame
{
    private CookingSettings settings;
    private CookingMinigameView view;

    private float zoneWidth;
    private float perfectWidth;
    private float needleSpeed;
    private float timeLimit;
    private int totalHits;
    private bool movingZone;
    private float zoneMoveSpeed;

    private float needlePhase;
    private float zoneBaseCenter;
    private float zoneCenter;
    private float zonePhase;
    private float hitTimer;
    private readonly List<int> scores = new List<int>();

    public DishQuality Result { get; private set; } = DishQuality.Ok;

    public void Begin(CookingStep step, CookingSettings cookingSettings, CookingMinigameView minigameView, int difficulty)
    {
        settings = cookingSettings;
        view = minigameView;

        zoneWidth = settings.ZoneWidth(step, difficulty);
        perfectWidth = settings.PerfectWidth(step, difficulty);
        needleSpeed = settings.NeedleSpeed(step, difficulty);
        timeLimit = settings.TimingTimeLimit(step, difficulty);
        totalHits = settings.Hits(step);
        movingZone = step != null && step.movingZone;
        zoneMoveSpeed = settings.ZoneMoveSpeed(difficulty);

        scores.Clear();
        Result = DishQuality.Ok;

        // เข็มเริ่มจากจุดสุ่ม ไม่ให้จำจังหวะได้ง่าย
        needlePhase = Random.value;
        PlaceZone();

        if (view != null)
        {
            view.SetHint(totalHits > 1 ? $"[Space] in the zone  (0/{totalHits})" : "[Space] in the zone");
        }
    }

    public bool Tick(float deltaTime, Keyboard keyboard)
    {
        needlePhase += deltaTime * needleSpeed;
        float needle = Mathf.PingPong(needlePhase * 2f, 1f);

        // โซนเลื่อน: แกว่งรอบจุดกลางเดิม ไม่ออกนอกแถบ
        if (movingZone)
        {
            zonePhase += deltaTime * zoneMoveSpeed;
            float half = zoneWidth * 0.5f;
            float range = Mathf.Min(0.25f, Mathf.Min(zoneBaseCenter - half, 1f - half - zoneBaseCenter));
            zoneCenter = zoneBaseCenter + Mathf.Sin(zonePhase * Mathf.PI * 2f) * Mathf.Max(0f, range);
        }

        hitTimer += deltaTime;

        if (view != null)
        {
            view.SetZone(zoneCenter, zoneWidth, perfectWidth);
            view.SetNeedle(needle);
            view.SetTimer(1f - hitTimer / timeLimit);
        }

        bool pressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        if (pressed)
        {
            float distance = Mathf.Abs(needle - zoneCenter);
            int score = distance <= perfectWidth * 0.5f ? 2 : distance <= zoneWidth * 0.5f ? 1 : 0;
            RegisterHit(score);
        }
        else if (hitTimer >= timeLimit)
        {
            // ไม่กดในเวลา = พลาด
            RegisterHit(0);
        }

        if (scores.Count >= totalHits)
        {
            Result = Average();
            return true;
        }
        return false;
    }

    private void RegisterHit(int score)
    {
        scores.Add(score);

        if (view != null)
        {
            DishQuality q = score >= 2 ? DishQuality.Perfect : score == 1 ? DishQuality.Good : DishQuality.Ok;
            view.PulseNeedle(score == 0 ? view.WrongKeyColor : DishQualityUtil.GetColor(q));
            if (totalHits > 1) view.SetHint($"[Space] in the zone  ({scores.Count}/{totalHits})");
        }

        // จังหวะถัดไป: ย้ายโซน
        if (scores.Count < totalHits)
        {
            PlaceZone();
        }
    }

    private void PlaceZone()
    {
        float half = zoneWidth * 0.5f;
        zoneBaseCenter = Random.Range(Mathf.Min(0.5f, half + 0.05f), Mathf.Max(0.5f, 1f - half - 0.05f));
        zoneCenter = zoneBaseCenter;
        zonePhase = 0f;
        hitTimer = 0f;
    }

    private DishQuality Average()
    {
        if (scores.Count == 0)
        {
            return DishQuality.Ok;
        }

        float total = 0f;
        foreach (int s in scores) total += s;
        float average = total / scores.Count;

        if (average >= 1.5f) return DishQuality.Perfect;
        if (average >= 0.5f) return DishQuality.Good;
        return DishQuality.Ok;
    }
}
