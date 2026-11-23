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
    Leaving,        // เดินออกจากร้าน
    Asking          // [2.4b] ถามหาเมนูนิยมที่ร้านไม่มี รอผู้เล่นกด E แนะนำเมนูวันนี้ (เพิ่มต่อท้ายเท่านั้น)
}

// เหตุผลที่ลูกค้าออกจากร้าน (ใช้ในสรุปวันและชื่อเสียงขั้น 2.6)
public enum CustomerLeaveReason
{
    Finished,     // กินเสร็จ จ่ายเงินแล้ว
    NoFoodLeft,   // ไม่มีเมนูเหลือให้สั่ง
    OutOfPatience,// รอนานเกินไป
    ShopClosed    // ร้านปิดระหว่างที่ยังอยู่ (ถูกเก็บออกทันที)
}

// [2.4b] ลูกค้าสั่งอาหารแบบไหน (CustomerManager.TryPickOrder คืนค่านี้ ใช้คิดความพอใจ)
public enum CustomerOrderKind
{
    None,       // ไม่มีอะไรเหลือให้สั่ง
    Normal,     // สั่งจานปกติ
    Popular,    // ได้สั่งเมนูนิยมของวันนี้
    Substitute, // ถามหาเมนูนิยมที่ไม่มี แล้วเปลี่ยนมากินเมนูวันนี้ตามที่ผู้เล่นแนะนำ
    Asking      // ถามหาเมนูนิยมที่ไม่มี (ยังไม่ได้สั่ง รอผู้เล่นแนะนำ)
}

// [2.4b] อารมณ์หลังกินเสร็จ (ใช้คิดความพอใจ)
public enum CustomerMood
{
    None,  // ยังไม่ได้กิน
    Loved, // ชอบมาก
    Good,  // อร่อยดี
    Meh    // เฉยๆ
}

