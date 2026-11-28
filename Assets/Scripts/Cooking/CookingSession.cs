using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// [2.5] ตัวเล่นมินิเกมทำอาหาร 1 จาน: เรียกมินิเกมทีละขั้นตามขั้นตอนของสูตร แล้วรวมผลเป็น OK / Good / Perfect
// - ระหว่างทำ: ผู้เล่นเดินไม่ได้ (ยืนที่เตา) / เวลาร้านและความอดทนลูกค้าเดินต่อ / นาฬิกาไม่หยุด
// - Esc = ยกเลิก ไม่เสียวัตถุดิบ / ทำพลาดยังได้จาน OK
// - วัตถุดิบหักตอนทำเสร็จ
// - [v2] สูตรที่มีช่องหมวด ได้จานแบบตามวัตถุดิบที่ใส่ เช่น "Sushi (Salmon)" (DishVariantRegistry)
//   - ตอนเปิดร้าน (InShop) = จานอยู่ในมือ (CarriedDish) / ร้านปิดไประหว่างทำ = เข้ากระเป๋า
//   - นอกเวลาร้าน (Free) = เข้ากระเป๋าพร้อมคุณภาพ
// - ไม่มีหน้าต่างผลลัพธ์: แถบกระพริบสีตามผลแต่ละขั้น ~0.5 วิ แล้วไปต่อ/ปิดเอง
// วางบนวัตถุระบบ CookingSystem (ตัวช่วย Build Cooking System สร้างให้)
public class CookingSession : MonoBehaviour
{
    public static CookingSession Instance { get; private set; }

    // กำลังทำอาหารอยู่ไหม (ระบบอื่นใช้ล็อกปุ่ม)
    public static bool IsBusy => Instance != null && Instance.IsCooking;

    public enum CookMode
    {
        InShop, // ตอนเปิดร้าน: ทำเสร็จถือในมือ
        Free    // นอกเวลาร้าน: ทำเสร็จเข้ากระเป๋า
    }

    private enum Phase
    {
        Idle,
        Intro,   // แสดงชื่อท่าก่อนเริ่มขั้น
        Playing, // กำลังเล่นมินิเกม
        Flash    // แถบกระพริบสีตามผล
    }

    [Header("References")]
    [Tooltip("ค่าตั้งต้นของระบบทำอาหาร (Assets/Cooking/CookingSettings.asset)")]
    [SerializeField] private CookingSettings settings;

    [Tooltip("หน้าตามินิเกม (แผง CookingMinigamePanel บน Canvas)")]
    [SerializeField] private CookingMinigameView view;

    [Tooltip("กระเป๋าของผู้เล่น (เว้นว่าง = หาเอง)")]
    [SerializeField] private Inventory inventory;

    [Tooltip("ตัวควบคุมการเดินของผู้เล่น (เว้นว่าง = หาเอง)")]
    [SerializeField] private PlayerController playerController;

    [Header("Messages")]
    [Tooltip("ทำเสร็จนอกเวลาร้าน ({0} = ชื่อจาน, {1} = คุณภาพ)")]
    [SerializeField] private string cookedFormat = "Cooked {0} ({1})!";

    [Tooltip("วัตถุดิบไม่พอ ({0} = ชื่อจาน)")]
    [SerializeField] private string missingFormat = "Not enough ingredients for {0}.";

    [Tooltip("กระเป๋าเต็ม ({0} = ชื่อจาน)")]
    [SerializeField] private string bagFullFormat = "Bag is full. Can't cook more {0}.";

    [Tooltip("ยกเลิกการทำอาหาร")]
    [SerializeField] private string cancelledMessage = "Cooking cancelled.";

    [Tooltip("ข้อความใต้แถบตอนแสดงชื่อท่า")]
    [SerializeField] private string cancelHint = "[Esc] cancel";

    [Header("Debug")]
    [Tooltip("ข้ามมินิเกม ได้จานทันทีด้วยคุณภาพด้านล่าง (ใช้ทดสอบระบบร้านเร็วๆ)")]
    [SerializeField] private bool skipMinigames = false;

    [SerializeField] private DishQuality skipQuality = DishQuality.Good;

    public bool IsCooking => phase != Phase.Idle;
    public RecipeData CurrentRecipe { get; private set; }

    // [v2] วัตถุดิบที่ใส่ช่องหมวดของจานที่กำลังทำ (null = สูตรไม่มีช่องหมวด)
    public ItemData CurrentChoice { get; private set; }
    public CookingSettings Settings => settings;

    private CookMode mode;
    private Phase phase = Phase.Idle;
    private float phaseTimer;
    private int stepIndex;
    private ICookingMinigame current;
    private bool lockedMovement;

