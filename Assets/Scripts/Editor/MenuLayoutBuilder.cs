using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// เครื่องมือสร้างหน้าต่างเลือกเมนูประจำวันอัตโนมัติ (ใช้ใน Editor เท่านั้น)
// เมนู: Tools > Second Serving > Build Daily Menu Window
// - สร้าง GameObject "DailyMenu" (คอมโพเนนต์ DailyMenu) ถ้ายังไม่มี แล้วผูก Inventory / RecipeBook ของ Player
// - สร้าง MenuSelectionUI → MenuWindow บน Canvas หลัก (ช่องเมนูซ้าย, รายการสูตรขวา, ปุ่ม Cancel / Open Shop)
// - ผูกช่องใน MenuSelectionUI / MenuSlotRowUI / MenuRecipeItemUI / SaveManager ให้
// - [2.4b] ใส่คอมโพเนนต์ PopularDishes บนวัตถุ DailyMenu + สร้างบรรทัด PopularText ใต้หัวข้อ
// - กดซ้ำได้ จะจัดตำแหน่งใหม่โดยไม่สร้างซ้ำ (หาจากชื่อ) กด Ctrl+Z ย้อนได้
public static class MenuLayoutBuilder
{
    // ---------- ขนาด (หน่วยตาม Canvas Scaler 1920x1080) ----------

    private const float WindowWidth = 1200f;
    private const float WindowHeight = 800f;
    private const float Margin = 40f;
    private const float ColumnGap = 40f;
    private const float LeftWidth = 560f;
    private const float ListTop = 190f;
    private const float BottomBarHeight = 60f;

    private const float RowHeight = 110f;
    private const float RowSpacing = 10f;
    private const float RecipeItemHeight = 64f;

    private static float RightLeft => Margin + LeftWidth + ColumnGap;
    private static float RightWidth => WindowWidth - RightLeft - Margin;

    private static readonly Color WindowColor = new Color(0.08f, 0.08f, 0.1f, 0.92f);
    private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color ListColor = new Color(0f, 0f, 0f, 0.25f);
    private static readonly Color ButtonColor = new Color(0.25f, 0.25f, 0.3f, 1f);
    private static readonly Color OpenButtonColor = new Color(0.2f, 0.55f, 0.3f, 1f);

    [MenuItem("Tools/Second Serving/Build Daily Menu Window")]
    public static void Build()
    {
        Canvas canvas = FindMainCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Daily Menu Window", "No Canvas found in the open scene.", "OK");
            return;
        }

        PlayerController player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        Undo.SetCurrentGroupName("Build Daily Menu Window");
        int undoGroup = Undo.GetCurrentGroup();

        // 1) DailyMenu (ระบบ)
        string menuResult;
        DailyMenu dailyMenu = Object.FindFirstObjectByType<DailyMenu>(FindObjectsInactive.Include);
        if (dailyMenu == null)
        {
            GameObject go = new GameObject("DailyMenu");
            Undo.RegisterCreatedObjectUndo(go, "Create DailyMenu");
            dailyMenu = Undo.AddComponent<DailyMenu>(go);
            menuResult = "Created DailyMenu object.";
        }
        else
        {
            menuResult = $"DailyMenu found on '{dailyMenu.name}'.";
        }

        if (player != null)
        {
            SerializedObject menuSo = new SerializedObject(dailyMenu);
            menuSo.FindProperty("inventory").objectReferenceValue = player.GetComponent<Inventory>();
            menuSo.FindProperty("recipeBook").objectReferenceValue = player.GetComponent<RecipeBook>();
            menuSo.ApplyModifiedProperties();
        }
        else
        {
            menuResult += " No PlayerController found: assign Inventory and Recipe Book in DailyMenu yourself.";
        }

        // 1b) [2.4b] เมนูนิยมรายวัน อยู่บนวัตถุเดียวกับ DailyMenu
        if (dailyMenu.GetComponent<PopularDishes>() == null)
        {
            Undo.AddComponent<PopularDishes>(dailyMenu.gameObject);
            menuResult += " Added PopularDishes.";
        }

