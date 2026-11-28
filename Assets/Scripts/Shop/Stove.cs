using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// [2.5] เตาทำอาหาร (กด E) ใช้ได้ทุกเวลา วางกี่เตาก็ได้
// - ตอนเปิดร้าน: แถบเลือกเมนูเหนือเตา เฉพาะเมนูวันนี้ที่ยังทำได้ ตามลำดับช่องในเมนู เช่น "< [Som Tam (2 waiting)]  Lemonade >"
//   - มีเมนูเดียว = เริ่มทำเลย / scroll = เลื่อน (ขึ้น = ซ้าย) / คลิกตัวเลือก = เลือก / คลิกซ้ำหรือ E = เริ่มทำ / Esc หรือเดินห่าง = ปิด
//   - [v2] แบบ carousel: แสดงทีละ Visible Options ตัวรอบตัวที่เลือก + ตำแหน่ง "4/12" / คลิกลูกศร < > = เลื่อนไป 1 ตัว
//   - ถือจานอยู่ = ทำใหม่ไม่ได้ (ต้องเสิร์ฟหรือกด Q เก็บก่อน)
//   - [v2 ผู้ใช้] แถบ 2 ชั้น: ชั้นแรก = ชื่อเมนู ตามลำดับช่องในเมนูที่ตั้งก่อนเปิดร้าน
//     เลือกเมนูที่มีช่องหมวด (any_fish ฯลฯ) และมีวัตถุดิบในหมวด 2 ชนิดขึ้นไป → ชั้นสอง = เลือกวัตถุดิบ เรียงราคาจานที่จะได้ (OK) แพงสุดก่อน
//     ชั้นสอง Esc = กลับชั้นแรก / เมนูไม่มีช่องหมวด หรือวัตถุดิบชนิดเดียว = เริ่มทำทันที / เริ่มทำแล้วจำเป็นตัวล่าสุดของสูตร (RecipeBook)
// - นอกเวลาร้าน: เปิดหน้าต่างรายการสูตร (CookingMenuUI) ทำเสร็จเข้ากระเป๋า
// ต้องมี Collider2D บนวัตถุเดียวกัน (แบบทึบ ลูกค้าจะเดินอ้อม / ผู้เล่นกด E ได้ในระยะ)
// สร้าง prefab ได้จาก Tools > Second Serving > Build Cooking System (Assets/Prefabs/Stove.prefab)
public class Stove : MonoBehaviour, IInteractable
{
    // เตาที่เปิดแถบเลือกอยู่ (เปิดได้ทีละเตา)
    public static Stove OpenPicker { get; private set; }

    [Header("References")]
    [Tooltip("ข้อความเหนือเตา (TextMeshPro แบบ 3D)")]
    [SerializeField] private TMP_Text bubbleText;

    [Header("Picker")]
    [Tooltip("เดินห่างจากเตาเกินระยะนี้ (หน่วย) = ปิดแถบเลือก")]
    [Min(0.5f)] [SerializeField] private float closeDistance = 2f;

    [Tooltip("ปกติ scroll ขึ้น = เลื่อนไปทางซ้าย ติ๊กเพื่อกลับทิศ")]
    [SerializeField] private bool invertScroll = false;

    [Tooltip("[v2] แบบ carousel: แสดงตัวเลือกพร้อมกันได้มากสุดเท่านี้ (รอบตัวที่เลือกอยู่) ที่เหลือเลื่อนดู")]
    [Min(1)] [SerializeField] private int visibleOptions = 3;

    [Tooltip("[v2] ตัวบอกตำแหน่งเมื่อมีตัวเลือกมากกว่าที่แสดง ({0} = ลำดับที่เลือก, {1} = ทั้งหมด) เว้นว่าง = ไม่แสดง")]
    [SerializeField] private string positionFormat = "{0}/{1}";

    [Header("Prompt")]
    [Tooltip("แสดงข้อความชวนกด E เมื่อผู้เล่นอยู่ใกล้ (เว้นว่าง = ไม่แสดง)")]
    [SerializeField] private string promptText = "[E] Cook";