// ลูกค้า 1 คน: เดินไปที่นั่ง → สั่งอาหารจากเมนูประจำวัน → รอ → (ผู้เล่นกด E เสิร์ฟ + จ่ายเงิน) → กิน → เดินออก
// [2.5] กด E ตอนมีจานที่สั่งในกระเป๋ามากกว่า 1 คุณภาพ = เปิดแถบเลือกคุณภาพ (ServePicker) เหนือหัว
// [2.5 รอบ 2] ถือจานที่ลูกค้าสั่ง + E = เสิร์ฟจานในมือ / ถือจานอื่น = แจ้งเตือน / ไม่มีจานเลย = บอกให้ไปทำที่เตา
// [2.4b] บางคนถามหาเมนูนิยมที่ร้านไม่มี → ผู้เล่นกด E แนะนำเมนูวันนี้ → ลูกค้าเปลี่ยนจาน → รอ → เสิร์ฟ → กิน → อารมณ์หลังกิน
// ถ้ารอนานเกินไป หรือไม่มีเมนูเหลือ จะเดินออกเลย
// ได้อาหารแล้ว = ไม่นับเป็นลูกค้าที่ร้านต้องรอ (ร้านปิดได้ทันทีหลังเสิร์ฟคนสุดท้าย ส่วนลูกค้าจะกินจนเสร็จแล้วเดินออกทีหลัง)
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

    [Tooltip("ข้อความตอนได้กินเมนูนิยม")]
    [SerializeField] private string popularEatingText = "So good!!";

    [Tooltip("ลูกค้าถามหาเมนูนิยม ({0} = ชื่อเมนู หรือคำใบ้ถ้ายังไม่รู้สูตร)")]
    [SerializeField] private string askFormat = "Got {0}?";

    [Tooltip("ข้อความของผู้เล่นตอนแนะนำเมนูวันนี้ (แสดงที่แจ้งเตือน) {0} = ชื่อเมนูที่ลูกค้าถามหา")]
    [SerializeField] private string recommendFormat = "Sorry, no {0} today. Try our menu, it's tasty!";

    [Tooltip("ลูกค้าตอบตกลงเปลี่ยนจาน ({0} = จานใหม่)")]
    [SerializeField] private string acceptFormat = "OK, I'll try {0}!";

    [Tooltip("แสดงข้อความตอบตกลงกี่วินาที ก่อนกลับไปแสดงชื่อจานและเวลา")]
    [Min(0f)] [SerializeField] private float acceptTextDuration = 1.5f;

    [Header("Mood text (after eating)")]
    [SerializeField] private string lovedLeaveText = "Loved it!!";
    [SerializeField] private string goodLeaveText = "Tasty, thanks!";
    [SerializeField] private string mehLeaveText = "It was okay.";

    [Tooltip("ข้อความแจ้งเตือนตอนเสิร์ฟไม่ได้ ({0} = ชื่อเมนู)")]
    [SerializeField] private string cannotServeFormat = "Can't serve {0} right now.";

    [Tooltip("[2.5] ถือจานที่ลูกค้าไม่ได้สั่ง ({0} = จานที่ลูกค้าสั่ง, {1} = จานในมือ)")]
    [SerializeField] private string wrongDishFormat = "This customer ordered {0}, not {1}.";

    [Tooltip("[2.5] ไม่มีจานนี้ทั้งในมือและในกระเป๋า ({0} = ชื่อเมนู)")]
    [SerializeField] private string cookFirstFormat = "Cook {0} at the stove first.";

    // ---------- สถานะ ----------

    public CustomerState State { get; private set; } = CustomerState.WalkingToSeat;

    // เมนูที่สั่ง (null = ยังไม่ได้สั่ง)
    public RecipeData Order { get; private set; }

    // [2.4b] สั่งแบบไหน (ปกติ / เมนูนิยม / จำใจสั่งจานแทน)
    public CustomerOrderKind OrderKind { get; private set; } = CustomerOrderKind.None;

    // [2.4b] ความพอใจของลูกค้าคนนี้ (ตั้งตอนได้อาหาร / ไม่ได้กิน = 0) ขั้น 2.6 ใช้คิดเงินและชื่อเสียง
    public float Satisfaction { get; private set; }

    // [2.4b] อารมณ์หลังกิน (สุ่มตอนได้อาหาร แสดงตอนกินเสร็จ)
    public CustomerMood Mood { get; private set; } = CustomerMood.None;

    // [2.4b] เมนูนิยมที่ถามหา (เฉพาะคนที่ถามหา)
    public RecipeData AskedFor { get; private set; }

    // [2.5] คุณภาพจานที่ได้ (ตั้งตอนเสิร์ฟ)
    public DishQuality ServedQuality { get; private set; } = DishQuality.Good;

    // สั่งไปแล้วหรือยัง (ใช้ให้ CustomerManager นับลูกค้าที่ยังไม่ได้สั่ง)
    public bool HasOrdered { get; private set; }

    // ยังต้องการผู้เล่นอยู่ไหม (ยังไม่ได้อาหาร) ลูกค้าที่ได้อาหารแล้วหรือกำลังเดินออกไม่นับ
    public bool NeedsService =>
        State == CustomerState.WalkingToSeat ||
        State == CustomerState.Ordering ||
        State == CustomerState.Asking ||
        State == CustomerState.WaitingForFood;

    // ความอดทนที่เหลือ 0–1 (ใช้แสดงผล)
    public float PatienceRatio => patienceMax > 0f ? Mathf.Clamp01(patienceLeft / patienceMax) : 0f;

    public Seat Seat { get; private set; }

    // [2.5] ข้อความเหนือหัว (ServePicker ใช้หาว่าคลิกโดนตัวเลือกไหน)
    public TMP_Text BubbleText => bubbleText;

    private CustomerManager manager;
    private Vector3 exitPosition;
    private Rigidbody2D rb;
    private Color bubbleBaseColor = Color.white;

    private readonly List<Vector3> path = new List<Vector3>();
    private int pathIndex;

    private float timer;
    private float patienceLeft;
    private float patienceMax;
    private float acceptTextTimer;
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

            case CustomerState.Asking:
                patienceLeft -= Time.deltaTime;
                UpdateAskingBubble();
                if (patienceLeft <= 0f)
                {
                    GiveUp();
                }
                break;

            case CustomerState.WaitingForFood:
                patienceLeft -= Time.deltaTime;
                if (acceptTextTimer > 0f) acceptTextTimer -= Time.deltaTime;
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
        RecipeData wanted = null;
        CustomerOrderKind kind = manager != null ? manager.TryPickOrder(this, out recipe, out wanted) : CustomerOrderKind.None;

        // ถามหาเมนูนิยมที่ร้านไม่มี: ยังไม่นับว่าสั่ง รอผู้เล่นกด E (ความอดทนเริ่มนับตั้งแต่ตอนนี้)
        if (kind == CustomerOrderKind.Asking && wanted != null)
        {
            OrderKind = kind;
            AskedFor = wanted;
            StartPatience();
            State = CustomerState.Asking;
            UpdateAskingBubble();
            Debug.Log($"Customer '{name}': Asking for {wanted.recipeName} (patience {patienceMax:0}s).");
            return;
        }

        HasOrdered = true;

        if (kind == CustomerOrderKind.None || recipe == null)
        {
            OrderKind = CustomerOrderKind.None;
            SetBubble(soldOutText);
            Leave(CustomerLeaveReason.NoFoodLeft);
            return;
        }

        StartWaiting(recipe, kind);
        Debug.Log($"Customer '{name}': Ordered {recipe.recipeName} ({kind}, patience {patienceMax:0}s).");
    }

    // [2.4b] ผู้เล่นกด E ที่ลูกค้าที่ถามหาเมนูนิยม: ขอโทษแล้วแนะนำเมนูวันนี้ ลูกค้าเปลี่ยนจาน คืนค่า true ถ้าสำเร็จ
    public bool RecommendTodaysMenu()
    {
        if (State != CustomerState.Asking)
        {
            return false;
        }

        string askedName = GetAskedName();
        NotificationUI.Post(string.Format(recommendFormat, askedName));

        RecipeData recipe = null;
        CustomerOrderKind kind = manager != null ? manager.TryPickSubstitute(this, out recipe) : CustomerOrderKind.None;
        HasOrdered = true;

        if (kind == CustomerOrderKind.None || recipe == null)
        {
            // ระหว่างรอ เมนูอื่นหมดไปแล้ว
            OrderKind = CustomerOrderKind.None;
            SetBubble(soldOutText);
            Leave(CustomerLeaveReason.NoFoodLeft);
            return false;
        }

        StartWaiting(recipe, kind);
        acceptTextTimer = acceptTextDuration;
        SetBubble(string.Format(acceptFormat, recipe.recipeName));
        Debug.Log($"Customer '{name}': Asked for {AskedFor?.recipeName}, switched to {recipe.recipeName} ({patienceLeft:0}s patience left).");
        return true;
    }

    // เริ่มนับความอดทน (ครั้งเดียวต่อคน ถามหาแล้วเปลี่ยนจาน = นับต่อจากเดิม ไม่รีเซ็ต)
    private void StartPatience()
    {
        if (patienceMax > 0f)
        {
            return;
        }

        patienceMax = Random.Range(Mathf.Min(minPatience, maxPatience), Mathf.Max(minPatience, maxPatience));
        patienceLeft = patienceMax;
    }

    private void StartWaiting(RecipeData recipe, CustomerOrderKind kind)
    {
        Order = recipe;
        OrderKind = kind;
        StartPatience();
        State = CustomerState.WaitingForFood;
        UpdateWaitingBubble();
    }

    // ชื่อเมนูที่ถามหา: รู้สูตรแล้ว = ชื่อจริง / ยังไม่รู้ = คำใบ้ (เช่น "that Japanese dessert")
    private string GetAskedName()
    {
        if (AskedFor == null)
        {
            return "it";
        }

        return PopularDishes.Instance != null ? PopularDishes.Instance.GetRequestName(AskedFor) : AskedFor.recipeName;
    }

    private void UpdateAskingBubble()
    {
        if (bubbleText == null)
        {
            return;
        }

        bubbleText.text = $"{string.Format(askFormat, GetAskedName())}\n[E] {Mathf.CeilToInt(patienceLeft)}s";
        bubbleText.color = PatienceRatio < 0.3f ? impatientColor : bubbleBaseColor;
    }

    private void UpdateWaitingBubble()
    {
        if (bubbleText == null || Order == null)
        {
            return;
        }

        // ยังแสดงข้อความตอบตกลงเปลี่ยนจานอยู่
        if (acceptTextTimer > 0f)
        {
            bubbleText.color = bubbleBaseColor;
            return;
        }

        // เมนูนิยมมีเครื่องหมายนำหน้า ให้ผู้เล่นเห็นว่าคนนี้จะพอใจมากถ้าได้กิน
        string dishName = Order.recipeName;
        if (OrderKind == CustomerOrderKind.Popular)
        {
            string marker = PopularDishes.Instance != null ? PopularDishes.Instance.Marker : "*";
            dishName = $"{marker} {dishName}";
        }

        string text = $"{dishName}\n{Mathf.CeilToInt(patienceLeft)}s";

        // [2.5] แถบเลือกคุณภาพเปิดอยู่สำหรับลูกค้าคนนี้
        ServePicker picker = ServePicker.Instance;
        if (picker != null && picker.IsOpenFor(this))
        {
            text += "\n" + picker.GetLine(this);
        }

        bubbleText.text = text;
        bubbleText.color = PatienceRatio < 0.3f ? impatientColor : bubbleBaseColor;
    }

    // [2.5] วาดข้อความเหนือหัวใหม่ (ServePicker เรียกตอนเปิด/ปิด/เลื่อนตัวเลือก)
    public void RefreshBubble()
    {
        if (State == CustomerState.WaitingForFood)
        {
            UpdateWaitingBubble();
        }
    }

    private void GiveUp()
    {
        Debug.Log($"Customer '{name}': Waited too long for {(Order != null ? Order.recipeName : AskedFor?.recipeName)}. Leaving.");
        if (manager != null && Order != null)
        {
            manager.ReleasePending(Order);
        }

        SetBubble(impatientLeaveText);
        Leave(CustomerLeaveReason.OutOfPatience);
    }

    // เสิร์ฟจานจากกระเป๋าให้ลูกค้าคนนี้ ใช้จานคุณภาพดีที่สุดที่มี คืนค่า true ถ้าสำเร็จ
    public bool TryServe()
    {
        return TryServe(null);
    }

    // [2.5] เสิร์ฟจานจากกระเป๋าด้วยคุณภาพที่เลือก (null = ดีที่สุดที่มี)
    public bool TryServe(DishQuality? quality)
    {
        if (State != CustomerState.WaitingForFood || Order == null)
        {
            return false;
        }

        DailyMenu menu = DailyMenu.Instance;
        if (menu == null || !menu.TryTakeServing(Order, quality, out DishQuality served))
        {
            NotificationUI.Post(string.Format(cannotServeFormat, Order.recipeName));
            return false;
        }

        ApplyServed(served);
        return true;
    }

    // [2.5 รอบ 2] เสิร์ฟจานที่ผู้เล่นถืออยู่ (ต้องเป็นจานที่ลูกค้าสั่ง) คืนค่า true ถ้าสำเร็จ
    public bool TryServeFromHand(CarriedDish hand)
    {
        if (State != CustomerState.WaitingForFood || Order == null || hand == null || !hand.IsHolding(Order))
        {
            return false;
        }

        DailyMenu menu = DailyMenu.Instance;
        DishQuality quality = hand.Quality;
        if (menu == null || !menu.TryTakeServingFromHand(Order, quality))
        {
            NotificationUI.Post(string.Format(cannotServeFormat, Order.recipeName));
            return false;
        }

        hand.Clear();
        ApplyServed(quality);
        return true;
    }

    // ได้อาหารแล้ว: ปิดแถบเลือก สุ่มอารมณ์ จ่ายเงิน เริ่มกิน
    private void ApplyServed(DishQuality served)
    {
        ServedQuality = served;
        if (ServePicker.Instance != null && ServePicker.Instance.IsOpenFor(this))
        {
            ServePicker.Instance.Close();
        }

        // เปลี่ยนสถานะก่อนรายงาน ให้ระบบอื่นเห็นว่าลูกค้าคนนี้ไม่ต้องการผู้เล่นแล้ว
        State = CustomerState.Eating;

        // สุ่มอารมณ์หลังกินและตั้งความพอใจก่อนรายงาน ให้ CustomerManager นับรวมได้ (แสดงอารมณ์ตอนกินเสร็จ)
        Mood = manager != null ? manager.DecideMood(OrderKind, served) : CustomerMood.Good;
        Satisfaction = manager != null ? manager.GetSatisfaction(Mood) : 1f;

        // [2.5b] จานยาก (★) ลูกค้าพอใจมากขึ้น (ตัวคูณใน CookingSettings)
        CookingSettings cooking = CookingSession.Instance != null ? CookingSession.Instance.Settings : null;
        if (cooking != null && Order != null)
        {
            Satisfaction *= cooking.SatisfactionScale(Order.Difficulty);
        }
        acceptTextTimer = 0f;

        // จ่ายเงินตอนได้อาหาร (ร้านอาจปิดไปแล้วระหว่างที่ลูกค้ายังกินอยู่ เงินต้องนับในวันนี้)
        if (manager != null)
        {
            manager.ReleasePending(Order);
            manager.ReportServed(this, Order);
            manager.ReportPaid(this, Order.sellPrice);
        }

        timer = eatDuration;
        SetBubble(OrderKind == CustomerOrderKind.Popular ? popularEatingText : eatingText);
        if (bubbleText != null) bubbleText.color = bubbleBaseColor;
    }

    private void FinishMeal()
    {
        switch (Mood)
        {
            case CustomerMood.Loved: SetBubble(lovedLeaveText); break;
            case CustomerMood.Meh: SetBubble(mehLeaveText); break;
            case CustomerMood.Good: SetBubble(goodLeaveText); break;
            default: SetBubble(happyLeaveText); break;
        }
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
        return State == CustomerState.WaitingForFood || State == CustomerState.Asking;
    }

    public void Interact(PlayerInteractor interactor)
    {
        // ถามหาเมนูนิยม = แนะนำเมนูวันนี้ / รออาหาร = เสิร์ฟ
        if (State == CustomerState.Asking)
        {
            RecommendTodaysMenu();
        }
        else
        {
            HandleServeInteract(interactor);
        }
    }

    // [2.5] กด E ที่ลูกค้าที่รออาหาร:
    // แถบเลือกเปิดอยู่ = เสิร์ฟตัวที่เลือก / [รอบ 2] ถือจานตรง = เสิร์ฟจานในมือ / ถือจานอื่น = แจ้งเตือน
    // มือว่าง: มีจานในกระเป๋า 2 คุณภาพขึ้นไป = เปิดแถบเลือก / คุณภาพเดียว = เสิร์ฟเลย / ไม่มี = บอกให้ไปทำที่เตา
    private void HandleServeInteract(PlayerInteractor interactor)
    {
        ServePicker picker = ServePicker.Instance;
        if (picker != null && picker.IsOpenFor(this))
        {
            picker.Confirm();
            return;
        }

        CarriedDish hand = CarriedDish.Instance;
        if (hand != null && hand.HasDish && Order != null)
        {
            if (hand.IsHolding(Order))
            {
                TryServeFromHand(hand);
            }
            else
            {
                NotificationUI.Post(string.Format(wrongDishFormat, Order.recipeName, hand.Recipe.recipeName));
            }
            return;
        }

        Inventory bag = interactor != null && interactor.Inventory != null ? interactor.Inventory :
                        DailyMenu.Instance != null ? DailyMenu.Instance.Inventory : null;

        if (bag != null && Order != null && Order.dishItem != null && interactor != null)
        {
            List<DishQuality> qualities = new List<DishQuality>();
            if (bag.GetAvailableQualities(Order.dishItem, qualities) >= 2)
            {
                // แถบเลือกของเตาใช้ scroll เหมือนกัน ปิดก่อน
                Stove.CloseAnyPicker();
                ServePicker.GetOrCreate().Open(this, interactor.transform, bag);
                return;
            }

            if (qualities.Count == 1)
            {
                TryServe(qualities[0]);
                return;
            }
        }

        // [2.5 รอบ 2] ไม่มีจานนี้เลย: ต้องไปทำที่เตาก่อน (เลิกทางลัดหักวัตถุดิบแล้ว)
        if (Order != null)
        {
            NotificationUI.Post(string.Format(cookFirstFormat, Order.recipeName));
        }
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