        // 2) ตัวควบคุม (เปิดอยู่ตลอด เต็มจอ) + ตัวหน้าต่าง (ซ่อน/แสดง)
        GameObject controller = FindOrCreate(canvas.transform, "MenuSelectionUI");
        Undo.RegisterFullObjectHierarchyUndo(controller, "Build Daily Menu Window");
        SetStretch(controller.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        MenuSelectionUI ui = GetOrAdd<MenuSelectionUI>(controller);

        GameObject window = FindOrCreate(controller.transform, "MenuWindow");
        RectTransform windowRt = window.GetComponent<RectTransform>();
        windowRt.anchorMin = new Vector2(0.5f, 0.5f);
        windowRt.anchorMax = new Vector2(0.5f, 0.5f);
        windowRt.pivot = new Vector2(0.5f, 0.5f);
        windowRt.anchoredPosition = Vector2.zero;
        windowRt.sizeDelta = new Vector2(WindowWidth, WindowHeight);
        Image windowBg = GetOrAdd<Image>(window);
        windowBg.color = WindowColor;
        windowBg.raycastTarget = true;

        // 3) หัวข้อและคำแนะนำ
        TMP_Text title = FindOrCreateText(window.transform, "TitleText", 40, TextAlignmentOptions.Center);
        SetTopStretch(title.rectTransform, Margin, Margin, 20f, 56f);
        title.text = "Today's Menu";
        title.fontStyle = FontStyles.Bold;

        TMP_Text hint = FindOrCreateText(window.transform, "HintText", 22, TextAlignmentOptions.Center);
        SetTopStretch(hint.rectTransform, Margin, Margin, 80f, 34f);
        hint.text = "Pick dishes from your recipes. Hold Shift for ±5. Ready dishes in your bag are used first.";
        hint.color = new Color(1f, 1f, 1f, 0.7f);

        // [2.4b] เมนูนิยมวันนี้ (MenuSelectionUI เขียนข้อความให้ตอนเปิดหน้าต่าง)
        TMP_Text popularText = FindOrCreateText(window.transform, "PopularText", 26, TextAlignmentOptions.Center);
        SetTopStretch(popularText.rectTransform, Margin, Margin, 120f, 36f);
        popularText.text = "Popular today:";
        popularText.color = new Color(1f, 0.82f, 0.3f);
        popularText.fontStyle = FontStyles.Bold;

        // 4) ฝั่งซ้าย: ช่องเมนู
        GameObject slotList = FindOrCreate(window.transform, "SlotList");
        RectTransform slotListRt = slotList.GetComponent<RectTransform>();
        slotListRt.anchorMin = new Vector2(0f, 0f);
        slotListRt.anchorMax = new Vector2(0f, 1f);
        slotListRt.pivot = new Vector2(0f, 1f);
        slotListRt.offsetMin = new Vector2(Margin, Margin + BottomBarHeight + 20f);
        slotListRt.offsetMax = new Vector2(Margin + LeftWidth, -ListTop);

        VerticalLayoutGroup slotLayout = GetOrAdd<VerticalLayoutGroup>(slotList);
        slotLayout.spacing = RowSpacing;
        slotLayout.childAlignment = TextAnchor.UpperLeft;
        slotLayout.childControlWidth = true;
        slotLayout.childControlHeight = true;
        slotLayout.childForceExpandWidth = true;
        slotLayout.childForceExpandHeight = false;

        MenuSlotRowUI rowTemplate = BuildSlotRowTemplate(slotList.transform);

        // 5) ฝั่งขวา: รายการสูตร
        TMP_Text recipeLabel = FindOrCreateText(window.transform, "RecipeLabel", 24, TextAlignmentOptions.Left);
        SetTopLeft(recipeLabel.rectTransform, RightLeft, ListTop - 40f, new Vector2(RightWidth, 34f));
        recipeLabel.text = "Known recipes";
        recipeLabel.fontStyle = FontStyles.Bold;

        RectTransform recipeContent = BuildRecipeScrollView(window.transform);
        MenuRecipeItemUI itemTemplate = BuildRecipeItemTemplate(recipeContent);

        // 6) แถบล่าง: รวมจำนวนจาน, Cancel, Open Shop
        TMP_Text total = FindOrCreateText(window.transform, "TotalText", 28, TextAlignmentOptions.Left);
        SetBottomLeft(total.rectTransform, Margin, Margin, new Vector2(LeftWidth, BottomBarHeight));
        total.text = "Total: 0 servings";

        Button openShop = BuildButton(window.transform, "OpenShopButton", "Open Shop", OpenButtonColor, 26);
        SetBottomRight(openShop.GetComponent<RectTransform>(), Margin, Margin, new Vector2(220f, BottomBarHeight));

        Button cancel = BuildButton(window.transform, "CancelButton", "Cancel", ButtonColor, 26);
        SetBottomRight(cancel.GetComponent<RectTransform>(), Margin + 220f + 20f, Margin, new Vector2(180f, BottomBarHeight));

        // 7) ผูกช่องใน MenuSelectionUI
        SerializedObject uiSo = new SerializedObject(ui);
        uiSo.FindProperty("dailyMenu").objectReferenceValue = dailyMenu;
        uiSo.FindProperty("playerController").objectReferenceValue = player;
        uiSo.FindProperty("windowRoot").objectReferenceValue = window;
        uiSo.FindProperty("slotContainer").objectReferenceValue = slotList.transform;
        uiSo.FindProperty("slotRowTemplate").objectReferenceValue = rowTemplate;
        uiSo.FindProperty("recipeContainer").objectReferenceValue = recipeContent;
        uiSo.FindProperty("recipeItemTemplate").objectReferenceValue = itemTemplate;
        uiSo.FindProperty("totalText").objectReferenceValue = total;
        uiSo.FindProperty("popularText").objectReferenceValue = popularText;
        uiSo.FindProperty("openShopButton").objectReferenceValue = openShop;
        uiSo.FindProperty("cancelButton").objectReferenceValue = cancel;
        uiSo.ApplyModifiedProperties();

        // 8) ผูก SaveManager
        string saveResult;
        SaveManager saveManager = Object.FindFirstObjectByType<SaveManager>(FindObjectsInactive.Include);
        if (saveManager != null)
        {
            SerializedObject saveSo = new SerializedObject(saveManager);
            saveSo.FindProperty("dailyMenu").objectReferenceValue = dailyMenu;
            saveSo.ApplyModifiedProperties();
            saveResult = "SaveManager linked.";
        }
        else
        {
            saveResult = "No SaveManager found. The last menu will not be saved.";
        }

        // หน้าต่างอยู่บนสุดของ Canvas (ทับ HUD) และปิดไว้ก่อน
        controller.transform.SetAsLastSibling();
        window.SetActive(false);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = controller;

        Debug.Log($"MenuLayoutBuilder: Daily menu window built. {menuResult} {saveResult}");
        EditorUtility.DisplayDialog("Daily Menu Window",
            "Daily menu window is ready (MenuSelectionUI > MenuWindow, hidden until you press E at the counter).\n\n" +
            menuResult + "\n" + saveResult + "\n\nSave the scene with Ctrl+S.", "OK");
    }