    private readonly List<CookingStep> steps = new List<CookingStep>();
    private readonly List<DishQuality> results = new List<DishQuality>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("CookingSession: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (inventory == null)
        {
            PlayerInteractor player = FindFirstObjectByType<PlayerInteractor>();
            if (player != null) inventory = player.GetComponent<Inventory>();
        }
        if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();
        if (view == null) view = FindFirstObjectByType<CookingMinigameView>(FindObjectsInactive.Include);

        if (settings == null) Debug.LogWarning("CookingSession: Cooking Settings is not assigned. Run Tools > Second Serving > Build Cooking System.", this);
        if (view == null) Debug.LogWarning("CookingSession: Minigame View is not assigned.", this);
        if (inventory == null) Debug.LogWarning("CookingSession: Inventory is not found.", this);
    }

    private void Start()
    {
        // สลบ/นอนระหว่างทำ (เช่น ถึง 03:00) = ยกเลิก โดยไม่ปล่อยการเดิน (DayEndManager ล็อกผู้เล่นไว้)
        if (DayEndManager.Instance != null) DayEndManager.Instance.OnDayEndStarted += HandleDayEndStarted;
    }

    private void OnDestroy()
    {
        if (DayEndManager.Instance != null) DayEndManager.Instance.OnDayEndStarted -= HandleDayEndStarted;
        if (Instance == this) Instance = null;
    }

    private void HandleDayEndStarted(DayEndReason reason)
    {
        if (!IsCooking)
        {
            return;
        }

        Debug.Log($"CookingSession: Day ended ({reason}) while cooking {CurrentRecipe?.recipeName}. Cancelled.");
        lockedMovement = false;
        EndSession();
    }

    // ---------- เริ่ม ----------

    // เริ่มทำสูตรนี้ คืนค่า false ถ้าทำไม่ได้ (แจ้งเตือนให้แล้ว)
    // [v2] choice = วัตถุดิบที่ใส่ช่องหมวด (null = ใช้ตัวตั้งต้น: ตัวที่เลือกล่าสุด หรือตัวที่มีมากสุด)
    public bool StartCooking(RecipeData recipe, CookMode cookMode, ItemData choice = null)
    {
        if (IsCooking || recipe == null || recipe.dishItem == null || inventory == null)
        {
            return false;
        }

        if (recipe.HasCategorySlot && !recipe.CategorySlot.Accepts(choice))
        {
            choice = RecipeRequirements.DefaultChoice(recipe, inventory, null);
        }
        CurrentChoice = recipe.HasCategorySlot ? choice : null;

        if (!HasIngredients(recipe))
        {
            NotificationUI.Post(string.Format(missingFormat, recipe.recipeName));
            return false;
        }

        // นอกเวลาร้านจานเข้ากระเป๋า ต้องมีที่ว่าง ([v2] นับตามจานแบบที่จะได้)
        ItemData expectedDish = DishVariantRegistry.GetOrCreate(recipe, CurrentChoice);
        if (cookMode == CookMode.Free && inventory.GetSpaceFor(expectedDish) <= 0)
        {
            NotificationUI.Post(string.Format(bagFullFormat, expectedDish != null ? expectedDish.itemName : recipe.recipeName));
            return false;
        }

        CurrentRecipe = recipe;
        mode = cookMode;
        results.Clear();

        if (settings != null)
        {
            settings.ResolveSteps(recipe, steps);
        }
        else
        {
            steps.Clear();
            steps.Add(CookingStep.Create(CookingMinigameType.Timing, ""));
        }

        // ล็อกการเดิน (ปุ่มลูกศร/WASD ใช้ในมินิเกม)
        lockedMovement = playerController != null && playerController.CanMove;
        if (lockedMovement) playerController.SetCanMove(false);

        string choiceText = CurrentChoice != null ? $", with {CurrentChoice.itemName}" : "";
        Debug.Log($"CookingSession: Cooking {recipe.recipeName} ({mode}, {steps.Count} steps, difficulty {recipe.Difficulty}{choiceText}).");

        if (skipMinigames || view == null || settings == null)
        {
            Finish(skipMinigames ? skipQuality : DishQuality.Good);
            return true;
        }

        view.Show();
        stepIndex = 0;
        BeginIntro();
        return true;
    }

    // ---------- อัปเดต ----------

    private void Update()
    {
        if (!IsCooking)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        // Esc = ยกเลิก (ไม่เสียวัตถุดิบ)
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Cancel();
            return;
        }

        float dt = Time.deltaTime;

