using System;
using System.Collections.Generic;
using UnityEngine;

// เมนูนิยมรายวัน: สุ่มจานที่ลูกค้าอยากกินเป็นพิเศษในวันนั้น
// - จำนวนต่อวัน = baseCount + โบนัส (อัปเกรดร้านในอนาคตเรียก SetBonusCount)
// - ส่วนใหญ่สุ่มจากสูตรที่ผู้เล่นรู้แล้ว บางจานเป็นสูตรที่ยังไม่รู้ (ไม่รวมสูตรลับ) เพื่อให้มีเหตุผลไปหาสูตรใหม่
// - สูตรที่ยังไม่รู้แสดงเป็นคำใบ้ (GetDisplayName) เฟส 3 จะเปลี่ยนเป็นคำใบ้ผ่าน NPC / ข่าวลือ / เนื้อเรื่อง
// - ไม่เซฟ: โหลดเซฟ = วันใหม่ สุ่มใหม่
// - สุ่มตอนเริ่มวัน และสุ่มเองตอนถูกเรียกใช้ครั้งแรกของวัน (กันลำดับการทำงานของสคริปต์ตอนเปิดเกม)
// ใส่คอมโพเนนต์นี้บนวัตถุ DailyMenu (เมนู Tools > Second Serving > Build Daily Menu Window ใส่ให้)
public class PopularDishes : MonoBehaviour
{
    public static PopularDishes Instance { get; private set; }

    [Header("References")]
    [Tooltip("เมนูประจำวัน (เว้นว่างไว้ = หาบนวัตถุเดียวกันหรือในฉาก) ใช้หาสมุดสูตรของผู้เล่น")]
    [SerializeField] private DailyMenu dailyMenu;

    [Header("Popular Dishes")]
    [Tooltip("จำนวนเมนูนิยมต่อวันตั้งต้น (บวกโบนัสจากการอัปเกรดได้)")]
    [Min(0)] [SerializeField] private int baseCount = 2;

    [Tooltip("โอกาสที่เมนูนิยมแต่ละจานจะเป็นสูตรที่ผู้เล่นยังไม่รู้ (0–1)")]
    [Range(0f, 1f)] [SerializeField] private float unknownRecipeChance = 0.25f;

    [Tooltip("ให้สูตรลับที่ผู้เล่นรู้แล้วเป็นเมนูนิยมได้ (สูตรลับที่ยังไม่รู้ไม่ถูกสุ่มเสมอ)")]
    [SerializeField] private bool allowKnownSecretRecipes = true;

    [Tooltip("พยายามไม่ให้ซ้ำกับเมนูนิยมของเมื่อวาน (ถ้าสูตรพอ)")]
    [SerializeField] private bool avoidYesterdaysDishes = true;

    [Header("Text")]
    [Tooltip("เครื่องหมายหน้าชื่อเมนูนิยม (ฟอนต์ TMP ตั้งต้นอาจไม่มีตัว ★ จึงใช้ * ไปก่อน)")]
    [SerializeField] private string popularMarker = "*";

    [Tooltip("คำใบ้ของสูตรที่ยังไม่รู้ ({0} = สัญชาติ เช่น Japanese, {1} = ประเภท เช่น dessert)")]
    [SerializeField] private string unknownHintFormat = "??? ({0} {1})";

    [Tooltip("คำที่ลูกค้าใช้ถามหาสูตรที่ผู้เล่นยังไม่รู้ ({0} = สัญชาติ, {1} = ประเภท) เช่น \"Got that Japanese dessert?\"")]
    [SerializeField] private string unknownRequestFormat = "that {0} {1}";

    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

    private readonly List<RecipeData> today = new List<RecipeData>();
    private readonly HashSet<RecipeData> yesterday = new HashSet<RecipeData>();

    // วันที่สุ่มไว้ล่าสุด (-1 = ยังไม่สุ่ม)
    private int rolledDay = -1;
    private int bonusCount;
    private bool isSubscribed;

    // ---------- Events ----------

    // ส่งสัญญาณเมื่อสุ่มเมนูนิยมใหม่
    public event Action OnPopularChanged;

    // ---------- ข้อมูลให้ระบบอื่นอ่าน ----------

    public int Count => Mathf.Max(0, baseCount + bonusCount);

    // เมนูนิยมของวันนี้ (สุ่มให้ถ้ายังไม่ได้สุ่ม)
    public IReadOnlyList<RecipeData> Today
    {
        get
        {
            EnsureRolled();
            return today;
        }
    }