    // ---------- แม่แบบแถวช่องเมนู ----------

    private static MenuSlotRowUI BuildSlotRowTemplate(Transform parent)
    {
        GameObject row = FindOrCreate(parent, "MenuSlotTemplate");
        Image bg = GetOrAdd<Image>(row);
        bg.color = RowColor;

        LayoutElement le = GetOrAdd<LayoutElement>(row);
        le.preferredHeight = RowHeight;
        le.minHeight = RowHeight;

        // ชื่อและข้อมูล: ซ้าย เว้นที่ด้านขวาให้ปุ่ม
        TMP_Text nameText = FindOrCreateText(row.transform, "NameText", 28, TextAlignmentOptions.Left);
        SetTopStretch(nameText.rectTransform, 16f, 250f, 14f, 40f);
        nameText.overflowMode = TextOverflowModes.Ellipsis;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_Text infoText = FindOrCreateText(row.transform, "InfoText", 18, TextAlignmentOptions.Left);
        SetTopStretch(infoText.rectTransform, 16f, 250f, 58f, 36f);
        infoText.color = new Color(1f, 1f, 1f, 0.7f);
        infoText.overflowMode = TextOverflowModes.Ellipsis;
        infoText.textWrappingMode = TextWrappingModes.NoWrap;

        // ปุ่มด้านขวา: − จำนวน + แล้วปุ่ม ✕
        Button minus = BuildButton(row.transform, "MinusButton", "-", ButtonColor, 30);
        SetRightCenter(minus.GetComponent<RectTransform>(), 196f, new Vector2(48f, 48f));

        TMP_Text count = FindOrCreateText(row.transform, "CountText", 30, TextAlignmentOptions.Center);
        SetRightCenter(count.rectTransform, 132f, new Vector2(64f, 48f));
        count.text = "0";
        count.fontStyle = FontStyles.Bold;

        Button plus = BuildButton(row.transform, "PlusButton", "+", ButtonColor, 30);
        SetRightCenter(plus.GetComponent<RectTransform>(), 80f, new Vector2(48f, 48f));

        Button remove = BuildButton(row.transform, "RemoveButton", "X", new Color(0.5f, 0.2f, 0.2f, 1f), 22);
        SetRightCenter(remove.GetComponent<RectTransform>(), 16f, new Vector2(44f, 44f));

        MenuSlotRowUI rowUI = GetOrAdd<MenuSlotRowUI>(row);
        SerializedObject so = new SerializedObject(rowUI);
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("infoText").objectReferenceValue = infoText;
        so.FindProperty("countText").objectReferenceValue = count;
        so.FindProperty("minusButton").objectReferenceValue = minus;
        so.FindProperty("plusButton").objectReferenceValue = plus;
        so.FindProperty("removeButton").objectReferenceValue = remove;
        so.ApplyModifiedProperties();

        nameText.text = "Slot 1 — pick a recipe on the right";
        infoText.text = "";

        row.SetActive(false);
        return rowUI;
    }

