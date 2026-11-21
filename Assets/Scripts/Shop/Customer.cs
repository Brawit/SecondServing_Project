using System.Collections.Generic;
using TMPro;
using UnityEngine;

// สถานะของลูกค้า
public enum CustomerState
{
    WalkingToSeat,  // เดินจากประตูไปที่นั่ง
    Ordering,       // นั่งแล้ว กำลังเลือกเมนู
    WaitingForFood, // สั่งแล้ว รออาหาร (ผู้เล่นกด E เพื่อเสิร์ฟ)
    Eating,         // กำลังกิน
    Leaving         // เดินออกจากร้าน
}

// เหตุผลที่ลูกค้าออกจากร้าน (ใช้ในสรุปวันและชื่อเสียงขั้น 2.6)
public enum CustomerLeaveReason
{
    Finished,     // กินเสร็จ จ่ายเงินแล้ว
    NoFoodLeft,   // ไม่มีเมนูเหลือให้สั่ง
    OutOfPatience,// รอนานเกินไป
    ShopClosed    // ร้านปิดระหว่างที่ยังอยู่ (ถูกเก็บออกทันที)
}

// ลูกค้า 1 คน: เดินไปที่นั่ง → สั่งอาหารจากเมนูประจำวัน → รอ → (ผู้เล่นกด E เสิร์ฟ + จ่ายเงิน) → กิน → เดินออก
// ถ้ารอนานเกินไป หรือไม่มีเมนูเหลือ จะเดินออกเลย
// ได้อาหารแล้ว = ไม่นับเป็นลูกค้าที่ร้านต้องรอ (ร้านปิดได้ทันทีหลังเสิร์ฟคนสุดท้าย ส่วนลูกค้าจะกินจนเสร็จแล้วเดินออกทีหลัง)
// ตอนนี้การเสิร์ฟเป็นแบบชั่วคราว (กด E ที่ลูกค้า) ขั้น 2.5 จะเปลี่ยนเป็นต้องถือจานที่ทำจากเตามาเสิร์ฟ
// ต้องมี Collider2D แบบ Trigger (ให้ผู้เล่นกด E ได้) และ Rigidbody2D แบบ Kinematic บนตัวเดียวกัน
// CustomerManager เป็นคนสร้างลูกค้าและเรียก Init
public class Customer : MonoBehaviour, IInteractable
{
    [Header("Movement")]
    [Tooltip("ความเร็วเดิน (หน่วยต่อวินาที)")]
    [Min(0.1f)] [SerializeField] private float moveSpeed = 2.5f;

    [Tooltip("ตอนเดินออกจากร้าน เดินเร็วกว่าปกติกี่เท่า")]
    [Min(0.1f)] [SerializeField] private float leaveSpeedMultiplier = 1.6f;

    [Tooltip("ระยะที่ถือว่าถึงจุดแล้ว")]
    [Min(0.01f)] [SerializeField] private float arriveDistance = 0.05f;

    [Header("Timing (seconds)")]
    [Tooltip("เวลาคิดก่อนสั่ง หลังนั่งลง")]
    [Min(0f)] [SerializeField] private float thinkTime = 1.5f;

    [Tooltip("เวลารออาหารต่ำสุด")]
    [Min(1f)] [SerializeField] private float minPatience = 35f;

    [Tooltip("เวลารออาหารสูงสุด")]
    [Min(1f)] [SerializeField] private float maxPatience = 50f;

    [Tooltip("เวลากิน")]
    [Min(0f)] [SerializeField] private float eatDuration = 5f;

    [Header("References")]
    [Tooltip("ข้อความลอยเหนือหัว (TextMeshPro แบบ 3D ไม่ใช่ UI)")]
    [SerializeField] private TMP_Text bubbleText;

    [Tooltip("รูปตัวลูกค้า (ใช้สุ่มสีให้แต่ละคนต่างกัน)")]
    [SerializeField] private SpriteRenderer bodyRenderer;

    [Header("Look")]
    [Tooltip("สุ่มสีตัวลูกค้าตอนเกิด (ใช้แยกแต่ละคนระหว่างยังไม่มีสไปรต์จริง)")]
    [SerializeField] private bool randomizeColor = true;

    [Tooltip("สีข้อความตอนใกล้หมดความอดทน")]
    [SerializeField] private Color impatientColor = new Color(1f, 0.45f, 0.35f);

    [Header("Text")]
    [SerializeField] private string thinkingText = "...";
    [SerializeField] private string soldOutText = "Nothing left...";
    [SerializeField] private string impatientLeaveText = "Too slow!";
    [SerializeField] private string eatingText = "Yum!";
    [SerializeField] private string happyLeaveText = "Thanks!";
    [SerializeField] private string shopClosedText = "Closed already?";

