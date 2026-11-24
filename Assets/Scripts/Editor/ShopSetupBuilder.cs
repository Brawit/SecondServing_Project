using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// [2.6b] เมนู: Tools > Second Serving > Build Shops
// สร้างระบบร้านค้า + กล่องขายข้ามคืนครบชุด กดซ้ำได้ ส่วนที่มีอยู่แล้วไม่ถูกเขียนทับ (kept):
// - รายการร้าน Assets/Shops/GeneralStore, BagShop, Bookstore (ถ้ายังไม่มีไฟล์)
// - prefab Assets/Prefabs/ShopKeeperCounter.prefab + ShippingBox.prefab
// - วางเคาน์เตอร์ร้าน 3 ร้าน (ซ้ายเคาน์เตอร์ช่าง) + กล่องขาย 1 กล่อง (ขวาตู้จดหมาย) ถ้าฉากยังไม่มีร้านนั้น/กล่อง
// - วัตถุ SalesLedger
// - หน้าต่าง ShopUI + ShippingBoxUI บน Canvas หลัก (ถ้ายังไม่มี / อยากได้ layout ใหม่ = ลบวัตถุ ShopUI / ShippingBoxUI ใน Canvas แล้วกดซ้ำ)
// - ตั้งเพดานกระเป๋าใน Inventory บน Player เป็น {30, 150, 500} (ตัดสินแล้ว 2.6b ตั้งทับทุกครั้งที่ยังไม่ตรง)
public static class ShopSetupBuilder
{
    private const string SquarePath = "Assets/Prefabs/Placeholder/WhiteSquare.png";
    private const string ShopsFolder = "Assets/Shops";
    private const string CounterPrefabPath = "Assets/Prefabs/ShopKeeperCounter.prefab";
    private const string BoxPrefabPath = "Assets/Prefabs/ShippingBox.prefab";
    private const string GeneralStorePath = ShopsFolder + "/GeneralStore.asset";
    private const string BagShopPath = ShopsFolder + "/BagShop.asset";
    private const string BookstorePath = ShopsFolder + "/Bookstore.asset";

    private static readonly int[] BagCaps = { 30, 150, 500 };

    // วัตถุดิบ 34 ชนิด: สูตรเริ่มต้น 8 สูตร (18) + สูตรในสมุด 9 เล่ม (16)
    private static readonly string[] GeneralStoreItems =
    {
        "green_papaya", "tomato", "chili", "peanut", "chicken", "bread", "basil", "cabbage", "flour", "egg",
        "pork", "corn", "glutinous_rice", "mango", "coconut", "lemon", "seasonal_fruit", "lime",
        "rice", "ginger", "cucumber", "thai_tea", "milk", "beef", "curry_roux", "carrot", "matcha",
        "spring_onion", "soybean", "coffee", "maple_syrup", "tortilla", "cheese", "bell_pepper"
    };

    private static readonly Color CounterColor = new Color(0.45f, 0.62f, 0.4f, 1f);
    private static readonly Color AwningColor = new Color(0.9f, 0.85f, 0.75f, 1f);
    private static readonly Color BoxColor = new Color(0.55f, 0.38f, 0.22f, 1f);
    private static readonly Color BoxLidColor = new Color(0.35f, 0.24f, 0.14f, 1f);
    private static readonly Color WindowColor = new Color(0.08f, 0.08f, 0.1f, 0.94f);
    private static readonly Color ListBackColor = new Color(1f, 1f, 1f, 0.05f);
    private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color ButtonColor = new Color(0.25f, 0.25f, 0.3f, 1f);
    private static readonly Color BuyColor = new Color(0.25f, 0.5f, 0.3f, 1f);
    private static readonly Color SelectColor = new Color(1f, 0.8f, 0.3f, 0.25f);

    [MenuItem("Tools/Second Serving/Build Shops")]
    public static void Build()
    {
        List<string> report = new List<string>();

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(ShopsFolder)) AssetDatabase.CreateFolder("Assets", "Shops");

        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        if (square == null) report.Add($"{SquarePath} not found: counters will have no sprite.");

        ShopCatalog general = EnsureGeneralStore(report);
        ShopCatalog bag = EnsureBagShop(report);
        ShopCatalog books = EnsureBookstore(report);