    // ---------- รายการสูตร ----------

    private static RectTransform BuildRecipeScrollView(Transform window)
    {
        GameObject scrollGo = FindOrCreate(window, "RecipeScrollView");
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(0f, 1f);
        scrollRt.pivot = new Vector2(0f, 1f);
        scrollRt.offsetMin = new Vector2(RightLeft, Margin + BottomBarHeight + 20f);
        scrollRt.offsetMax = new Vector2(RightLeft + RightWidth, -ListTop);

        Image bg = GetOrAdd<Image>(scrollGo);
        bg.color = ListColor;

        ScrollRect scroll = GetOrAdd<ScrollRect>(scrollGo);
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        GameObject viewportGo = FindOrCreate(scrollGo.transform, "Viewport");
        RectTransform viewportRt = viewportGo.GetComponent<RectTransform>();
        SetStretch(viewportRt, 0f, 0f, 0f, 0f);
        GetOrAdd<RectMask2D>(viewportGo);

        GameObject contentGo = FindOrCreate(viewportGo.transform, "Content");
        RectTransform contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = GetOrAdd<VerticalLayoutGroup>(contentGo);
        layout.spacing = 6f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(contentGo);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRt;
        scroll.content = contentRt;

        return contentRt;
    }

    private static MenuRecipeItemUI BuildRecipeItemTemplate(Transform parent)
    {
        GameObject item = FindOrCreate(parent, "RecipeItemTemplate");
        Image bg = GetOrAdd<Image>(item);
        bg.color = Color.white;

        Button button = GetOrAdd<Button>(item);
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.12f);
        colors.highlightedColor = new Color(1f, 0.85f, 0.4f, 0.35f);
        colors.pressedColor = new Color(1f, 0.85f, 0.4f, 0.55f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0.12f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.03f);
        button.colors = colors;