    private RecipeBook Book => dailyMenu != null ? dailyMenu.RecipeBook : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("PopularDishes: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dailyMenu == null) dailyMenu = GetComponent<DailyMenu>();
        if (dailyMenu == null) dailyMenu = FindFirstObjectByType<DailyMenu>(FindObjectsInactive.Include);
        if (dailyMenu == null) Debug.LogWarning("PopularDishes: Daily Menu not found. Popular dishes can't be picked.", this);
    }

    private void Start()
    {
        Subscribe();
    }

    private void OnEnable()
    {
        if (Instance == this && GameClock.Instance != null)
        {
            Subscribe();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Subscribe()
    {
        if (isSubscribed || GameClock.Instance == null)
        {
            return;
        }

        GameClock.Instance.OnDayStarted += HandleDayStarted;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed)
        {
            return;
        }

        if (GameClock.Instance != null)
        {
            GameClock.Instance.OnDayStarted -= HandleDayStarted;
        }
        isSubscribed = false;
    }

    private void HandleDayStarted(int day)
    {
        Roll(day);
    }

    // ---------- คำถามจากระบบอื่น ----------

    // จานนี้เป็นเมนูนิยมของวันนี้ไหม
    public bool IsPopular(RecipeData recipe)
    {
        if (recipe == null)
        {
            return false;
        }

        EnsureRolled();
        return today.Contains(recipe);
    }

    // ผู้เล่นรู้สูตรนี้แล้วหรือยัง
    public bool IsKnown(RecipeData recipe)
    {
        return Book != null && Book.Knows(recipe);
    }

    // ชื่อที่แสดงในเกม: รู้สูตรแล้ว = ชื่อจริง / ยังไม่รู้ = คำใบ้
    // เฟส 3: เปลี่ยนตรงนี้ให้ใช้คำใบ้จากข่าวลือ / NPC / เนื้อเรื่อง แทนคำใบ้ประเทศ + ประเภท
    public string GetDisplayName(RecipeData recipe)
    {
        if (recipe == null)
        {
            return "";
        }

        if (IsKnown(recipe))
        {
            return recipe.recipeName;
        }

        return string.Format(unknownHintFormat, CuisineAdjective(recipe.cuisine), CourseWord(recipe.course));
    }

    // ชื่อที่ลูกค้าใช้ถามหา: รู้สูตรแล้ว = ชื่อจริง / ยังไม่รู้ = คำใบ้ เช่น "that Japanese dessert"
    // เฟส 3: เปลี่ยนตรงนี้ได้ เช่น ให้ลูกค้าพูดถึงข่าวลือหรือ NPC ที่รู้สูตร
    public string GetRequestName(RecipeData recipe)
    {
        if (recipe == null)
        {
            return "";
        }

        if (IsKnown(recipe))
        {
            return recipe.recipeName;
        }

        return string.Format(unknownRequestFormat, CuisineAdjective(recipe.cuisine), CourseWord(recipe.course));
    }

    public string Marker => popularMarker;

    // ข้อความรวมเมนูนิยมวันนี้ เช่น "* Som Tam   * ??? (Japanese dessert)" (ว่าง = ไม่มีเมนูนิยม)
    public string GetSummaryText(string separator = "   ")
    {
        EnsureRolled();
        List<string> parts = new List<string>();
        foreach (RecipeData recipe in today)
        {
            if (recipe != null)
            {
                parts.Add($"{popularMarker} {GetDisplayName(recipe)}");
            }
        }
        return string.Join(separator, parts);
    }

    // ---------- อัปเกรด ----------

    // ตั้งจำนวนเมนูนิยมที่ได้เพิ่ม (อัปเกรดร้าน) มีผลตั้งแต่วันถัดไป หรือเรียก Reroll เพื่อสุ่มใหม่ทันที
    public void SetBonusCount(int bonus)
    {
        bonusCount = Mathf.Max(0, bonus);
    }

    // สุ่มเมนูนิยมของวันนี้ใหม่ทันที
    public void Reroll()
    {
        int day = GameClock.Instance != null ? GameClock.Instance.Day : Mathf.Max(1, rolledDay);
        Roll(day);
    }

    // ---------- การสุ่ม ----------

    private void EnsureRolled()
    {
        int day = GameClock.Instance != null ? GameClock.Instance.Day : 1;
        if (rolledDay != day)
        {
            Roll(day);
        }
    }

    private void Roll(int day)
    {
        // จำของเมื่อวานไว้ (ถ้าวันเปลี่ยน) เพื่อไม่ให้ซ้ำ
        if (rolledDay != day && today.Count > 0)
        {
            yesterday.Clear();
            foreach (RecipeData recipe in today)
            {
                if (recipe != null) yesterday.Add(recipe);
            }
        }

        today.Clear();
        rolledDay = day;

        RecipeBook book = Book;
        RecipeDatabase database = book != null ? book.Database : null;
        if (database == null)
        {
            Debug.LogWarning("PopularDishes: No Recipe Database (check DailyMenu > Recipe Book > Recipe Database).", this);
            OnPopularChanged?.Invoke();
            return;
        }

        // แยกสูตรเป็น 2 กอง: รู้แล้ว / ยังไม่รู้ (ไม่รวมสูตรลับ)
        List<RecipeData> knownPool = new List<RecipeData>();
        List<RecipeData> unknownPool = new List<RecipeData>();
        foreach (RecipeData recipe in database.Recipes)
        {
            if (recipe == null)
            {
                continue;
            }

            if (book.Knows(recipe))
            {
                if (!recipe.isSecret || allowKnownSecretRecipes) knownPool.Add(recipe);
            }
            else if (!recipe.isSecret)
            {
                unknownPool.Add(recipe);
            }
        }

        for (int i = 0; i < Count; i++)
        {
            bool wantUnknown = UnityEngine.Random.value < unknownRecipeChance;
            List<RecipeData> first = wantUnknown ? unknownPool : knownPool;
            List<RecipeData> second = wantUnknown ? knownPool : unknownPool;

            // ลองกองที่ต้องการก่อน ถ้าหมดใช้อีกกอง
            RecipeData picked = PickFrom(first, true) ?? PickFrom(second, true) ?? PickFrom(first, false) ?? PickFrom(second, false);
            if (picked == null)
            {
                break;
            }

            today.Add(picked);
        }

        if (verboseLog)
        {
            List<string> names = new List<string>();
            foreach (RecipeData recipe in today)
            {
                names.Add(IsKnown(recipe) ? recipe.recipeName : $"{recipe.recipeName} (unknown)");
            }
            Debug.Log($"PopularDishes: Day {day} popular = {(names.Count > 0 ? string.Join(", ", names) : "none")}.");
        }

        OnPopularChanged?.Invoke();
    }

    // สุ่ม 1 สูตรจากกองที่ยังไม่ถูกเลือกวันนี้ (avoidRepeat = ข้ามของเมื่อวานด้วย)
    private RecipeData PickFrom(List<RecipeData> pool, bool avoidRepeat)
    {
        List<RecipeData> options = new List<RecipeData>();
        foreach (RecipeData recipe in pool)
        {
            if (today.Contains(recipe))
            {
                continue;
            }

            if (avoidRepeat && avoidYesterdaysDishes && yesterday.Contains(recipe))
            {
                continue;
            }

            options.Add(recipe);
        }

        return options.Count > 0 ? options[UnityEngine.Random.Range(0, options.Count)] : null;
    }

    // ---------- คำสำหรับคำใบ้ ----------

    private static string CuisineAdjective(Cuisine cuisine)
    {
        switch (cuisine)
        {
            case Cuisine.Thailand: return "Thai";
            case Cuisine.Japan: return "Japanese";
            case Cuisine.China: return "Chinese";
            case Cuisine.Italy: return "Italian";
            case Cuisine.USA: return "American";
            case Cuisine.Mexico: return "Mexican";
            case Cuisine.HouseSpecial: return "house special";
            default: return cuisine.ToString();
        }
    }

    private static string CourseWord(DishCourse course)
    {
        switch (course)
        {
            case DishCourse.Main: return "dish";
            case DishCourse.Dessert: return "dessert";
            case DishCourse.Drink: return "drink";
            default: return course.ToString().ToLowerInvariant();
        }
    }

    // ---------- Debug (คลิกขวาที่หัวคอมโพเนนต์ระหว่าง Play) ----------

    [ContextMenu("Debug: Reroll Popular Dishes")]
    private void DebugReroll()
    {
        Reroll();
    }

    [ContextMenu("Debug: Print Popular Dishes")]
    private void DebugPrint()
    {
        EnsureRolled();
        foreach (RecipeData recipe in today)
        {
            Debug.Log($"PopularDishes: {recipe.recipeName} (known: {IsKnown(recipe)}, shown as \"{GetDisplayName(recipe)}\").");
        }
    }
}