    [Tooltip("ระยะที่แสดงข้อความชวนกด E")]
    [Min(0.5f)] [SerializeField] private float promptDistance = 1.6f;

    [Header("Text")]
    [Tooltip("ตัวเลือกในแถบ ({0} = ชื่อจาน, {1} = จำนวนลูกค้าที่รอ) ใช้เมื่อมีคนรอ")]
    [SerializeField] private string waitingFormat = "{0} ({1} waiting)";

    [Tooltip("บรรทัดวิธีใช้ใต้ตัวเลือก (เว้นว่าง = ไม่แสดง)")]
    [SerializeField] private string pickerHint = "Scroll / click to pick · Click again or E to cook · Esc to cancel";

    [Tooltip("ถือจานอยู่ ({0} = ชื่อจาน)")]
    [SerializeField] private string handsFullFormat = "Hands full! Serve {0} or press Q to stash it.";

    [Tooltip("ไม่มีอะไรให้ทำตอนเปิดร้าน")]
    [SerializeField] private string nothingToCookMessage = "Nothing to cook. Today's menu is cooked or sold out.";

    [Tooltip("นอกเวลาร้านแต่ไม่มีหน้าต่างรายการสูตรในฉาก")]
    [SerializeField] private string noWindowMessage = "No cooking window in the scene. Run Build Cooking System.";

    // [v2] ตัวเลือก 1 ตัวในแถบ: สูตร + วัตถุดิบที่ใส่ช่องหมวด (สูตรไม่มีช่องหมวด = null)
    private struct CookOption
    {
        public RecipeData recipe;
        public ItemData choice;

        public bool Same(CookOption other) => recipe == other.recipe && choice == other.choice;
    }

    [Tooltip("[v2] ชั้นเลือกวัตถุดิบ: ข้อความตัวเลือก ({0} = ชื่อสูตร, {1} = ชื่อวัตถุดิบ, {2} = จำนวนในกระเป๋า, {3} = ราคาขาย OK)")]
    [SerializeField] private string choiceOptionFormat = "{1} x{2} <size=80%>{3}</size>";

    [Tooltip("[v2] ชั้นเลือกวัตถุดิบ: หัวแถบ ({0} = ชื่อสูตร)")]
    [SerializeField] private string choiceHeaderFormat = "<size=80%>{0}:</size> ";

    [Tooltip("[v2] ชั้นเลือกวัตถุดิบ: บรรทัดวิธีใช้ (เว้นว่าง = ไม่แสดง)")]
    [SerializeField] private string choiceHint = "Scroll / click to pick · Click again or E to cook · Esc to go back";

    // [v2] สูตรที่กำลังเลือกวัตถุดิบ (null = ชั้นแรก เลือกเมนู)
    private RecipeData pickedRecipe;

    private readonly List<CookOption> options = new List<CookOption>();
    private int selectedIndex;
    private Transform player;
    private bool pickerOpen;

    private void Awake()
    {
        if (GetComponent<Collider2D>() == null)
        {
            Debug.LogWarning($"Stove '{name}': Needs a Collider2D to be found by the player.", this);
        }
        if (bubbleText == null)
        {
            Debug.LogWarning($"Stove '{name}': Bubble Text is not assigned.", this);
        }
    }