        LayoutElement le = GetOrAdd<LayoutElement>(item);
        le.preferredHeight = RecipeItemHeight;
        le.minHeight = RecipeItemHeight;

        TMP_Text nameText = FindOrCreateText(item.transform, "NameText", 24, TextAlignmentOptions.Left);
        SetTopStretch(nameText.rectTransform, 14f, 14f, 6f, 30f);
        nameText.overflowMode = TextOverflowModes.Ellipsis;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.raycastTarget = false;

        TMP_Text infoText = FindOrCreateText(item.transform, "InfoText", 17, TextAlignmentOptions.Left);
        SetTopStretch(infoText.rectTransform, 14f, 14f, 36f, 24f);
        infoText.color = new Color(1f, 1f, 1f, 0.7f);
        infoText.overflowMode = TextOverflowModes.Ellipsis;
        infoText.textWrappingMode = TextWrappingModes.NoWrap;
        infoText.raycastTarget = false;

        MenuRecipeItemUI itemUI = GetOrAdd<MenuRecipeItemUI>(item);
        SerializedObject so = new SerializedObject(itemUI);
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("infoText").objectReferenceValue = infoText;
        so.ApplyModifiedProperties();

        item.SetActive(false);
        return itemUI;
    }

    // ---------- ปุ่ม ----------

    private static Button BuildButton(Transform parent, string name, string label, Color color, float fontSize)
    {
        GameObject go = FindOrCreate(parent, name);
        Image bg = GetOrAdd<Image>(go);
        bg.color = color;

        Button button = GetOrAdd<Button>(go);
        button.targetGraphic = bg;

        TMP_Text text = FindOrCreateText(go.transform, "Label", fontSize, TextAlignmentOptions.Center);
        SetStretch(text.rectTransform, 0f, 0f, 0f, 0f);
        text.text = label;
        text.raycastTarget = false;

        return button;
    }

    // ---------- ตัวช่วยจัดตำแหน่ง ----------

    private static Canvas FindMainCanvas()
    {
        InventoryUI inventoryUI = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        if (inventoryUI != null)
        {
            Canvas c = inventoryUI.GetComponentInParent<Canvas>(true);
            if (c != null)
            {
                return c.rootCanvas;
            }
        }

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c.isRootCanvas)
            {
                return c;
            }
        }
        return null;
    }

    private static void SetTopStretch(RectTransform rt, float left, float right, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -(top + height));
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static void SetTopLeft(RectTransform rt, float left, float top, Vector2 size)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(left, -top);
        rt.sizeDelta = size;
    }

    private static void SetBottomLeft(RectTransform rt, float left, float bottom, Vector2 size)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(left, bottom);
        rt.sizeDelta = size;
    }

    private static void SetBottomRight(RectTransform rt, float right, float bottom, Vector2 size)
    {
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-right, bottom);
        rt.sizeDelta = size;
    }

    // ยึดขวา กึ่งกลางแนวตั้ง (right = ระยะจากขอบขวาถึงขอบขวาของวัตถุ)
    private static void SetRightCenter(RectTransform rt, float right, Vector2 size)
    {
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-right, 0f);
        rt.sizeDelta = size;
    }

    private static void SetStretch(RectTransform rt, float left, float right, float top, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    // ---------- ตัวช่วยสร้าง/หาวัตถุ ----------

    private static GameObject FindOrCreate(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null)
        {
            return t.gameObject;
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        return go;
    }

    private static TMP_Text FindOrCreateText(Transform parent, string name, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = FindOrCreate(parent, name);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();

        if (text == null)
        {
            text = Undo.AddComponent<TextMeshProUGUI>(go);
            text.text = name;
        }
        else
        {
            Undo.RecordObject(text, $"Edit {name}");
        }

        text.fontSize = fontSize;
        text.alignment = alignment;
        return text;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null)
        {
            component = Undo.AddComponent<T>(go);
        }
        else
        {
            Undo.RecordObject(component, $"Edit {typeof(T).Name}");
        }
        return component;
    }
}