        switch (phase)
        {
            case Phase.Intro:
                phaseTimer -= dt;
                if (phaseTimer <= 0f) BeginPlaying();
                break;

            case Phase.Playing:
                if (current != null && current.Tick(dt, keyboard))
                {
                    EndStep(current.Result);
                }
                break;

            case Phase.Flash:
                phaseTimer -= dt;
                if (phaseTimer <= 0f) NextStep();
                break;
        }
    }

    // ---------- ขั้นตอน ----------

    private void BeginIntro()
    {
        CookingStep step = steps[stepIndex];

        view.ResetParts();
        view.SetHeader($"{CurrentRecipe.recipeName}  <noparse>{settings.Stars(CurrentRecipe.Difficulty)}</noparse>  ·  {stepIndex + 1}/{steps.Count}");
        view.SetLabel(settings.GetLabel(step));
        view.SetVisual(settings.ResolveVisual(CurrentRecipe, steps, stepIndex));
        view.SetHint(cancelHint);

        phase = Phase.Intro;
        phaseTimer = settings.stepIntroDelay;
        if (phaseTimer <= 0f) BeginPlaying();
    }

    private void BeginPlaying()
    {
        CookingStep step = steps[stepIndex];
        current = CreateMinigame(step.PlayableType);
        current.Begin(step, settings, view, CurrentRecipe.Difficulty);
        phase = Phase.Playing;
    }

    // สร้างมินิเกมตามชนิด (เพิ่มมินิเกมใหม่ตรงนี้)
    private static ICookingMinigame CreateMinigame(CookingMinigameType type)
    {
        switch (type)
        {
            case CookingMinigameType.Mash: return new MashMinigame();
            case CookingMinigameType.Sequence: return new SequenceMinigame();
            case CookingMinigameType.Hold: return new HoldMinigame();
            case CookingMinigameType.Stir: return new StirMinigame();
            case CookingMinigameType.Shake: return new ShakeMinigame();
            case CookingMinigameType.Whisk: return new WhiskMinigame();
            default: return new TimingMinigame();
        }
    }

    private void EndStep(DishQuality result)
    {
        results.Add(result);
        current = null;

        // แถบกระพริบสีตามผล + ชื่อผล
        view.FlashBar(DishQualityUtil.GetColor(result));
        view.SetBarVisible(true);
        view.SetLabel($"{DishQualityUtil.Label(result)}!", DishQualityUtil.GetColor(result));

        phase = Phase.Flash;
        phaseTimer = settings.resultFlashDuration;
        if (phaseTimer <= 0f) NextStep();
    }

    private void NextStep()
    {
        stepIndex++;
        if (stepIndex < steps.Count)
        {
            BeginIntro();
            return;
        }

        Finish(settings.Combine(results));
    }

    // ---------- จบ / ยกเลิก ----------

    private void Finish(DishQuality quality)
    {
        RecipeData recipe = CurrentRecipe;
        EndSession();

        if (recipe == null)
        {
            return;
        }

        // ตรวจอีกครั้งก่อนหัก (กันของหายระหว่างทำ) [v2] ตัวที่เลือกหมด = ลองตัวอื่นในหมวด
        if (!HasIngredients(recipe) && recipe.HasCategorySlot)
        {
            CurrentChoice = RecipeRequirements.DefaultChoice(recipe, inventory, null);
        }
        if (!HasIngredients(recipe))
        {
            NotificationUI.Post(string.Format(missingFormat, recipe.recipeName));
            return;
        }

        // [v2] จานที่ได้: สูตรมีช่องหมวด = จานแบบตามวัตถุดิบที่ใส่ / ไม่มี = จานแม่
        ItemData dish = DishVariantRegistry.GetOrCreate(recipe, CurrentChoice);
        if (dish == null)
        {
            Debug.LogWarning($"CookingSession: {recipe.recipeName} has no dish item.");
            return;
        }

        ShopManager shop = ShopManager.Instance;
        bool toHand = mode == CookMode.InShop && shop != null && shop.IsServing && CarriedDish.Instance != null && !CarriedDish.Instance.HasDish;

        if (!toHand && inventory.GetSpaceFor(dish) <= 0)
        {
            NotificationUI.Post(string.Format(bagFullFormat, dish.itemName));
            return;
        }

        ConsumeIngredients(recipe);

        if (toHand)
        {
            CarriedDish.Instance.Hold(dish, quality);
        }
        else
        {
            inventory.AddItem(dish, 1, quality);
            NotificationUI.Post(string.Format(cookedFormat, dish.itemName, DishQualityUtil.Label(quality)));
        }

        string destination = toHand ? "hand" : "bag";
        Debug.Log($"CookingSession: Finished {dish.itemName} ({dish.itemId}) -> {DishQualityUtil.Label(quality)} ({destination}).");
    }

    public void Cancel()
    {
        if (!IsCooking)
        {
            return;
        }

        Debug.Log($"CookingSession: Cancelled {CurrentRecipe?.recipeName}.");
        EndSession();
        NotificationUI.Post(cancelledMessage);
    }

    private void EndSession()
    {
        phase = Phase.Idle;
        current = null;
        CurrentRecipe = null;

        if (view != null) view.Hide();

        if (lockedMovement && playerController != null)
        {
            playerController.SetCanMove(true);
        }
        lockedMovement = false;
    }

    // ---------- วัตถุดิบ ----------

    private bool HasIngredients(RecipeData recipe)
    {
        return RecipeRequirements.HasAll(recipe, inventory, CurrentChoice);
    }

    private void ConsumeIngredients(RecipeData recipe)
    {
        foreach (KeyValuePair<ItemData, int> pair in RecipeRequirements.Resolve(recipe, CurrentChoice))
        {
            inventory.RemoveItem(pair.Key, pair.Value);
        }
    }
}