    private void Start()
    {
        PlayerInteractor interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor != null) player = interactor.transform;
        RefreshBubble();
    }

    private void OnDisable()
    {
        ClosePicker();
    }

    // ---------- IInteractable ----------

    public bool CanInteract(PlayerInteractor interactor)
    {
        return !CookingSession.IsBusy && !MenuSelectionUI.IsAnyOpen && !CookingMenuUI.IsAnyOpen;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (interactor != null) player = interactor.transform;

        ShopManager shop = ShopManager.Instance;
        if (shop != null && shop.IsServing)
        {
            HandleShopInteract();
            return;
        }

        // นอกเวลาร้าน: หน้าต่างรายการสูตร
        if (CookingMenuUI.Instance != null)
        {
            CookingMenuUI.Instance.Open();
        }
        else
        {
            NotificationUI.Post(noWindowMessage);
        }
    }

    // ตอนเปิดร้าน: แถบเปิดอยู่ = เริ่มทำตัวที่เลือก / ไม่งั้นเปิดแถบ (มีตัวเดียว = เริ่มเลย)
    private void HandleShopInteract()
    {
        if (pickerOpen)
        {
            ConfirmPicker();
            return;
        }

        CarriedDish hand = CarriedDish.Instance;
        if (hand != null && hand.HasDish)
        {
            NotificationUI.Post(string.Format(handsFullFormat, hand.Item.itemName));
            return;
        }

        if (BuildOptions(options) == 0)
        {
            NotificationUI.Post(nothingToCookMessage);
            return;
        }

        // มีเมนูเดียว: เลือกเลย (มีวัตถุดิบหลายชนิด = เปิดชั้นเลือกวัตถุดิบ)
        if (options.Count == 1)
        {
            ChooseMenu(options[0].recipe);
            return;
        }

        OpenPickerWith(0);
    }

    // [v2] เลือกเมนูในชั้นแรก: มีวัตถุดิบในหมวดหลายชนิด = ไปชั้นเลือกวัตถุดิบ / ไม่งั้นเริ่มทำเลย
    private void ChooseMenu(RecipeData recipe)
    {
        if (recipe == null)
        {
            ClosePicker();
            return;
        }

        DailyMenu menu = DailyMenu.Instance;
        if (recipe.HasCategorySlot && menu != null && menu.Inventory != null && SortedChoices(recipe, menu.Inventory).Count >= 2)
        {
            EnterIngredientLevel(recipe);
            return;
        }

        // วัตถุดิบชนิดเดียว = CookingSession เลือกตัวนั้นเอง (choice null)
        ClosePicker();
        StartCooking(new CookOption { recipe = recipe });
    }

    // [v2] เปิดชั้นเลือกวัตถุดิบของสูตรนี้ (เลือกตัวแพงสุดไว้ก่อน)
    private void EnterIngredientLevel(RecipeData recipe)
    {
        pickedRecipe = recipe;
        if (BuildOptions(options) == 0)
        {
            pickedRecipe = null;
            ClosePicker();
            return;
        }

        if (!pickerOpen)
        {
            OpenPickerWith(0);
        }
        else
        {
            selectedIndex = 0;
            RefreshBubble();
        }
    }

    // [v2] กลับจากชั้นเลือกวัตถุดิบไปชั้นเมนู (เลือกเมนูเดิมไว้)
    private void BackToMenuLevel()
    {
        RecipeData previous = pickedRecipe;
        pickedRecipe = null;
        if (BuildOptions(options) == 0)
        {
            ClosePicker();
            return;
        }

        selectedIndex = Mathf.Max(0, options.FindIndex(o => o.recipe == previous));
        RefreshBubble();
    }

    // ---------- แถบเลือกเมนู ----------

    private void OpenPickerWith(int firstIndex)
    {
        // ปิดแถบของเตาอื่น / แถบเลือกคุณภาพตอนเสิร์ฟ (ใช้ scroll เหมือนกัน)
        if (OpenPicker != null && OpenPicker != this) OpenPicker.ClosePicker();
        if (ServePicker.Instance != null && ServePicker.Instance.IsOpen) ServePicker.Instance.Close();

        pickerOpen = true;
        OpenPicker = this;
        selectedIndex = Mathf.Clamp(firstIndex, 0, Mathf.Max(0, options.Count - 1));
        RefreshBubble();
    }

    public void ClosePicker()
    {
        if (!pickerOpen)
        {
            return;
        }

        pickerOpen = false;
        pickedRecipe = null;
        if (OpenPicker == this) OpenPicker = null;
        RefreshBubble();
    }

    // ปิดแถบของเตาที่เปิดอยู่ (Customer เรียกก่อนเปิดแถบเลือกคุณภาพ)
    public static void CloseAnyPicker()
    {
        if (OpenPicker != null) OpenPicker.ClosePicker();
    }

    private bool HasSelection => selectedIndex >= 0 && selectedIndex < options.Count;

    private void ConfirmPicker()
    {
        if (!HasSelection)
        {
            ClosePicker();
            return;
        }

        CookOption option = options[selectedIndex];

        // ชั้นแรก: เลือกเมนู (อาจไปต่อชั้นเลือกวัตถุดิบ)
        if (pickedRecipe == null)
        {
            ChooseMenu(option.recipe);
            return;
        }

        // ชั้นสอง: เริ่มทำด้วยวัตถุดิบที่เลือก
        ClosePicker();
        StartCooking(option);
    }

    private void StartCooking(CookOption option)
    {
        RecipeData recipe = option.recipe;
        if (recipe == null)
        {
            return;
        }

        if (CookingSession.Instance == null)
        {
            Debug.LogWarning("Stove: No CookingSession in the scene. Run Tools > Second Serving > Build Cooking System.", this);
            return;
        }

        // แถบเลือกคุณภาพตอนเสิร์ฟเปิดค้างอยู่ (เช่น ลูกค้านั่งใกล้เตา) ปิดก่อนเริ่มทำ
        if (ServePicker.Instance != null && ServePicker.Instance.IsOpen) ServePicker.Instance.Close();

        // [v2] วัตถุดิบที่ผู้เล่นเลือกในแถบ (สูตรไม่มีช่องหมวด = null) เริ่มทำได้ = จำเป็นตัวล่าสุดของสูตร
        DailyMenu menu = DailyMenu.Instance;
        bool started = CookingSession.Instance.StartCooking(recipe, CookingSession.CookMode.InShop, option.choice);
        if (started && recipe.HasCategorySlot && menu != null && menu.RecipeBook != null && CookingSession.Instance.CurrentChoice != null)
        {
            menu.RecipeBook.SetLastChoice(recipe, CookingSession.Instance.CurrentChoice);
        }
    }

    // ตัวเลือกของชั้นที่เปิดอยู่
    // - ชั้นแรก (pickedRecipe null) = เมนูวันนี้ที่ยังทำได้ เรียงตามช่องในเมนู
    // - [v2] ชั้นสอง = วัตถุดิบในหมวดของ pickedRecipe ที่มีพอทำ เรียงราคาแพงสุดก่อน (สูตรนั้นทำไม่ได้แล้ว = 0)
    private int BuildOptions(List<CookOption> result)
    {
        result.Clear();
        DailyMenu menu = DailyMenu.Instance;
        if (menu == null || !menu.IsActive)
        {
            return 0;
        }

        if (pickedRecipe != null)
        {
            if (menu.Inventory == null || menu.IndexOf(pickedRecipe) < 0 || menu.GetCookRoom(pickedRecipe) <= 0)
            {
                return 0;
            }

            foreach (ItemData candidate in SortedChoices(pickedRecipe, menu.Inventory))
            {
                result.Add(new CookOption { recipe = pickedRecipe, choice = candidate });
            }
            return result.Count;
        }

        foreach (DailyMenu.MenuEntry entry in menu.Entries)
        {
            if (!entry.IsEmpty && menu.GetCookRoom(entry.Recipe) > 0)
            {
                result.Add(new CookOption { recipe = entry.Recipe });
            }
        }

        return result.Count;
    }

    // [v2] วัตถุดิบในหมวดที่มีพอทำ เรียงราคาจานที่จะได้แพงสุดก่อน (ราคาเท่ากัน = มีมากกว่าก่อน ตามลำดับของ GetCandidates)
    private static List<ItemData> SortedChoices(RecipeData recipe, Inventory inventory)
    {
        List<ItemData> candidates = RecipeRequirements.GetCandidates(recipe, inventory);
        List<ItemData> byCount = new List<ItemData>(candidates);
        candidates.Sort((a, b) =>
        {
            int byPrice = PriceOf(new CookOption { recipe = recipe, choice = b }).CompareTo(PriceOf(new CookOption { recipe = recipe, choice = a }));
            return byPrice != 0 ? byPrice : byCount.IndexOf(a).CompareTo(byCount.IndexOf(b));
        });
        return candidates;
    }

    // [v2] ราคาขายของจานที่ตัวเลือกนี้จะได้ ที่คุณภาพ OK (รวมตัวคูณดาวแล้ว ใช้เรียงวัตถุดิบในสูตรเดียวกัน)
    private static int PriceOf(CookOption option)
    {
        if (option.recipe == null) return 0;
        int basePrice = option.choice != null
            ? DishPricing.VariantPrice(option.recipe, option.choice)
            : (option.recipe.dishItem != null ? option.recipe.dishItem.sellPrice : option.recipe.sellPrice);
        return DishPricing.PriceFor(basePrice, option.recipe.Difficulty, DishQuality.Ok);
    }

    private static int IndexOfOption(List<CookOption> list, CookOption option)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Same(option)) return i;
        }
        return -1;
    }

    // ลูกค้าที่นั่งรอจานนี้อยู่
    private static int CountWaiting(RecipeData recipe)
    {
        CustomerManager manager = CustomerManager.Instance;
        if (manager == null || recipe == null)
        {
            return 0;
        }

        int count = 0;
        foreach (Customer customer in manager.Customers)
        {
            if (customer != null && customer.State == CustomerState.WaitingForFood && customer.Order == recipe)
            {
                count++;
            }
        }
        return count;
    }

    // ---------- อัปเดต ----------

    private void Update()
    {
        if (!pickerOpen)
        {
            UpdatePrompt();
            return;
        }

        ShopManager shop = ShopManager.Instance;
        bool farAway = player != null && Vector2.Distance(player.position, transform.position) > closeDistance;
        if (shop == null || !shop.IsServing || CookingSession.IsBusy || farAway)
        {
            ClosePicker();
            return;
        }

        // ตัวเลือกเปลี่ยนได้ระหว่างเปิด (ลูกค้ามา/ไป ทำครบแล้ว) จำตัวที่เลือกไว้
        bool hadSelection = HasSelection;
        CookOption previous = hadSelection ? options[selectedIndex] : default;
        if (BuildOptions(options) == 0)
        {
            // [v2] ชั้นเลือกวัตถุดิบ: สูตรนั้นทำไม่ได้แล้ว = กลับชั้นเมนู
            if (pickedRecipe != null)
            {
                BackToMenuLevel();
                return;
            }
            ClosePicker();
            return;
        }
        selectedIndex = hadSelection ? Mathf.Max(0, IndexOfOption(options, previous)) : 0;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            // [v2] ชั้นเลือกวัตถุดิบ: Esc = กลับชั้นเมนู
            if (pickedRecipe != null)
            {
                BackToMenuLevel();
                return;
            }
            ClosePicker();
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                int direction = scroll > 0f ? -1 : 1;
                if (invertScroll) direction = -direction;
                selectedIndex = Mathf.Clamp(selectedIndex + direction, 0, options.Count - 1);
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                int clicked = FindClickedOption(mouse.position.ReadValue());
                if (clicked >= 0 && clicked < options.Count)
                {
                    if (clicked == selectedIndex)
                    {
                        ConfirmPicker();
                        return;
                    }
                    selectedIndex = clicked;
                }
            }
        }

        RefreshBubble();
    }

    // ข้อความชวนกด E ตอนผู้เล่นอยู่ใกล้ (ไม่แสดงระหว่างทำอาหาร)
    private bool promptShown;

    private void UpdatePrompt()
    {
        bool show = !string.IsNullOrEmpty(promptText) && player != null && !CookingSession.IsBusy &&
                    Vector2.Distance(player.position, transform.position) <= promptDistance;
        if (show != promptShown)
        {
            promptShown = show;
            RefreshBubble();
        }
    }

    // ---------- ข้อความ ----------

    private void RefreshBubble()
    {
        if (bubbleText == null)
        {
            return;
        }

        if (!pickerOpen)
        {
            bubbleText.text = promptShown ? promptText : "";
            return;
        }

        // [v2] carousel: แสดงเฉพาะช่วงรอบตัวที่เลือก (ตัวที่เลือกอยู่กลาง ถ้าทำได้) ลูกศรบอกว่ายังมีอีกด้านไหน
        int visible = Mathf.Clamp(visibleOptions, 1, Mathf.Max(1, options.Count));
        int first = Mathf.Clamp(selectedIndex - visible / 2, 0, Mathf.Max(0, options.Count - visible));
        int last = Mathf.Min(options.Count, first + visible);
        bool moreLeft = first > 0;
        bool moreRight = last < options.Count;

        // ลูกศรคลิกได้ (เลื่อนไป 1 ตัว) / ฝั่งที่ไม่มีแล้ว = จาง
        StringBuilder sb = new StringBuilder();
        if (pickedRecipe != null && !string.IsNullOrEmpty(choiceHeaderFormat))
        {
            sb.Append(string.Format(choiceHeaderFormat, pickedRecipe.recipeName));
        }
        sb.Append(moreLeft
            ? "<link=\"prev\"><b><noparse><</noparse></b></link> "
            : "<alpha=#40><noparse><</noparse><alpha=#FF> ");
        for (int i = first; i < last; i++)
        {
            RecipeData recipe = options[i].recipe;
            ItemData choice = options[i].choice;
            string text;
            if (choice != null)
            {
                // [v2] ชั้นเลือกวัตถุดิบ เช่น "Salmon x2 B401"
                Inventory bag = DailyMenu.Instance != null ? DailyMenu.Instance.Inventory : null;
                text = string.Format(choiceOptionFormat, recipe.recipeName, choice.itemName,
                    bag != null ? bag.GetCount(choice) : 0, PlayerWallet.Format(PriceOf(options[i])));
            }
            else
            {
                int waiting = CountWaiting(recipe);
                text = waiting > 0 ? string.Format(waitingFormat, recipe.recipeName, waiting) : recipe.recipeName;

                // [2.5b] ดาวความยากต่อท้ายชื่อ
                CookingSettings settings = CookingSession.Instance != null ? CookingSession.Instance.Settings : null;
                if (settings != null) text += $" <size=80%><noparse>{settings.Stars(recipe.Difficulty)}</noparse></size>";
            }
            if (i == selectedIndex) text = $"<b>[{text}]</b>";
            text = $"<link=\"r{i}\">{text}</link>";
            if (i > first) sb.Append("  ");
            sb.Append(text);
        }
        sb.Append(moreRight
            ? " <link=\"next\"><b><noparse>></noparse></b></link>"
            : " <alpha=#40><noparse>></noparse><alpha=#FF>");

        // ตำแหน่ง เช่น "4/12" (เฉพาะตอนมีตัวเลือกมากกว่าที่แสดง)
        if (options.Count > visible && !string.IsNullOrEmpty(positionFormat))
        {
            sb.Append($"  <size=70%>{string.Format(positionFormat, selectedIndex + 1, options.Count)}</size>");
        }

        string hint = pickedRecipe != null ? choiceHint : pickerHint;
        if (!string.IsNullOrEmpty(hint))
        {
            sb.Append($"\n<size=70%>{hint}</size>");
        }

        bubbleText.text = sb.ToString();
    }

    // หาว่าคลิกโดนตัวเลือกไหน (ห่อด้วย <link="r0"> ...) คืนค่า -1 ถ้าไม่โดน
    private int FindClickedOption(Vector2 screenPosition)
    {
        Camera cam = Camera.main;
        if (bubbleText == null || cam == null)
        {
            return -1;
        }

        Canvas canvas = bubbleText.canvas;
        Camera useCam = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(bubbleText, screenPosition, useCam);
        if (linkIndex < 0 || linkIndex >= bubbleText.textInfo.linkCount)
        {
            return -1;
        }

        string id = bubbleText.textInfo.linkInfo[linkIndex].GetLinkID();

        // [v2] ลูกศร carousel = เลือกตัวข้างๆ (ยังไม่เริ่มทำ)
        if (id == "prev") return selectedIndex > 0 ? selectedIndex - 1 : -1;
        if (id == "next") return selectedIndex < options.Count - 1 ? selectedIndex + 1 : -1;

        if (id != null && id.Length > 1 && id[0] == 'r' && int.TryParse(id.Substring(1), out int option))
        {
            return option;
        }
        return -1;
    }
}
