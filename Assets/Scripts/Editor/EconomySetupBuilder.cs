using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// [2.6] เมนู: Tools > Second Serving > Build Economy
// สร้างระบบเงิน + ก่อสร้างครบชุด กดซ้ำได้ ส่วนที่มีอยู่แล้วไม่ถูกเขียนทับ (kept):
// - PlayerWallet บน Player
// - วัตถุ ConstructionManager + งานอัปเกรดร้าน Assets/Construction/shop_upgrade_2/3/4 (ถ้ายังไม่มี) + เติมงานทุกไฟล์ที่ยังไม่อยู่ในรายการ
// - prefab Assets/Prefabs/BuilderCounter.prefab + วางเคาน์เตอร์ช่าง 1 ตัว ซ้าย OutsideFront 3 หน่วย (ถ้ายังไม่มี)
// - หน้าต่าง ConstructionUI บน Canvas หลัก (ถ้ายังไม่มี)
// - เงินบนจอ MoneyPanel ใต้ ClockPanel (แยกจากนาฬิกา เพราะนาฬิกาถูกซ่อนตอนเปิดร้าน) (ถ้ายังไม่มี)
// - ผูก PlayerWallet / ConstructionManager ให้ SaveManager (ถ้าช่องยังว่าง)
public static class EconomySetupBuilder
{
    private const string SquarePath = "Assets/Prefabs/Placeholder/WhiteSquare.png";
    private const string CounterPath = "Assets/Prefabs/BuilderCounter.prefab";
    private const string ConstructionFolder = "Assets/Construction";