        ShopKeeperCounter counterPrefab = CreateCounterPrefab(square, report);
        ShippingBox boxPrefab = CreateBoxPrefab(square, report);

        Undo.SetCurrentGroupName("Build Shops");
        int undoGroup = Undo.GetCurrentGroup();

        EnsureLedger(report);
        SetBagCaps(report);
        PlaceCounters(counterPrefab, new[] { general, bag, books }, report);
        PlaceBox(boxPrefab, report);

        Canvas canvas = FindMainCanvas();
        if (canvas == null)
        {
            report.Add("No Canvas found: UI was not built.");
        }
        else
        {
            BuildShopWindow(canvas, report);
            BuildBoxWindow(canvas, report);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        string summary = string.Join("\n", report);
        Debug.Log($"ShopSetupBuilder: {summary.Replace('\n', ' ')}");
        EditorUtility.DisplayDialog("Shops", summary + "\n\nSave the scene with Ctrl+S.", "OK");
    }

    // ---------- รายการร้าน ----------

    private static ShopCatalog EnsureGeneralStore(List<string> report)
    {
        ShopCatalog existing = AssetDatabase.LoadAssetAtPath<ShopCatalog>(GeneralStorePath);
        if (existing != null)
        {
            report.Add("GeneralStore catalog already exists (kept).");
            return existing;
        }

        Dictionary<string, ItemData> items = new Dictionary<string, ItemData>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && !string.IsNullOrEmpty(item.itemId) && !items.ContainsKey(item.itemId)) items.Add(item.itemId, item);
        }

        ShopCatalog catalog = ScriptableObject.CreateInstance<ShopCatalog>();
        catalog.shopName = "General Store";
        catalog.buysFromPlayer = true;
        List<string> missing = new List<string>();
        foreach (string id in GeneralStoreItems)
        {
            if (items.TryGetValue(id, out ItemData item)) catalog.entries.Add(new ShopEntry { kind = ShopEntryKind.Item, item = item });
            else missing.Add(id);
        }

        AssetDatabase.CreateAsset(catalog, GeneralStorePath);
        AssetDatabase.SaveAssets();
        report.Add($"Created {GeneralStorePath} ({catalog.entries.Count} items).");
        if (missing.Count > 0) report.Add($"Items not found (run Import Recipes & Ingredients?): {string.Join(", ", missing)}");
        return catalog;
    }

    private static ShopCatalog EnsureBagShop(List<string> report)
    {
        ShopCatalog existing = AssetDatabase.LoadAssetAtPath<ShopCatalog>(BagShopPath);
        if (existing != null)
        {
            report.Add("BagShop catalog already exists (kept).");
            return existing;
        }

        ShopCatalog catalog = ScriptableObject.CreateInstance<ShopCatalog>();
        catalog.shopName = "Bag Shop";
        catalog.buysFromPlayer = false;
        catalog.entries.Add(new ShopEntry { kind = ShopEntryKind.BagUpgrade, bagLevel = 2, priceOverride = 2500 });
        catalog.entries.Add(new ShopEntry { kind = ShopEntryKind.BagUpgrade, bagLevel = 3, priceOverride = 20000 });

        AssetDatabase.CreateAsset(catalog, BagShopPath);
        AssetDatabase.SaveAssets();
        report.Add($"Created {BagShopPath} (level 2 = 2,500 / level 3 = 20,000).");
        return catalog;
    }

    private static ShopCatalog EnsureBookstore(List<string> report)
    {
        ShopCatalog existing = AssetDatabase.LoadAssetAtPath<ShopCatalog>(BookstorePath);
        if (existing != null)
        {
            report.Add("Bookstore catalog already exists (kept).");
            return existing;
        }

        List<RecipeData> books = new List<RecipeData>();
        foreach (string guid in AssetDatabase.FindAssets("t:RecipeData"))
        {
            RecipeData recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(AssetDatabase.GUIDToAssetPath(guid));
            if (recipe != null && recipe.obtainSource == RecipeSource.ShopBook && !books.Contains(recipe)) books.Add(recipe);
        }
        books.Sort((a, b) => a.sellPrice.CompareTo(b.sellPrice));

        ShopCatalog catalog = ScriptableObject.CreateInstance<ShopCatalog>();
        catalog.shopName = "Bookstore";
        catalog.buysFromPlayer = false;
        foreach (RecipeData recipe in books)
        {
            catalog.entries.Add(new ShopEntry { kind = ShopEntryKind.RecipeBook, recipe = recipe });
        }

        AssetDatabase.CreateAsset(catalog, BookstorePath);
        AssetDatabase.SaveAssets();
        report.Add($"Created {BookstorePath} ({books.Count} recipe books, price = dish price × 10).");
        if (books.Count != 9) report.Add($"Expected 9 Shop Book recipes but found {books.Count}. Check the Obtain column and re-import.");
        return catalog;
    }

    // ---------- ระบบในฉาก ----------

    private static void EnsureLedger(List<string> report)
    {
        if (Object.FindFirstObjectByType<SalesLedger>(FindObjectsInactive.Include) != null)
        {
            report.Add("SalesLedger already in the scene (kept).");
            return;
        }

        GameObject go = new GameObject("SalesLedger");
        Undo.RegisterCreatedObjectUndo(go, "Create SalesLedger");
        go.AddComponent<SalesLedger>();
        report.Add("Created SalesLedger.");
    }

    private static void SetBagCaps(List<string> report)
    {
        PlayerInteractor player = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        Inventory inventory = player != null ? player.GetComponent<Inventory>() : null;
        if (inventory == null)
        {
            report.Add("Player Inventory not found: bag caps were not set.");
            return;
        }

        SerializedObject so = new SerializedObject(inventory);
        SerializedProperty caps = so.FindProperty("stackCapsByLevel");
        bool same = caps.arraySize == BagCaps.Length;
        for (int i = 0; same && i < BagCaps.Length; i++) same = caps.GetArrayElementAtIndex(i).intValue == BagCaps[i];
        if (same)
        {
            report.Add("Bag caps already 30 / 150 / 500 (kept).");
            return;
        }

        caps.arraySize = BagCaps.Length;
        for (int i = 0; i < BagCaps.Length; i++) caps.GetArrayElementAtIndex(i).intValue = BagCaps[i];
        SerializedProperty start = so.FindProperty("startingBagLevel");
        if (start != null) start.intValue = Mathf.Clamp(start.intValue, 1, BagCaps.Length);
        so.ApplyModifiedProperties();
        report.Add("Set bag caps on the Player Inventory to 30 / 150 / 500.");
    }

    // ---------- prefab ----------

    private static ShopKeeperCounter CreateCounterPrefab(Sprite square, List<string> report)
    {
        ShopKeeperCounter existing = AssetDatabase.LoadAssetAtPath<ShopKeeperCounter>(CounterPrefabPath);
        if (existing != null)
        {
            report.Add("ShopKeeperCounter prefab already exists (kept as is).");
            return existing;
        }

        GameObject root = new GameObject("ShopKeeperCounter");
        try
        {
            AddSprite(root.transform, "Counter", square, CounterColor, new Vector3(0f, 0f, 0f), new Vector3(1.6f, 0.6f, 1f), 2);
            AddSprite(root.transform, "Awning", square, AwningColor, new Vector3(0f, 0.38f, 0f), new Vector3(1.8f, 0.16f, 1f), 3);

            BoxCollider2D col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = new Vector2(1.6f, 0.6f);

            TextMeshPro text = AddWorldLabel(root.transform, 0.6f);

            ShopKeeperCounter counter = root.AddComponent<ShopKeeperCounter>();
            SerializedObject so = new SerializedObject(counter);
            so.FindProperty("labelText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, CounterPrefabPath);
            report.Add(saved != null ? $"Created {CounterPrefabPath}." : "Failed to create the ShopKeeperCounter prefab.");
            return saved != null ? saved.GetComponent<ShopKeeperCounter>() : null;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static ShippingBox CreateBoxPrefab(Sprite square, List<string> report)
    {
        ShippingBox existing = AssetDatabase.LoadAssetAtPath<ShippingBox>(BoxPrefabPath);
        if (existing != null)
        {
            report.Add("ShippingBox prefab already exists (kept as is).");
            return existing;
        }

        GameObject root = new GameObject("ShippingBox");
        try
        {
            AddSprite(root.transform, "Box", square, BoxColor, Vector3.zero, new Vector3(0.9f, 0.7f, 1f), 2);
            AddSprite(root.transform, "Lid", square, BoxLidColor, new Vector3(0f, 0.3f, 0f), new Vector3(1f, 0.14f, 1f), 3);

            BoxCollider2D col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = new Vector2(0.9f, 0.7f);

            TextMeshPro text = AddWorldLabel(root.transform, 0.55f);

            ShippingBox box = root.AddComponent<ShippingBox>();
            SerializedObject so = new SerializedObject(box);
            so.FindProperty("labelText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, BoxPrefabPath);
            report.Add(saved != null ? $"Created {BoxPrefabPath}." : "Failed to create the ShippingBox prefab.");
            return saved != null ? saved.GetComponent<ShippingBox>() : null;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void AddSprite(Transform parent, string name, Sprite sprite, Color color, Vector3 position, Vector3 scale, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
    }

    private static TextMeshPro AddWorldLabel(Transform parent, float y)
    {
        GameObject label = new GameObject("Label");
        label.transform.SetParent(parent, false);
        label.transform.localPosition = new Vector3(0f, y, 0f);
        TextMeshPro text = label.AddComponent<TextMeshPro>();
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.text = "";
        text.fontSize = 3f;
        text.alignment = TextAlignmentOptions.Bottom;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(4f, 1f);
        text.rectTransform.pivot = new Vector2(0.5f, 0f);
        text.sortingOrder = 30;
        return text;
    }

    // ---------- วางในฉาก ----------

    // วางร้านที่ฉากยังไม่มี: เรียงไปทางซ้ายของเคาน์เตอร์ช่าง ห่างกัน 2.5 หน่วย
    private static void PlaceCounters(ShopKeeperCounter prefab, ShopCatalog[] catalogs, List<string> report)
    {
        if (prefab == null)
        {
            report.Add("No ShopKeeperCounter prefab: place the shops yourself.");
            return;
        }

        HashSet<ShopCatalog> placed = new HashSet<ShopCatalog>();
        foreach (ShopKeeperCounter c in Object.FindObjectsByType<ShopKeeperCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.Catalog != null) placed.Add(c.Catalog);
        }

        Vector3 origin;
        BuilderCounter builder = Object.FindFirstObjectByType<BuilderCounter>(FindObjectsInactive.Include);
        GameObject outside = GameObject.Find("OutsideFront");
        if (builder != null) origin = builder.transform.position;
        else if (outside != null) origin = outside.transform.position + new Vector3(-3f, 0f, 0f);
        else origin = Vector3.zero;

        int slot = 1;
        int created = 0;
        foreach (ShopCatalog catalog in catalogs)
        {
            if (catalog == null) continue;
            if (placed.Contains(catalog))
            {
                report.Add($"{catalog.shopName} counter already in the scene (kept).");
                slot++;
                continue;
            }

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
            Undo.RegisterCreatedObjectUndo(go, $"Place {catalog.shopName}");
            go.name = catalog.shopName.Replace(" ", "") + "Counter";
            go.transform.position = origin + new Vector3(-2.5f * slot, 0f, 0f);

            SerializedObject so = new SerializedObject(go.GetComponent<ShopKeeperCounter>());
            so.FindProperty("catalog").objectReferenceValue = catalog;
            so.ApplyModifiedProperties();
            slot++;
            created++;
        }

        if (created > 0) report.Add($"Placed {created} shop counter(s) left of the builder counter: move them where you like.");
    }

    private static void PlaceBox(ShippingBox prefab, List<string> report)
    {
        int count = Object.FindObjectsByType<ShippingBox>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        if (count > 0)
        {
            report.Add("Shipping box already in the scene (kept).");
            return;
        }
        if (prefab == null)
        {
            report.Add("No ShippingBox prefab: place one yourself.");
            return;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
        Undo.RegisterCreatedObjectUndo(go, "Place ShippingBox");

        Mailbox mailbox = Object.FindFirstObjectByType<Mailbox>(FindObjectsInactive.Include);
        GameObject outside = GameObject.Find("OutsideFront");
        if (mailbox != null)
        {
            go.transform.position = mailbox.transform.position + new Vector3(1.5f, 0f, 0f);
            report.Add("Placed the shipping box 1.5 units right of the mailbox.");
        }
        else if (outside != null)
        {
            go.transform.position = outside.transform.position + new Vector3(3f, 0f, 0f);
            report.Add("Placed the shipping box 3 units right of OutsideFront.");
        }
        else
        {
            report.Add("Mailbox / OutsideFront not found: placed the shipping box at (0, 0).");
        }
    }

    // ---------- หน้าต่างร้าน ----------

    private static void BuildShopWindow(Canvas canvas, List<string> report)
    {
        if (Object.FindFirstObjectByType<ShopUI>(FindObjectsInactive.Include) != null)
        {
            report.Add("ShopUI already exists (kept, layout not touched).");
            return;
        }

        GameObject host = CreateUI(canvas.transform, "ShopUI");
        SetStretch(host.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        GameObject window = CreateWindow(host.transform, "ShopWindow");

        TMP_Text title = CreateText(window.transform, "TitleText", 36, TextAlignmentOptions.Left);
        SetTopStretch(title.rectTransform, 32f, 400f, 18f, 46f);
        title.fontStyle = FontStyles.Bold;

        TMP_Text money = CreateText(window.transform, "MoneyText", 26, TextAlignmentOptions.Right);
        SetTopStretch(money.rectTransform, 400f, 110f, 24f, 40f);

        Button close = BuildButton(window.transform, "CloseButton", "Close", ButtonColor, 22);
        PlaceTopRight(close.GetComponent<RectTransform>(), new Vector2(84f, 40f));

        Button buyTab = BuildButton(window.transform, "BuyTab", "Buy", ButtonColor, 22);
        PlaceTopLeft(buyTab.GetComponent<RectTransform>(), 32f, 74f, new Vector2(140f, 40f));
        Button sellTab = BuildButton(window.transform, "SellTab", "Sell", ButtonColor, 22);
        PlaceTopLeft(sellTab.GetComponent<RectTransform>(), 180f, 74f, new Vector2(140f, 40f));

        // แท็บ Buy
        GameObject buyRoot = CreateUI(window.transform, "BuyRoot");
        SetStretch(buyRoot.GetComponent<RectTransform>(), 24f, 24f, 126f, 24f);
        RectTransform buyContent = BuildScroll(buyRoot.transform, "BuyList");
        SetStretch((RectTransform)buyContent.parent.parent, 0f, 0f, 0f, 0f);
        ShopEntryRowUI rowTemplate = BuildShopRowTemplate(buyContent);

        // แท็บ Sell
        GameObject sellRoot = CreateUI(window.transform, "SellRoot");
        SetStretch(sellRoot.GetComponent<RectTransform>(), 24f, 24f, 126f, 24f);
        SellPanelUI sellPanel = BuildSellPanel(sellRoot.transform, 0f, 0.62f);
        TMP_Text sellSummary = CreateText(sellRoot.transform, "SellSummaryText", 22, TextAlignmentOptions.TopLeft);
        SetAnchors(sellSummary.rectTransform, 0.64f, 1f, 0f, 1f, 8f);
        sellRoot.SetActive(false);

        ShopUI ui = host.AddComponent<ShopUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("windowRoot").objectReferenceValue = window;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("moneyText").objectReferenceValue = money;
        so.FindProperty("closeButton").objectReferenceValue = close;
        so.FindProperty("buyTabButton").objectReferenceValue = buyTab;
        so.FindProperty("sellTabButton").objectReferenceValue = sellTab;
        so.FindProperty("buyRoot").objectReferenceValue = buyRoot;
        so.FindProperty("sellRoot").objectReferenceValue = sellRoot;
        so.FindProperty("listContent").objectReferenceValue = buyContent;
        so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
        so.FindProperty("sellPanel").objectReferenceValue = sellPanel;
        so.FindProperty("sellSummaryText").objectReferenceValue = sellSummary;
        so.ApplyModifiedPropertiesWithoutUndo();

        window.SetActive(false);
        report.Add("Built ShopUI (ShopWindow).");
    }

    private static ShopEntryRowUI BuildShopRowTemplate(RectTransform content)
    {
        GameObject row = CreateUI(content, "ShopRowTemplate");
        row.AddComponent<Image>().color = RowColor;
        row.AddComponent<LayoutElement>().preferredHeight = 60f;

        TMP_Text name = CreateText(row.transform, "NameText", 22, TextAlignmentOptions.Left);
        SetAnchors(name.rectTransform, 0f, 0.34f, 0f, 1f, 12f);
        TMP_Text owned = CreateText(row.transform, "OwnedText", 18, TextAlignmentOptions.Left);
        SetAnchors(owned.rectTransform, 0.34f, 0.56f, 0f, 1f, 6f);
        TMP_Text price = CreateText(row.transform, "PriceText", 22, TextAlignmentOptions.Right);
        SetAnchors(price.rectTransform, 0.56f, 0.68f, 0f, 1f, 6f);
        TMP_Text reason = CreateText(row.transform, "ReasonText", 18, TextAlignmentOptions.Center);
        SetAnchors(reason.rectTransform, 0.68f, 0.8f, 0f, 1f, 4f);

        Button buyOne = BuildButton(row.transform, "BuyOneButton", "Buy 1", BuyColor, 20);
        SetAnchors(buyOne.GetComponent<RectTransform>(), 0.8f, 0.895f, 0.12f, 0.88f, 4f);
        Button buyFive = BuildButton(row.transform, "BuyFiveButton", "Buy 5", BuyColor, 20);
        SetAnchors(buyFive.GetComponent<RectTransform>(), 0.9f, 0.995f, 0.12f, 0.88f, 4f);

        ShopEntryRowUI ui = row.AddComponent<ShopEntryRowUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("nameText").objectReferenceValue = name;
        so.FindProperty("ownedText").objectReferenceValue = owned;
        so.FindProperty("priceText").objectReferenceValue = price;
        so.FindProperty("reasonText").objectReferenceValue = reason;
        so.FindProperty("buyOneButton").objectReferenceValue = buyOne;
        so.FindProperty("buyOneLabel").objectReferenceValue = buyOne.GetComponentInChildren<TMP_Text>(true);
        so.FindProperty("buyFiveButton").objectReferenceValue = buyFive;
        so.FindProperty("buyFiveLabel").objectReferenceValue = buyFive.GetComponentInChildren<TMP_Text>(true);
        so.ApplyModifiedPropertiesWithoutUndo();

        row.SetActive(false);
        return ui;
    }

    // ---------- หน้าต่างกล่องขาย ----------

    private static void BuildBoxWindow(Canvas canvas, List<string> report)
    {
        if (Object.FindFirstObjectByType<ShippingBoxUI>(FindObjectsInactive.Include) != null)
        {
            report.Add("ShippingBoxUI already exists (kept, layout not touched).");
            return;
        }

        GameObject host = CreateUI(canvas.transform, "ShippingBoxUI");
        SetStretch(host.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        GameObject window = CreateWindow(host.transform, "ShippingBoxWindow");

        TMP_Text title = CreateText(window.transform, "TitleText", 36, TextAlignmentOptions.Left);
        SetTopStretch(title.rectTransform, 32f, 140f, 18f, 46f);
        title.fontStyle = FontStyles.Bold;

        Button close = BuildButton(window.transform, "CloseButton", "Close", ButtonColor, 22);
        PlaceTopRight(close.GetComponent<RectTransform>(), new Vector2(84f, 40f));

        GameObject body = CreateUI(window.transform, "Body");
        SetStretch(body.GetComponent<RectTransform>(), 24f, 24f, 80f, 24f);

        SellPanelUI sellPanel = BuildSellPanel(body.transform, 0f, 0.6f);

        // ฝั่งขวา: ของในกล่อง
        GameObject right = CreateUI(body.transform, "BoxSide");
        SetAnchors(right.GetComponent<RectTransform>(), 0.62f, 1f, 0f, 1f, 0f);

        TMP_Text header = CreateText(right.transform, "BoxHeader", 24, TextAlignmentOptions.Left);
        SetTopStretch(header.rectTransform, 4f, 4f, 0f, 34f);
        header.text = "In the box today";
        header.fontStyle = FontStyles.Bold;

        RectTransform boxContent = BuildScroll(right.transform, "BoxList");
        SetStretch((RectTransform)boxContent.parent.parent, 0f, 0f, 40f, 130f);

        TMP_Text empty = CreateText(right.transform, "BoxEmptyText", 20, TextAlignmentOptions.Center);
        SetStretch(empty.rectTransform, 10f, 10f, 60f, 150f);
        empty.raycastTarget = false;

        TMP_Text summary = CreateText(right.transform, "SummaryText", 20, TextAlignmentOptions.TopLeft);
        SetBottomStretch(summary.rectTransform, 4f, 4f, 0f, 120f);

        BoxEntryRowUI boxRow = BuildBoxRowTemplate(boxContent);

        ShippingBoxUI ui = host.AddComponent<ShippingBoxUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("windowRoot").objectReferenceValue = window;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("closeButton").objectReferenceValue = close;
        so.FindProperty("sellPanel").objectReferenceValue = sellPanel;
        so.FindProperty("boxContent").objectReferenceValue = boxContent;
        so.FindProperty("boxRowTemplate").objectReferenceValue = boxRow;
        so.FindProperty("boxEmptyText").objectReferenceValue = empty;
        so.FindProperty("summaryText").objectReferenceValue = summary;
        so.ApplyModifiedPropertiesWithoutUndo();

        window.SetActive(false);
        report.Add("Built ShippingBoxUI (ShippingBoxWindow).");
    }

    private static BoxEntryRowUI BuildBoxRowTemplate(RectTransform content)
    {
        GameObject row = CreateUI(content, "BoxRowTemplate");
        row.AddComponent<Image>().color = RowColor;
        row.AddComponent<LayoutElement>().preferredHeight = 48f;

        TMP_Text name = CreateText(row.transform, "NameText", 18, TextAlignmentOptions.Left);
        SetAnchors(name.rectTransform, 0f, 0.5f, 0f, 1f, 8f);
        TMP_Text price = CreateText(row.transform, "PriceText", 18, TextAlignmentOptions.Right);
        SetAnchors(price.rectTransform, 0.5f, 0.68f, 0f, 1f, 4f);
        Button takeOne = BuildButton(row.transform, "TakeOneButton", "Take 1", ButtonColor, 16);
        SetAnchors(takeOne.GetComponent<RectTransform>(), 0.69f, 0.84f, 0.12f, 0.88f, 3f);
        Button takeAll = BuildButton(row.transform, "TakeAllButton", "Take all", ButtonColor, 16);
        SetAnchors(takeAll.GetComponent<RectTransform>(), 0.845f, 0.995f, 0.12f, 0.88f, 3f);

        BoxEntryRowUI ui = row.AddComponent<BoxEntryRowUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("nameText").objectReferenceValue = name;
        so.FindProperty("priceText").objectReferenceValue = price;
        so.FindProperty("takeOneButton").objectReferenceValue = takeOne;
        so.FindProperty("takeAllButton").objectReferenceValue = takeAll;
        so.ApplyModifiedPropertiesWithoutUndo();

        row.SetActive(false);
        return ui;
    }

    // ---------- แผงขาย (ใช้ร่วม) ----------

    // สร้างแผงขายในช่วงแนวนอน xMin–xMax ของ parent
    private static SellPanelUI BuildSellPanel(Transform parent, float xMin, float xMax)
    {
        GameObject panel = CreateUI(parent, "SellPanel");
        SetAnchors(panel.GetComponent<RectTransform>(), xMin, xMax, 0f, 1f, 0f);

        Button ingredientsTab = BuildButton(panel.transform, "IngredientsTab", "Ingredients", ButtonColor, 20);
        PlaceTopLeft(ingredientsTab.GetComponent<RectTransform>(), 0f, 0f, new Vector2(170f, 36f));
        Button dishesTab = BuildButton(panel.transform, "DishesTab", "Dishes", ButtonColor, 20);
        PlaceTopLeft(dishesTab.GetComponent<RectTransform>(), 176f, 0f, new Vector2(170f, 36f));

        RectTransform content = BuildScroll(panel.transform, "SellList");
        SetStretch((RectTransform)content.parent.parent, 0f, 0f, 44f, 40f);

        TMP_Text empty = CreateText(panel.transform, "EmptyText", 20, TextAlignmentOptions.Center);
        SetStretch(empty.rectTransform, 10f, 10f, 80f, 80f);
        empty.raycastTarget = false;

        TMP_Text hint = CreateText(panel.transform, "HintText", 18, TextAlignmentOptions.Center);
        SetBottomStretch(hint.rectTransform, 0f, 0f, 0f, 34f);
        hint.color = new Color(1f, 1f, 1f, 0.7f);
        hint.raycastTarget = false;

        SellItemSlotUI rowTemplate = BuildSellRowTemplate(content);

        SellPanelUI ui = panel.AddComponent<SellPanelUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("ingredientsTabButton").objectReferenceValue = ingredientsTab;
        so.FindProperty("dishesTabButton").objectReferenceValue = dishesTab;
        so.FindProperty("listContent").objectReferenceValue = content;
        so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
        so.FindProperty("emptyText").objectReferenceValue = empty;
        so.FindProperty("hintText").objectReferenceValue = hint;
        so.ApplyModifiedPropertiesWithoutUndo();
        return ui;
    }

    private static SellItemSlotUI BuildSellRowTemplate(RectTransform content)
    {
        GameObject row = CreateUI(content, "SellRowTemplate");
        Image bg = row.AddComponent<Image>();
        bg.color = RowColor;
        Button button = row.AddComponent<Button>();
        button.targetGraphic = bg;
        row.AddComponent<LayoutElement>().preferredHeight = 44f;

        GameObject frame = CreateUI(row.transform, "SelectionFrame");
        SetStretch(frame.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Image frameImage = frame.AddComponent<Image>();
        frameImage.color = SelectColor;
        frameImage.raycastTarget = false;
        frame.SetActive(false);

        GameObject icon = CreateUI(row.transform, "Icon");
        RectTransform iconRt = icon.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(6f, 0f);
        iconRt.sizeDelta = new Vector2(34f, 34f);
        Image iconImage = icon.AddComponent<Image>();
        iconImage.raycastTarget = false;
        iconImage.preserveAspect = true;

        TMP_Text name = CreateText(row.transform, "NameText", 20, TextAlignmentOptions.Left);
        SetAnchors(name.rectTransform, 0f, 0.55f, 0f, 1f, 0f);
        name.rectTransform.offsetMin = new Vector2(48f, 0f);
        name.raycastTarget = false;
        TMP_Text count = CreateText(row.transform, "CountText", 18, TextAlignmentOptions.Right);
        SetAnchors(count.rectTransform, 0.55f, 1f, 0f, 1f, 10f);
        count.raycastTarget = false;

        SellItemSlotUI ui = row.AddComponent<SellItemSlotUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("iconImage").objectReferenceValue = iconImage;
        so.FindProperty("nameText").objectReferenceValue = name;
        so.FindProperty("countText").objectReferenceValue = count;
        so.FindProperty("selectionFrame").objectReferenceValue = frame;
        so.ApplyModifiedPropertiesWithoutUndo();

        row.SetActive(false);
        return ui;
    }

    // ---------- ตัวช่วย UI ----------

    private static GameObject CreateWindow(Transform parent, string name)
    {
        GameObject window = CreateUI(parent, name);
        RectTransform rt = window.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 20f);
        rt.sizeDelta = new Vector2(1180f, 760f);
        window.AddComponent<Image>().color = WindowColor;
        return window;
    }

    // ScrollRect แนวตั้ง คืน Content (แถวเรียงด้วย VerticalLayoutGroup) / ตัว ScrollRect = content.parent.parent
    private static RectTransform BuildScroll(Transform parent, string name)
    {
        GameObject root = CreateUI(parent, name);
        root.AddComponent<Image>().color = ListBackColor;
        ScrollRect scroll = root.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        GameObject viewport = CreateUI(root.transform, "Viewport");
        RectTransform viewportRt = viewport.GetComponent<RectTransform>();
        SetStretch(viewportRt, 4f, 4f, 4f, 4f);
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateUI(viewport.transform, "Content");
        RectTransform contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(2, 2, 2, 2);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRt;
        scroll.content = contentRt;
        return contentRt;
    }

    private static void PlaceTopRight(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-16f, -16f);
        rt.sizeDelta = size;
    }

    private static void PlaceTopLeft(RectTransform rt, float x, float y, Vector2 size)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = size;
    }

    // anchor ตามสัดส่วน + ขอบใน pad (ทุกด้าน)
    private static void SetAnchors(RectTransform rt, float xMin, float xMax, float yMin, float yMax, float pad)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    private static void SetBottomStretch(RectTransform rt, float left, float right, float bottom, float height)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, bottom + height);
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
