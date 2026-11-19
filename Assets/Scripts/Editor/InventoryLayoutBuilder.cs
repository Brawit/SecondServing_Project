using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// เครื่องมือจัดหน้าต่างกระเป๋าอัตโนมัติ (ใช้ใน Editor เท่านั้น)
// เมนู: Tools > Second Serving > Build Inventory Window Layout
// - เอา Vertical Layout Group / Content Size Fitter ออกจาก InventoryWindow
// - จัดตำแหน่ง TitleText, ProgressText, BagLevelText, TabBar
// - สร้าง Scroll View (ตารางช่องที่เลื่อนได้), EmptyTabText, DetailPanel ถ้ายังไม่มี
// - ปิด SlotGrid เดิม และผูกช่องต่างๆ ในคอมโพเนนต์ InventoryUI ให้
// - แก้ prefab ช่อง: ย้ายตัวเลขไปมุมขวาบน และเพิ่มกรอบเลือก (SelectionFrame)
// กด Ctrl+Z ย้อนการเปลี่ยนแปลงในฉากได้ (ยกเว้นการแก้ prefab)
public static class InventoryLayoutBuilder
{
    // ---------- ขนาด (หน่วยตาม Canvas Scaler 1920x1080) ----------

    private const float WindowWidth = 1500f;
    private const float WindowHeight = 860f;
    private const float Margin = 40f;
    private const float DetailWidth = 420f;
    private const float Gap = 20f;

    private const float TitleTop = 20f;
    private const float TitleHeight = 60f;
    private const float InfoTop = 90f;
    private const float InfoHeight = 40f;
    private const float TabTop = 140f;
    private const float TabHeight = 50f;
    private const float GridTop = 210f;

    private static readonly Vector2 CellSize = new Vector2(100f, 110f);
    private static readonly Vector2 CellSpacing = new Vector2(10f, 10f);