    [Tooltip("ข้อความแจ้งเตือนตอนเสิร์ฟไม่ได้ ({0} = ชื่อเมนู)")]
    [SerializeField] private string cannotServeFormat = "Can't serve {0} right now.";

    // ---------- สถานะ ----------

    public CustomerState State { get; private set; } = CustomerState.WalkingToSeat;

    // เมนูที่สั่ง (null = ยังไม่ได้สั่ง)
    public RecipeData Order { get; private set; }

    // สั่งไปแล้วหรือยัง (ใช้ให้ CustomerManager นับลูกค้าที่ยังไม่ได้สั่ง)
    public bool HasOrdered { get; private set; }

    // ยังต้องการผู้เล่นอยู่ไหม (ยังไม่ได้อาหาร) ลูกค้าที่ได้อาหารแล้วหรือกำลังเดินออกไม่นับ
    public bool NeedsService =>
        State == CustomerState.WalkingToSeat ||
        State == CustomerState.Ordering ||
        State == CustomerState.WaitingForFood;

    // ความอดทนที่เหลือ 0–1 (ใช้แสดงผล)
    public float PatienceRatio => patienceMax > 0f ? Mathf.Clamp01(patienceLeft / patienceMax) : 0f;

    public Seat Seat { get; private set; }

    private CustomerManager manager;
    private Vector3 exitPosition;
    private Rigidbody2D rb;
    private Color bubbleBaseColor = Color.white;

    private readonly List<Vector3> path = new List<Vector3>();
    private int pathIndex;