    private static readonly Color CounterColor = new Color(0.62f, 0.45f, 0.28f, 1f);
    private static readonly Color HammerColor = new Color(0.85f, 0.75f, 0.35f, 1f);
    private static readonly Color WindowColor = new Color(0.08f, 0.08f, 0.1f, 0.94f);
    private static readonly Color BarBackColor = new Color(1f, 1f, 1f, 0.12f);
    private static readonly Color BarFillColor = new Color(1f, 0.76f, 0.25f, 1f);
    private static readonly Color ButtonColor = new Color(0.25f, 0.25f, 0.3f, 1f);
    private static readonly Color HireColor = new Color(0.25f, 0.5f, 0.3f, 1f);
    private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.1f, 0.75f);

    [MenuItem("Tools/Second Serving/Build Economy")]
    public static void Build()
    {
        List<string> report = new List<string>();

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(ConstructionFolder)) AssetDatabase.CreateFolder("Assets", "Construction");

        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        if (square == null) report.Add($"{SquarePath} not found: the counter will have no sprite.");

        EnsureShopUpgrades(report);
        BuilderCounter counterPrefab = CreateCounterPrefab(square, report);

        Undo.SetCurrentGroupName("Build Economy");
        int undoGroup = Undo.GetCurrentGroup();

        PlayerWallet wallet = EnsureWallet(report);
        ConstructionManager manager = EnsureManager(report);
        FillProjects(manager, report);
        PlaceCounter(counterPrefab, report);

        Canvas canvas = FindMainCanvas();
        if (canvas == null)
        {
            report.Add("No Canvas found: UI was not built.");
        }
        else
        {
            BuildMoneyPanel(canvas, report);
            BuildConstructionWindow(canvas, report);
        }

        LinkSaveManager(wallet, manager, report);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        string summary = string.Join("\n", report);
        Debug.Log($"EconomySetupBuilder: {summary.Replace('\n', ' ')}");
        EditorUtility.DisplayDialog("Economy", summary + "\n\nSave the scene with Ctrl+S.", "OK");
    }

    // ---------- ระบบ ----------

    private static PlayerWallet EnsureWallet(List<string> report)
    {
        PlayerWallet existing = Object.FindFirstObjectByType<PlayerWallet>(FindObjectsInactive.Include);
        if (existing != null)
        {
            report.Add("PlayerWallet already on the Player (kept).");
            return existing;
        }

        PlayerInteractor player = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        if (player == null)
        {
            report.Add("Player (PlayerInteractor) not found: PlayerWallet was not added.");
            return null;
        }

        PlayerWallet wallet = Undo.AddComponent<PlayerWallet>(player.gameObject);
        report.Add("Added PlayerWallet to the Player.");
        return wallet;
    }

    private static ConstructionManager EnsureManager(List<string> report)
    {
        ConstructionManager existing = Object.FindFirstObjectByType<ConstructionManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            report.Add("ConstructionManager already in the scene (kept).");
            return existing;
        }

        GameObject go = new GameObject("ConstructionManager");
        Undo.RegisterCreatedObjectUndo(go, "Create ConstructionManager");
        ConstructionManager manager = go.AddComponent<ConstructionManager>();
        report.Add("Created ConstructionManager.");
        return manager;
    }

    // งานอัปเกรดร้าน 3 ขั้น (ผู้ใช้ตั้ง 20,000 / 75,000 / 150,000 · 1 / 2 / 3 วัน)
    private static void EnsureShopUpgrades(List<string> report)
    {
        int created = 0;
        created += CreateUpgrade("shop_upgrade_2", "Shop Upgrade (Level 2)", "Expand the kitchen and dining room.", 20000, 1, 1) ? 1 : 0;
        created += CreateUpgrade("shop_upgrade_3", "Shop Upgrade (Level 3)", "A bigger, cozier restaurant.", 75000, 2, 2) ? 1 : 0;
        created += CreateUpgrade("shop_upgrade_4", "Shop Upgrade (Level 4)", "The finest restaurant in the countryside.", 150000, 3, 3) ? 1 : 0;
        report.Add(created > 0 ? $"Created {created} shop upgrade project(s) in {ConstructionFolder}." : "Shop upgrade projects already exist (kept).");
    }

    private static bool CreateUpgrade(string id, string displayName, string description, int money, int days, int requiredLevel)
    {
        string path = $"{ConstructionFolder}/{id}.asset";
        if (AssetDatabase.LoadAssetAtPath<ConstructionProject>(path) != null)
        {
            return false;
        }

        ConstructionProject project = ScriptableObject.CreateInstance<ConstructionProject>();
        project.projectId = id;
        project.displayName = displayName;
        project.description = description;
        project.money = money;
        project.days = days;
        project.requiredShopLevel = requiredLevel;
        project.requiresFullReputation = true;
        project.effect = ConstructionEffect.ShopLevelUp;
        AssetDatabase.CreateAsset(project, path);
        AssetDatabase.SaveAssets();
        return true;
    }

    // ใส่ ConstructionProject ทุกไฟล์ที่ยังไม่อยู่ในรายการ (ไม่ลบของเดิม)
    private static void FillProjects(ConstructionManager manager, List<string> report)
    {
        if (manager == null) return;

        SerializedObject so = new SerializedObject(manager);
        SerializedProperty list = so.FindProperty("projects");
        HashSet<Object> existing = new HashSet<Object>();
        for (int i = 0; i < list.arraySize; i++)
        {
            Object item = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (item != null) existing.Add(item);
        }

        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ConstructionProject"))
        {
            ConstructionProject project = AssetDatabase.LoadAssetAtPath<ConstructionProject>(AssetDatabase.GUIDToAssetPath(guid));
            if (project == null || existing.Contains(project)) continue;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = project;
            added++;
        }

        if (added > 0)
        {
            so.ApplyModifiedProperties();
            report.Add($"Added {added} project(s) to ConstructionManager.");
        }
        else
        {
            report.Add("ConstructionManager projects are up to date.");
        }
    }

    private static void LinkSaveManager(PlayerWallet wallet, ConstructionManager manager, List<string> report)
    {
        SaveManager save = Object.FindFirstObjectByType<SaveManager>(FindObjectsInactive.Include);
        if (save == null)
        {
            report.Add("SaveManager not found: money and construction will not be saved.");
            return;
        }

        SerializedObject so = new SerializedObject(save);
        bool changed = false;
        SerializedProperty walletProp = so.FindProperty("playerWallet");
        if (walletProp != null && walletProp.objectReferenceValue == null && wallet != null)
        {
            walletProp.objectReferenceValue = wallet;
            changed = true;
        }
        SerializedProperty managerProp = so.FindProperty("constructionManager");
        if (managerProp != null && managerProp.objectReferenceValue == null && manager != null)
        {
            managerProp.objectReferenceValue = manager;
            changed = true;
        }

        if (changed)
        {
            so.ApplyModifiedProperties();
            report.Add("Linked PlayerWallet / ConstructionManager to SaveManager.");
        }
        else
        {
            report.Add("SaveManager links already set (kept).");
        }
    }

    // ---------- เคาน์เตอร์ช่าง ----------

    private static BuilderCounter CreateCounterPrefab(Sprite square, List<string> report)
    {
        BuilderCounter existing = AssetDatabase.LoadAssetAtPath<BuilderCounter>(CounterPath);
        if (existing != null)
        {
            report.Add("BuilderCounter prefab already exists (kept as is).");
            return existing;
        }

        GameObject root = new GameObject("BuilderCounter");
        try
        {
            GameObject body = new GameObject("Counter");
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(1.6f, 0.6f, 1f);
            SpriteRenderer bodySr = body.AddComponent<SpriteRenderer>();
            bodySr.sprite = square;
            bodySr.color = CounterColor;
            bodySr.sortingOrder = 2;

            // ค้อนบนเคาน์เตอร์ (ให้รู้ว่าเป็นของช่าง)
            GameObject hammer = new GameObject("Hammer");
            hammer.transform.SetParent(root.transform, false);
            hammer.transform.localPosition = new Vector3(0.4f, 0.12f, 0f);
            hammer.transform.localScale = new Vector3(0.35f, 0.15f, 1f);
            SpriteRenderer hammerSr = hammer.AddComponent<SpriteRenderer>();
            hammerSr.sprite = square;
            hammerSr.color = HammerColor;
            hammerSr.sortingOrder = 3;

            BoxCollider2D col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = new Vector2(1.6f, 0.6f);

            GameObject label = new GameObject("Label");
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            TextMeshPro text = label.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.text = "";
            text.fontSize = 3f;
            text.alignment = TextAlignmentOptions.Bottom;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(4f, 1f);
            text.rectTransform.pivot = new Vector2(0.5f, 0f);
            text.sortingOrder = 30;

            BuilderCounter counter = root.AddComponent<BuilderCounter>();
            SerializedObject so = new SerializedObject(counter);
            so.FindProperty("labelText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, CounterPath);
            report.Add(saved != null ? $"Created {CounterPath}." : "Failed to create the BuilderCounter prefab.");
            return saved != null ? saved.GetComponent<BuilderCounter>() : null;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void PlaceCounter(BuilderCounter prefab, List<string> report)
    {
        int count = Object.FindObjectsByType<BuilderCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        if (count > 0)
        {
            report.Add($"{count} builder counter(s) already in the scene (kept).");
            return;
        }
        if (prefab == null)
        {
            report.Add("No BuilderCounter prefab: place one yourself.");
            return;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
        Undo.RegisterCreatedObjectUndo(go, "Place BuilderCounter");

        GameObject anchor = GameObject.Find("OutsideFront");
        if (anchor != null)
        {
            go.transform.position = anchor.transform.position + new Vector3(-3f, 0f, 0f);
            report.Add("Placed a BuilderCounter 3 units left of OutsideFront: move it into the builder's house later.");
        }
        else
        {
            report.Add("OutsideFront not found: placed a BuilderCounter at (0, 0).");
        }
    }

    // ---------- เงินบนจอ ----------

    private static void BuildMoneyPanel(Canvas canvas, List<string> report)
    {
        if (Object.FindFirstObjectByType<MoneyUI>(FindObjectsInactive.Include) != null)
        {
            report.Add("MoneyUI already exists (kept).");
            return;
        }

        GameObject panel = CreateUI(canvas.transform, "MoneyPanel");
        RectTransform rt = panel.GetComponent<RectTransform>();

        // วางใต้ ClockPanel (ใช้ anchor เดียวกัน) ไม่เจอ = มุมขวาบน
        Transform clock = FindDeep(canvas.transform, "ClockPanel");
        RectTransform clockRt = clock != null ? clock as RectTransform : null;
        if (clockRt != null)
        {
            rt.anchorMin = clockRt.anchorMin;
            rt.anchorMax = clockRt.anchorMax;
            rt.pivot = clockRt.pivot;
            float clockHeight = clockRt.rect.height > 0f ? clockRt.rect.height : clockRt.sizeDelta.y;
            float width = clockRt.rect.width > 0f ? clockRt.rect.width : 260f;
            rt.sizeDelta = new Vector2(width, 44f);
            // pivot เดียวกับนาฬิกา: เลื่อนลง = ส่วนของนาฬิกาใต้ pivot + ช่องว่าง 8 + ส่วนของแผงเงินเหนือ pivot
            float pivotY = clockRt.pivot.y;
            rt.anchoredPosition = clockRt.anchoredPosition + new Vector2(0f, -(clockHeight * pivotY + 8f + 44f * (1f - pivotY)));
            report.Add("Built MoneyPanel under ClockPanel (move it if it overlaps).");
        }
        else
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -150f);
            rt.sizeDelta = new Vector2(260f, 44f);
            report.Add("ClockPanel not found: built MoneyPanel at the top right.");
        }

        Image bg = panel.AddComponent<Image>();
        bg.color = PanelColor;
        bg.raycastTarget = false;

        TMP_Text text = CreateText(panel.transform, "MoneyText", 28, TextAlignmentOptions.Center);
        SetStretch(text.rectTransform, 8f, 8f, 0f, 0f);
        text.text = "B 500";
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;

        MoneyUI ui = panel.AddComponent<MoneyUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("moneyText").objectReferenceValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- หน้าต่างช่าง ----------

    private static void BuildConstructionWindow(Canvas canvas, List<string> report)
    {
        if (Object.FindFirstObjectByType<ConstructionUI>(FindObjectsInactive.Include) != null)
        {
            report.Add("ConstructionUI already exists (kept, layout not touched).");
            return;
        }

        GameObject host = CreateUI(canvas.transform, "ConstructionUI");
        SetStretch(host.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        GameObject window = CreateUI(host.transform, "BuilderWindow");
        RectTransform windowRt = window.GetComponent<RectTransform>();
        windowRt.anchorMin = new Vector2(0.5f, 0.5f);
        windowRt.anchorMax = new Vector2(0.5f, 0.5f);
        windowRt.pivot = new Vector2(0.5f, 0.5f);
        windowRt.anchoredPosition = new Vector2(0f, 30f);
        windowRt.sizeDelta = new Vector2(820f, 560f);
        window.AddComponent<Image>().color = WindowColor;

        TMP_Text title = CreateText(window.transform, "TitleText", 36, TextAlignmentOptions.Center);
        SetTopStretch(title.rectTransform, 20f, 20f, 18f, 46f);
        title.text = "Builder";
        title.fontStyle = FontStyles.Bold;

        TMP_Text level = CreateText(window.transform, "LevelText", 28, TextAlignmentOptions.Left);
        SetTopStretch(level.rectTransform, 48f, 48f, 84f, 38f);
        level.fontStyle = FontStyles.Bold;

        TMP_Text repText = CreateText(window.transform, "ReputationText", 22, TextAlignmentOptions.Left);
        SetTopStretch(repText.rectTransform, 48f, 48f, 136f, 30f);

        GameObject barBack = CreateUI(window.transform, "ReputationBar");
        SetTopStretch(barBack.GetComponent<RectTransform>(), 48f, 48f, 170f, 22f);
        Image backImage = barBack.AddComponent<Image>();
        backImage.color = BarBackColor;
        backImage.raycastTarget = false;

        GameObject fill = CreateUI(barBack.transform, "Fill");
        RectTransform fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0.5f, 1f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = BarFillColor;
        fillImage.raycastTarget = false;

        TMP_Text cost = CreateText(window.transform, "CostText", 22, TextAlignmentOptions.Left);
        SetTopStretch(cost.rectTransform, 48f, 48f, 214f, 30f);
        TMP_Text time = CreateText(window.transform, "TimeText", 22, TextAlignmentOptions.Left);
        SetTopStretch(time.rectTransform, 48f, 48f, 250f, 30f);
        TMP_Text unlock = CreateText(window.transform, "UnlockText", 22, TextAlignmentOptions.Left);
        SetTopStretch(unlock.rectTransform, 48f, 48f, 286f, 60f);
        unlock.textWrappingMode = TextWrappingModes.Normal;

        TMP_Text status = CreateText(window.transform, "StatusText", 22, TextAlignmentOptions.Center);
        SetTopStretch(status.rectTransform, 48f, 48f, 366f, 60f);
        status.textWrappingMode = TextWrappingModes.Normal;

        Button hire = BuildButton(window.transform, "HireButton", "Hire", HireColor, 26);
        PlaceBottom(hire.GetComponent<RectTransform>(), -130f, new Vector2(220f, 56f));
        Button close = BuildButton(window.transform, "CloseButton", "Close", ButtonColor, 26);
        PlaceBottom(close.GetComponent<RectTransform>(), 130f, new Vector2(220f, 56f));

        // กล่องยืนยัน Yes / No (บังทั้งหน้าต่าง)
        GameObject confirm = CreateUI(window.transform, "ConfirmPanel");
        SetStretch(confirm.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        confirm.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        GameObject box = CreateUI(confirm.transform, "Box");
        RectTransform boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(600f, 260f);
        box.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 1f);

        TMP_Text confirmText = CreateText(box.transform, "ConfirmText", 26, TextAlignmentOptions.Center);
        SetStretch(confirmText.rectTransform, 30f, 30f, 24f, 100f);
        confirmText.textWrappingMode = TextWrappingModes.Normal;

        Button yes = BuildButton(box.transform, "YesButton", "Yes", HireColor, 26);
        PlaceBottom(yes.GetComponent<RectTransform>(), -110f, new Vector2(180f, 54f));
        Button no = BuildButton(box.transform, "NoButton", "No", ButtonColor, 26);
        PlaceBottom(no.GetComponent<RectTransform>(), 110f, new Vector2(180f, 54f));
        confirm.SetActive(false);

        ConstructionUI ui = host.AddComponent<ConstructionUI>();
        SerializedObject so = new SerializedObject(ui);
        PlayerInteractor player = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        if (player != null) so.FindProperty("playerController").objectReferenceValue = player.GetComponent<PlayerController>();
        so.FindProperty("windowRoot").objectReferenceValue = window;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("levelText").objectReferenceValue = level;
        so.FindProperty("reputationFill").objectReferenceValue = fillRt;
        so.FindProperty("reputationText").objectReferenceValue = repText;
        so.FindProperty("costText").objectReferenceValue = cost;
        so.FindProperty("timeText").objectReferenceValue = time;
        so.FindProperty("unlockText").objectReferenceValue = unlock;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("hireButton").objectReferenceValue = hire;
        so.FindProperty("closeButton").objectReferenceValue = close;
        so.FindProperty("confirmPanel").objectReferenceValue = confirm;
        so.FindProperty("confirmText").objectReferenceValue = confirmText;
        so.FindProperty("yesButton").objectReferenceValue = yes;
        so.FindProperty("noButton").objectReferenceValue = no;
        so.ApplyModifiedPropertiesWithoutUndo();

        window.SetActive(false);
        host.transform.SetAsLastSibling();
        report.Add($"Built ConstructionUI on '{canvas.name}'.");
    }

    // ---------- ตัวช่วย ----------

    private static void PlaceBottom(RectTransform rt, float x, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, 28f);
        rt.sizeDelta = size;
    }

    private static Button BuildButton(Transform parent, string name, string label, Color color, float fontSize)
    {
        GameObject go = CreateUI(parent, name);
        Image bg = go.AddComponent<Image>();
        bg.color = color;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = bg;

        TMP_Text text = CreateText(go.transform, "Label", fontSize, TextAlignmentOptions.Center);
        SetStretch(text.rectTransform, 0f, 0f, 0f, 0f);
        text.text = label;
        text.raycastTarget = false;
        return button;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child;
        }
        return null;
    }

    private static Canvas FindMainCanvas()
    {
        InventoryUI inventoryUI = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        if (inventoryUI != null)
        {
            Canvas c = inventoryUI.GetComponentInParent<Canvas>(true);
            if (c != null) return c.rootCanvas;
        }
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.isRootCanvas) return c;
        }
        return null;
    }

    private static GameObject CreateUI(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        return go;
    }

    private static TMP_Text CreateText(Transform parent, string name, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = CreateUI(parent, name);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = "";
        text.fontSize = fontSize;
        text.alignment = alignment;
        return text;
    }

    private static void SetTopStretch(RectTransform rt, float left, float right, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -(top + height));
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static void SetStretch(RectTransform rt, float left, float right, float top, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }
}