    private static readonly Color SelectedTabColor = new Color(1f, 0.85f, 0.4f, 1f);
    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.25f);
    private static readonly Color SelectionColor = new Color(1f, 0.85f, 0.3f, 0.45f);

    private static readonly string[] TabNames = { "Tab_Ingredients", "Tab_Dishes", "Tab_Recipes", "Tab_Equipment", "Tab_Accessory" };

    // ความกว้างฝั่งตาราง = หน้าต่าง - ขอบซ้ายขวา - แผงรายละเอียด - ช่องว่างตรงกลาง
    private static float GridWidth => WindowWidth - Margin * 2f - DetailWidth - Gap;

    [MenuItem("Tools/Second Serving/Build Inventory Window Layout")]
    public static void Build()
    {
        InventoryUI ui = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            EditorUtility.DisplayDialog("Inventory Layout", "No InventoryUI found in the open scene.", "OK");
            return;
        }

        SerializedObject so = new SerializedObject(ui);

        // หาตัวหน้าต่าง
        GameObject window = so.FindProperty("windowRoot").objectReferenceValue as GameObject;
        if (window == null)
        {
            Transform found = ui.transform.Find("InventoryWindow");
            window = found != null ? found.gameObject : null;
        }

        if (window == null)
        {
            EditorUtility.DisplayDialog("Inventory Layout", "InventoryWindow not found. Assign Window Root in InventoryUI first.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Build Inventory Window Layout");
        int undoGroup = Undo.GetCurrentGroup();
        Undo.RegisterFullObjectHierarchyUndo(window, "Build Inventory Window Layout");

        Transform root = window.transform;

        // 1) เอา Layout ที่คุมตำแหน่งลูกออก
        RemoveComponent<VerticalLayoutGroup>(window);
        RemoveComponent<HorizontalLayoutGroup>(window);
        RemoveComponent<ContentSizeFitter>(window);

        // 2) ตัวหน้าต่าง: กลางจอ ขนาดคงที่
        SetCentered(window.GetComponent<RectTransform>(), new Vector2(WindowWidth, WindowHeight));

        // 3) หัวข้อ: บนสุด กลาง
        TMP_Text title = FindOrCreateText(root, "TitleText", 40, TextAlignmentOptions.Center);
        SetTopStretch(title.rectTransform, Margin, Margin, TitleTop, TitleHeight);
        title.fontStyle = FontStyles.Bold;

        // 4) ความคืบหน้า: ซ้ายบน / ระดับกระเป๋า: ขวาบน
        TMP_Text progress = FindOrCreateText(root, "ProgressText", 24, TextAlignmentOptions.Left);
        SetTopLeft(progress.rectTransform, Margin, InfoTop, new Vector2(500f, InfoHeight));

        TMP_Text bagLevel = FindOrCreateText(root, "BagLevelText", 24, TextAlignmentOptions.Right);
        SetTopRight(bagLevel.rectTransform, Margin, InfoTop, new Vector2(500f, InfoHeight));

        // 5) แถบแท็บ: ใต้ข้อความ กว้างเท่าฝั่งตาราง
        Button[] tabs = BuildTabBar(root);

        // 6) Scroll View ของตาราง
        RectTransform content = BuildScrollView(root, out TMP_Text emptyText);

        // 7) ปิด SlotGrid เดิม
        Transform oldGrid = root.Find("SlotGrid");
        if (oldGrid != null && oldGrid.gameObject.activeSelf)
        {
            Undo.RecordObject(oldGrid.gameObject, "Disable SlotGrid");
            oldGrid.gameObject.SetActive(false);
        }

        // 8) แผงรายละเอียด
        GameObject detailPanel = BuildDetailPanel(root, out TMP_Text detailName, out TMP_Text detailBody);

        // 9) ผูกช่องใน InventoryUI
        so.FindProperty("windowRoot").objectReferenceValue = window;
        so.FindProperty("slotContainer").objectReferenceValue = content;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("progressText").objectReferenceValue = progress;
        so.FindProperty("bagLevelText").objectReferenceValue = bagLevel;
        so.FindProperty("emptyTabText").objectReferenceValue = emptyText;
        so.FindProperty("detailPanel").objectReferenceValue = detailPanel;
        so.FindProperty("detailNameText").objectReferenceValue = detailName;
        so.FindProperty("detailBodyText").objectReferenceValue = detailBody;

        SerializedProperty tabProp = so.FindProperty("tabButtons");
        tabProp.arraySize = tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
        {
            tabProp.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];
        }

        so.ApplyModifiedProperties();

        // 10) แก้ prefab ช่อง
        InventorySlotUI slotPrefab = so.FindProperty("slotPrefab").objectReferenceValue as InventorySlotUI;
        string prefabResult = slotPrefab != null ? FixSlotPrefab(slotPrefab) : "Slot Prefab is not assigned. Prefab was not changed.";

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);

        Debug.Log($"InventoryLayoutBuilder: Layout built. {prefabResult}");
        EditorUtility.DisplayDialog("Inventory Layout",
            "Inventory window layout is ready.\n\n" + prefabResult + "\n\nSave the scene with Ctrl+S.", "OK");
    }

    // ---------- ส่วนประกอบ ----------

    private static Button[] BuildTabBar(Transform root)
    {
        GameObject bar = FindOrCreate(root, "TabBar");
        SetTopLeft(bar.GetComponent<RectTransform>(), Margin, TabTop, new Vector2(GridWidth, TabHeight));

        HorizontalLayoutGroup layout = GetOrAdd<HorizontalLayoutGroup>(bar);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        Button[] result = new Button[TabNames.Length];
        for (int i = 0; i < TabNames.Length; i++)
        {
            Transform t = bar.transform.Find(TabNames[i]);
            if (t == null)
            {
                Debug.LogWarning($"InventoryLayoutBuilder: '{TabNames[i]}' not found under TabBar. Create it, then run this tool again.");
                continue;
            }

            t.SetSiblingIndex(i);
            Button button = t.GetComponent<Button>();
            if (button != null)
            {
                Undo.RecordObject(button, "Tab Color");
                ColorBlock colors = button.colors;
                colors.disabledColor = SelectedTabColor;
                button.colors = colors;
                result[i] = button;
            }
        }

        return result;
    }

    private static RectTransform BuildScrollView(Transform root, out TMP_Text emptyText)
    {
        GameObject scrollGo = FindOrCreate(root, "SlotScrollView");
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();

        // ยึดซ้าย ยืดเต็มความสูงที่เหลือ
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(0f, 1f);
        scrollRt.pivot = new Vector2(0f, 1f);
        scrollRt.offsetMin = new Vector2(Margin, Margin);
        scrollRt.offsetMax = new Vector2(Margin + GridWidth, -GridTop);

        Image bg = GetOrAdd<Image>(scrollGo);
        bg.color = new Color(0f, 0f, 0f, 0.15f);

        ScrollRect scroll = GetOrAdd<ScrollRect>(scrollGo);
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        // Viewport: ตัดส่วนที่เกินกรอบ
        GameObject viewportGo = FindOrCreate(scrollGo.transform, "Viewport");
        RectTransform viewportRt = viewportGo.GetComponent<RectTransform>();
        SetStretch(viewportRt, 0f, 0f, 0f, 0f);
        GetOrAdd<RectMask2D>(viewportGo);

        // Content: วางช่องเป็นตาราง สูงตามจำนวนช่อง
        GameObject contentGo = FindOrCreate(viewportGo.transform, "Content");
        RectTransform contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);

        GridLayoutGroup grid = GetOrAdd<GridLayoutGroup>(contentGo);
        grid.cellSize = CellSize;
        grid.spacing = CellSpacing;
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, Mathf.FloorToInt((GridWidth - 20f + CellSpacing.x) / (CellSize.x + CellSpacing.x)));

        ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(contentGo);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRt;
        scroll.content = contentRt;

        // ข้อความแท็บว่าง: วางบน Scroll View (ไม่อยู่ใน Content เพราะ Grid จะจับไปเป็นช่อง)
        emptyText = FindOrCreateText(scrollGo.transform, "EmptyTabText", 28, TextAlignmentOptions.Center);
        SetStretch(emptyText.rectTransform, 0f, 0f, 0f, 0f);
        emptyText.color = new Color(1f, 1f, 1f, 0.6f);
        emptyText.raycastTarget = false;
        emptyText.text = "Nothing here yet.";
        emptyText.transform.SetAsLastSibling();

        return contentRt;
    }

    private static GameObject BuildDetailPanel(Transform root, out TMP_Text nameText, out TMP_Text bodyText)
    {
        GameObject panel = FindOrCreate(root, "DetailPanel");
        RectTransform rt = panel.GetComponent<RectTransform>();

        // ยึดขวา ยืดเต็มความสูงตั้งแต่ระดับแถบแท็บลงไป
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(-(Margin + DetailWidth), Margin);
        rt.offsetMax = new Vector2(-Margin, -TabTop);

        Image bg = GetOrAdd<Image>(panel);
        bg.color = PanelColor;

        nameText = FindOrCreateText(panel.transform, "DetailName", 32, TextAlignmentOptions.TopLeft);
        SetTopStretch(nameText.rectTransform, 20f, 20f, 20f, 60f);
        nameText.fontStyle = FontStyles.Bold;

        bodyText = FindOrCreateText(panel.transform, "DetailBody", 22, TextAlignmentOptions.TopLeft);
        SetStretch(bodyText.rectTransform, 20f, 20f, 90f, 20f);
        bodyText.textWrappingMode = TextWrappingModes.Normal;

        return panel;
    }

    // แก้ prefab ช่อง: ตัวเลขมุมขวาบน + กรอบเลือก
    private static string FixSlotPrefab(InventorySlotUI prefab)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path))
        {
            return "Slot Prefab path not found. Prefab was not changed.";
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            InventorySlotUI slot = contents.GetComponent<InventorySlotUI>();
            if (slot == null)
            {
                return "InventorySlotUI not found on the prefab root. Prefab was not changed.";
            }

            SerializedObject so = new SerializedObject(slot);

            // ตัวเลขจำนวน: มุมขวาบน
            TMP_Text count = so.FindProperty("countText").objectReferenceValue as TMP_Text;
            if (count != null)
            {
                RectTransform crt = count.rectTransform;
                crt.anchorMin = new Vector2(1f, 1f);
                crt.anchorMax = new Vector2(1f, 1f);
                crt.pivot = new Vector2(1f, 1f);
                crt.anchoredPosition = new Vector2(-6f, -4f);
                crt.sizeDelta = new Vector2(70f, 30f);
                count.alignment = TextAlignmentOptions.TopRight;
                count.fontSize = 20;
                count.raycastTarget = false;
            }

            // กรอบเลือก: อยู่หลังไอคอน ปิดไว้ก่อน
            Transform frameT = contents.transform.Find("SelectionFrame");
            GameObject frame;
            if (frameT == null)
            {
                frame = new GameObject("SelectionFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                frame.transform.SetParent(contents.transform, false);
            }
            else
            {
                frame = frameT.gameObject;
            }

            SetStretch(frame.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            Image frameImage = frame.GetComponent<Image>();
            frameImage.color = SelectionColor;
            frameImage.raycastTarget = false;
            frame.transform.SetAsFirstSibling();
            frame.SetActive(false);

            so.FindProperty("selectionFrame").objectReferenceValue = frame;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            return $"Slot prefab updated ({path}).";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ---------- ตัวช่วยจัดตำแหน่ง ----------

    private static void SetCentered(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }

    // ยืดเต็มความกว้าง ยึดด้านบน (left/right = ระยะขอบ, top = ระยะจากบน, height = ความสูง)
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

    private static void SetTopRight(RectTransform rt, float right, float top, Vector2 size)
    {
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-right, -top);
        rt.sizeDelta = size;
    }

    // ยืดเต็มกรอบแม่ โดยเว้นขอบแต่ละด้าน
    private static void SetStretch(RectTransform rt, float left, float right, float top, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    // ---------- ตัวช่วยสร้าง/หาวัตถุ ----------

    // หาลูกตามชื่อ ถ้าไม่มีให้สร้างใหม่ (มี RectTransform)
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
        bool isNew = text == null;

        if (isNew)
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

    private static void RemoveComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component != null)
        {
            Undo.DestroyObjectImmediate(component);
        }
    }
}