    private float timer;
    private float patienceLeft;
    private float patienceMax;
    private CustomerLeaveReason leaveReason;
    private bool finished;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogWarning($"Customer '{name}': Needs a Collider2D (Is Trigger) so the player can serve with E.", this);
        }
        else
        {
            col.isTrigger = true;
        }

        if (bubbleText != null)
        {
            bubbleBaseColor = bubbleText.color;
        }

        if (randomizeColor && bodyRenderer != null)
        {
            bodyRenderer.color = Color.HSVToRGB(Random.value, 0.45f, 0.95f);
        }
    }

    // CustomerManager เรียกหลังสร้าง: ตั้งที่นั่ง จุดทางออก แล้วเริ่มเดิน
    public void Init(CustomerManager owner, Seat seat, Vector3 exit)
    {
        manager = owner;
        Seat = seat;
        exitPosition = exit;

        SetBubble(thinkingText);
        State = CustomerState.WalkingToSeat;
        StartPathTo(seat != null ? seat.SitPosition : transform.position);
    }

    private void Update()
    {
        switch (State)
        {
            case CustomerState.Ordering:
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    ChooseOrder();
                }
                break;

            case CustomerState.WaitingForFood:
                patienceLeft -= Time.deltaTime;
                UpdateWaitingBubble();
                if (patienceLeft <= 0f)
                {
                    GiveUp();
                }
                break;

            case CustomerState.Eating:
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    FinishMeal();
                }
                break;
        }
    }

    private void FixedUpdate()
    {
        if (State != CustomerState.WalkingToSeat && State != CustomerState.Leaving)
        {
            return;
        }

        if (pathIndex >= path.Count)
        {
            OnPathFinished();
            return;
        }

        Vector2 current = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 target = path[pathIndex];
        float speed = State == CustomerState.Leaving ? moveSpeed * leaveSpeedMultiplier : moveSpeed;
        Vector2 next = Vector2.MoveTowards(current, target, speed * Time.fixedDeltaTime);

        if (rb != null) rb.MovePosition(next);
        else transform.position = next;

        if (Vector2.Distance(next, target) <= arriveDistance)
        {
            pathIndex++;
        }
    }

    // ---------- เดิน ----------

    private void StartPathTo(Vector3 destination)
    {
        path.Clear();
        pathIndex = 0;

        bool found = GridPathfinder.Instance != null && GridPathfinder.Instance.FindPath(transform.position, destination, path);
        if (!found)
        {
            // หาเส้นทางไม่ได้: เดินตรงไปเลย (จะทะลุของ แต่เกมไม่ค้าง)
            Debug.LogWarning($"Customer '{name}': No path to {destination}. Walking straight.", this);
            path.Clear();
            path.Add(destination);
        }
    }

    private void OnPathFinished()
    {
        if (State == CustomerState.WalkingToSeat)
        {
            State = CustomerState.Ordering;
            timer = thinkTime;
            SetBubble(thinkingText);
        }
        else if (State == CustomerState.Leaving && !finished)
        {
            finished = true;
            if (manager != null)
            {
                manager.ReportLeft(this, leaveReason);
            }
            Destroy(gameObject);
        }
    }

    // ---------- สั่ง / รอ / กิน ----------

    private void ChooseOrder()
    {
        RecipeData recipe = null;
        bool picked = manager != null && manager.TryPickOrder(this, out recipe);
        HasOrdered = true;

        if (!picked || recipe == null)
        {
            SetBubble(soldOutText);
            Leave(CustomerLeaveReason.NoFoodLeft);
            return;
        }

        Order = recipe;
        patienceMax = Random.Range(Mathf.Min(minPatience, maxPatience), Mathf.Max(minPatience, maxPatience));
        patienceLeft = patienceMax;
        State = CustomerState.WaitingForFood;
        UpdateWaitingBubble();
        Debug.Log($"Customer '{name}': Ordered {recipe.recipeName} (patience {patienceMax:0}s).");
    }

    private void UpdateWaitingBubble()
    {
        if (bubbleText == null || Order == null)
        {
            return;
        }

        bubbleText.text = $"{Order.recipeName}\n{Mathf.CeilToInt(patienceLeft)}s";
        bubbleText.color = PatienceRatio < 0.3f ? impatientColor : bubbleBaseColor;
    }

    private void GiveUp()
    {
        Debug.Log($"Customer '{name}': Waited too long for {Order?.recipeName}. Leaving.");
        if (manager != null && Order != null)
        {
            manager.ReleasePending(Order);
        }

        SetBubble(impatientLeaveText);
        Leave(CustomerLeaveReason.OutOfPatience);
    }

    // เสิร์ฟอาหารให้ลูกค้าคนนี้ (ตอนนี้เรียกจากการกด E / ขั้น 2.5 จะเรียกตอนถือจานมาเสิร์ฟ) คืนค่า true ถ้าสำเร็จ
    public bool TryServe()
    {
        if (State != CustomerState.WaitingForFood || Order == null)
        {
            return false;
        }

        DailyMenu menu = DailyMenu.Instance;
        if (menu == null || !menu.TryTakeServing(Order))
        {
            NotificationUI.Post(string.Format(cannotServeFormat, Order.recipeName));
            return false;
        }

        // เปลี่ยนสถานะก่อนรายงาน ให้ระบบอื่นเห็นว่าลูกค้าคนนี้ไม่ต้องการผู้เล่นแล้ว
        State = CustomerState.Eating;

        // จ่ายเงินตอนได้อาหาร (ร้านอาจปิดไปแล้วระหว่างที่ลูกค้ายังกินอยู่ เงินต้องนับในวันนี้)
        if (manager != null)
        {
            manager.ReleasePending(Order);
            manager.ReportServed(this, Order);
            manager.ReportPaid(this, Order.sellPrice);
        }

        timer = eatDuration;
        SetBubble(eatingText);
        if (bubbleText != null) bubbleText.color = bubbleBaseColor;
        return true;
    }

    private void FinishMeal()
    {
        SetBubble(happyLeaveText);
        Leave(CustomerLeaveReason.Finished);
    }

    // ---------- ออกจากร้าน ----------

    private void Leave(CustomerLeaveReason reason)
    {
        leaveReason = reason;

        if (Seat != null)
        {
            Seat.Release(this);
        }

        State = CustomerState.Leaving;
        StartPathTo(exitPosition);
    }

    // ร้านปิดก่อนได้อาหาร (เช่น กด F8): เลิกรอแล้วเดินออก (CustomerManager เรียก)
    // ลูกค้าที่ได้อาหารแล้วไม่ต้องเรียก จะกินจนเสร็จแล้วเดินออกเอง
    public void LeaveBecauseShopClosed()
    {
        if (!NeedsService)
        {
            return;
        }

        if (State == CustomerState.WaitingForFood && manager != null && Order != null)
        {
            manager.ReleasePending(Order);
        }

        HasOrdered = true;
        SetBubble(shopClosedText);
        if (bubbleText != null) bubbleText.color = bubbleBaseColor;
        Leave(CustomerLeaveReason.ShopClosed);
    }

    // หายไปทันที (ใช้เก็บกวาดตอนเริ่มวันใหม่ถ้ายังมีลูกค้าค้างอยู่)
    public void RemoveImmediately()
    {
        if (finished)
        {
            return;
        }

        finished = true;
        if (Seat != null)
        {
            Seat.Release(this);
        }
        Destroy(gameObject);
    }

    // ---------- IInteractable ----------

    public bool CanInteract(PlayerInteractor interactor)
    {
        return State == CustomerState.WaitingForFood;
    }

    public void Interact(PlayerInteractor interactor)
    {
        TryServe();
    }

    // ---------- ภายใน ----------

    private void SetBubble(string text)
    {
        if (bubbleText != null)
        {
            bubbleText.text = text;
        }
    }
}
