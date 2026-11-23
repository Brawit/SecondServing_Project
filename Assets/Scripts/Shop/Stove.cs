using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// [2.5] เตาทำอาหาร (กด E) ใช้ได้ทุกเวลา วางกี่เตาก็ได้
// - ตอนเปิดร้าน: แถบเลือกเมนูเหนือเตา เฉพาะเมนูวันนี้ที่ยังทำได้ เรียงตามลูกค้าที่รอมากสุด เช่น "< [Som Tam (2 waiting)]  Lemonade >"
//   - มีเมนูเดียว = เริ่มทำเลย / scroll = เลื่อน (ขึ้น = ซ้าย) / คลิกตัวเลือก = เลือก / คลิกซ้ำหรือ E = เริ่มทำ / Esc หรือเดินห่าง = ปิด
//   - ถือจานอยู่ = ทำใหม่ไม่ได้ (ต้องเสิร์ฟหรือกด Q เก็บก่อน)
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

    private readonly List<RecipeData> options = new List<RecipeData>();
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
            NotificationUI.Post(string.Format(handsFullFormat, hand.Recipe.recipeName));
            return;
        }

        if (BuildOptions(options) == 0)
        {
            NotificationUI.Post(nothingToCookMessage);
            return;
        }

        if (options.Count == 1)
        {
            StartCooking(options[0]);
            return;
        }

        OpenPickerWith(options[0]);
    }

    // ---------- แถบเลือกเมนู ----------

    private void OpenPickerWith(RecipeData first)
    {
        // ปิดแถบของเตาอื่น / แถบเลือกคุณภาพตอนเสิร์ฟ (ใช้ scroll เหมือนกัน)
        if (OpenPicker != null && OpenPicker != this) OpenPicker.ClosePicker();
        if (ServePicker.Instance != null && ServePicker.Instance.IsOpen) ServePicker.Instance.Close();

        pickerOpen = true;
        OpenPicker = this;
        selectedIndex = Mathf.Max(0, options.IndexOf(first));
        RefreshBubble();
    }

    public void ClosePicker()
    {
        if (!pickerOpen)
        {
            return;
        }

        pickerOpen = false;
        if (OpenPicker == this) OpenPicker = null;
        RefreshBubble();
    }

    // ปิดแถบของเตาที่เปิดอยู่ (Customer เรียกก่อนเปิดแถบเลือกคุณภาพ)
    public static void CloseAnyPicker()
    {
        if (OpenPicker != null) OpenPicker.ClosePicker();
    }

    private RecipeData SelectedRecipe =>
        selectedIndex >= 0 && selectedIndex < options.Count ? options[selectedIndex] : null;

    private void ConfirmPicker()
    {
        RecipeData recipe = SelectedRecipe;
        ClosePicker();
        if (recipe != null) StartCooking(recipe);
    }

    private void StartCooking(RecipeData recipe)
    {
        if (CookingSession.Instance == null)
        {
            Debug.LogWarning("Stove: No CookingSession in the scene. Run Tools > Second Serving > Build Cooking System.", this);
            return;
        }

        // แถบเลือกคุณภาพตอนเสิร์ฟเปิดค้างอยู่ (เช่น ลูกค้านั่งใกล้เตา) ปิดก่อนเริ่มทำ
        if (ServePicker.Instance != null && ServePicker.Instance.IsOpen) ServePicker.Instance.Close();

        CookingSession.Instance.StartCooking(recipe, CookingSession.CookMode.InShop);
    }

    // เมนูวันนี้ที่ยังทำได้ เรียงตามลูกค้าที่รอมากสุด (เท่ากัน = ตามลำดับในเมนู)
    private int BuildOptions(List<RecipeData> result)
    {
        result.Clear();
        DailyMenu menu = DailyMenu.Instance;
        if (menu == null || !menu.IsActive)
        {
            return 0;
        }

        foreach (DailyMenu.MenuEntry entry in menu.Entries)
        {
            if (!entry.IsEmpty && menu.GetCookRoom(entry.Recipe) > 0)
            {
                result.Add(entry.Recipe);
            }
        }

        // เรียงแบบคงลำดับเดิมเมื่อจำนวนรอเท่ากัน
        List<RecipeData> menuOrder = new List<RecipeData>(result);
        result.Sort((a, b) =>
        {
            int byWaiting = CountWaiting(b).CompareTo(CountWaiting(a));
            return byWaiting != 0 ? byWaiting : menuOrder.IndexOf(a).CompareTo(menuOrder.IndexOf(b));
        });

        return result.Count;
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
        RecipeData previous = SelectedRecipe;
        if (BuildOptions(options) == 0)
        {
            ClosePicker();
            return;
        }
        selectedIndex = previous != null ? Mathf.Max(0, options.IndexOf(previous)) : 0;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
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

        StringBuilder sb = new StringBuilder("< ");
        for (int i = 0; i < options.Count; i++)
        {
            RecipeData recipe = options[i];
            int waiting = CountWaiting(recipe);
            string text = waiting > 0 ? string.Format(waitingFormat, recipe.recipeName, waiting) : recipe.recipeName;

            // [2.5b] ดาวความยากต่อท้ายชื่อ
            CookingSettings settings = CookingSession.Instance != null ? CookingSession.Instance.Settings : null;
            if (settings != null) text += $" <size=80%><noparse>{settings.Stars(recipe.Difficulty)}</noparse></size>";
            if (i == selectedIndex) text = $"<b>[{text}]</b>";
            text = $"<link=\"r{i}\">{text}</link>";
            if (i > 0) sb.Append("  ");
            sb.Append(text);
        }
        sb.Append(" >");

        if (!string.IsNullOrEmpty(pickerHint))
        {
            sb.Append($"\n<size=70%>{pickerHint}</size>");
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
        if (id != null && id.Length > 1 && id[0] == 'r' && int.TryParse(id.Substring(1), out int option))
        {
            return option;
        }
        return -1;
    }
}
