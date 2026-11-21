using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// เครื่องมือสร้างแผงช่องสวมใส่อัตโนมัติ (ใช้ใน Editor เท่านั้น)
// เมนู: Tools > Second Serving > Build Equipment Panel
// - ใส่ PlayerEquipment ให้ Player ถ้ายังไม่มี
// - สร้าง EquipmentPanel (8 ช่อง) ในหน้าต่างกระเป๋า มุมขวาบน เหนือแผงรายละเอียด
// - ผูกช่องใน EquipmentPanelUI / EquipmentSlotUI / InventoryUI / SaveManager ให้
// - กดซ้ำได้ จะจัดตำแหน่งใหม่โดยไม่สร้างซ้ำ (หาจากชื่อ) กด Ctrl+Z ย้อนได้
// เมนู: Tools > Second Serving > Create Test Equipment Items
// - สร้าง ItemData ของสวมใส่ทดสอบ 8 ชิ้นใน Assets/Items/TestEquipment แล้วอัปเดต ItemDatabase
public static class EquipmentLayoutBuilder
{
    // ---------- ขนาด (ต้องตรงกับ InventoryLayoutBuilder: หน้าต่าง 1500x860, ขอบ 40, แผงรายละเอียดกว้าง 420, แท็บเริ่มที่ 140) ----------

    private const float Margin = 40f;
    private const float ColumnWidth = 420f;
    private const float PanelTop = 140f;
    private const float Gap = 20f;

    private const float Padding = 20f;
    private const float RowLabelHeight = 28f;
    private const float SlotWidth = 86f;
    private const float SlotHeight = 100f;
    private const float SlotSpacing = 12f;
    private const float RowGap = 14f;

    // ความสูงแผง = (ชื่อแถว + ช่อง) x 2 แถว + ช่องว่างระหว่างแถว + ขอบบนล่าง
    private const float PanelHeight = Padding * 2f + (RowLabelHeight + SlotHeight) * 2f + RowGap;

    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.25f);
    private static readonly Color SlotBackgroundColor = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color SelectionColor = new Color(1f, 0.85f, 0.3f, 0.45f);

    private static readonly string[] RowLabels = { "Stats", "Fashion" };

    [MenuItem("Tools/Second Serving/Build Equipment Panel")]
    public static void BuildPanel()
    {
        InventoryUI ui = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            EditorUtility.DisplayDialog("Equipment Panel", "No InventoryUI found in the open scene.", "OK");
            return;
        }

        SerializedObject uiSo = new SerializedObject(ui);
        GameObject window = uiSo.FindProperty("windowRoot").objectReferenceValue as GameObject;
        if (window == null)
        {
            EditorUtility.DisplayDialog("Equipment Panel", "Window Root is not assigned in InventoryUI. Run Build Inventory Window Layout first.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Build Equipment Panel");
        int undoGroup = Undo.GetCurrentGroup();

        // 1) ใส่ PlayerEquipment ให้ผู้เล่น
        string playerResult;
        PlayerEquipment equipment = Object.FindFirstObjectByType<PlayerEquipment>(FindObjectsInactive.Include);
        if (equipment == null)
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player != null)
            {
                equipment = Undo.AddComponent<PlayerEquipment>(player.gameObject);
                playerResult = $"Added PlayerEquipment to '{player.name}'.";
            }
            else
            {
                playerResult = "No PlayerController found. Add PlayerEquipment to the Player yourself.";
            }
        }
        else
        {
            playerResult = $"PlayerEquipment found on '{equipment.name}'.";
        }

        // 2) ตัวแผง: ยึดมุมขวาบนของหน้าต่าง คอลัมน์เดียวกับแผงรายละเอียด
        GameObject panel = FindOrCreate(window.transform, "EquipmentPanel");
        Undo.RegisterFullObjectHierarchyUndo(panel, "Build Equipment Panel");

        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 1f);
        panelRt.anchorMax = new Vector2(1f, 1f);
        panelRt.pivot = new Vector2(1f, 1f);
        panelRt.anchoredPosition = new Vector2(-Margin, -PanelTop);
        panelRt.sizeDelta = new Vector2(ColumnWidth, PanelHeight);

        Image panelBg = GetOrAdd<Image>(panel);
        panelBg.color = PanelColor;
        panelBg.raycastTarget = true;

        EquipmentPanelUI panelUI = GetOrAdd<EquipmentPanelUI>(panel);

        // 3) ชื่อแถว 2 แถว และช่องแถวละ 4 ช่อง (ลำดับช่องตรงกับ EquipSlot)
        EquipmentSlotUI[] slotUIs = new EquipmentSlotUI[PlayerEquipment.SlotCount];

        for (int row = 0; row < 2; row++)
        {
            float rowTop = Padding + row * (RowLabelHeight + SlotHeight + RowGap);

            TMP_Text label = FindOrCreateText(panel.transform, $"{RowLabels[row]}Label", 22, TextAlignmentOptions.Left);
            SetTopLeft(label.rectTransform, Padding, rowTop, new Vector2(ColumnWidth - Padding * 2f, RowLabelHeight));
            label.text = row == 0 ? "Stats" : "Fashion (looks only)";
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;

            for (int col = 0; col < 4; col++)
            {
                int index = row * 4 + col;
                EquipSlot slot = (EquipSlot)index;
                float left = Padding + col * (SlotWidth + SlotSpacing);
                float top = rowTop + RowLabelHeight;

                slotUIs[index] = BuildSlot(panel.transform, slot, left, top);
            }
        }

        // 4) ผูกช่องใน EquipmentPanelUI
        SerializedObject panelSo = new SerializedObject(panelUI);
        panelSo.FindProperty("equipment").objectReferenceValue = equipment;
        SerializedProperty slotsProp = panelSo.FindProperty("slotUIs");
        slotsProp.arraySize = slotUIs.Length;
        for (int i = 0; i < slotUIs.Length; i++)
        {
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotUIs[i];
        }
        panelSo.ApplyModifiedProperties();

        // 5) ผูกช่องใน InventoryUI (ระยะเลื่อนแผงรายละเอียด = ความสูงแผงนี้ + ช่องว่าง)
        uiSo.FindProperty("equipmentPanel").objectReferenceValue = panelUI;
        uiSo.FindProperty("detailShiftWithEquipment").floatValue = PanelHeight + Gap;
        uiSo.ApplyModifiedProperties();

        // 6) ผูก SaveManager
        string saveResult;
        SaveManager saveManager = Object.FindFirstObjectByType<SaveManager>(FindObjectsInactive.Include);
        if (saveManager != null)
        {
            SerializedObject saveSo = new SerializedObject(saveManager);
            saveSo.FindProperty("playerEquipment").objectReferenceValue = equipment;
            saveSo.ApplyModifiedProperties();
            saveResult = "SaveManager linked.";
        }
        else
        {
            saveResult = "No SaveManager found. Equipped items will not be saved.";
        }

        // แผงอยู่หน้าสุดในหน้าต่าง (วาดทับแผงรายละเอียดถ้าซ้อนกัน)
        panel.transform.SetAsLastSibling();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        Selection.activeGameObject = panel;

        Debug.Log($"EquipmentLayoutBuilder: Equipment panel built. {playerResult} {saveResult}");
        EditorUtility.DisplayDialog("Equipment Panel",
            "Equipment panel is ready (top-right of the bag window, shown on the Equipment / Accessory tabs).\n\n" +
            playerResult + "\n" + saveResult + "\n\nSave the scene with Ctrl+S.", "OK");
    }

    // สร้างช่อง 1 ช่อง: พื้นหลัง (รับการวาง) + กรอบเลือก + ไอคอน + ชื่อ
    private static EquipmentSlotUI BuildSlot(Transform parent, EquipSlot slot, float left, float top)
    {
        GameObject go = FindOrCreate(parent, $"EquipSlot_{slot}");
        SetTopLeft(go.GetComponent<RectTransform>(), left, top, new Vector2(SlotWidth, SlotHeight));

        // พื้นหลังต้องรับ raycast ไม่งั้นวางของ (OnDrop) ไม่ได้
        Image bg = GetOrAdd<Image>(go);
        bg.color = SlotBackgroundColor;
        bg.raycastTarget = true;

        // กรอบเลือก: อยู่หลังไอคอน ปิดไว้ก่อน
        GameObject frame = FindOrCreate(go.transform, "SelectionFrame");
        SetStretch(frame.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Image frameImage = GetOrAdd<Image>(frame);
        frameImage.color = SelectionColor;
        frameImage.raycastTarget = false;
        frame.transform.SetSiblingIndex(0);
        frame.SetActive(false);

        // ไอคอน: ส่วนบนของช่อง
        GameObject iconGo = FindOrCreate(go.transform, "Icon");
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 1f);
        iconRt.anchorMax = new Vector2(0.5f, 1f);
        iconRt.pivot = new Vector2(0.5f, 1f);
        iconRt.anchoredPosition = new Vector2(0f, -8f);
        iconRt.sizeDelta = new Vector2(60f, 60f);
        Image icon = GetOrAdd<Image>(iconGo);
        icon.raycastTarget = false;
        icon.enabled = false;
        iconGo.transform.SetSiblingIndex(1);

        // ชื่อ: ด้านล่างของช่อง
        TMP_Text label = FindOrCreateText(go.transform, "Label", 14, TextAlignmentOptions.Bottom);
        SetStretch(label.rectTransform, 4f, 4f, 70f, 4f);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        label.text = PlayerEquipment.GetSlotLabel(slot);

        EquipmentSlotUI slotUI = GetOrAdd<EquipmentSlotUI>(go);
        SerializedObject so = new SerializedObject(slotUI);
        so.FindProperty("iconImage").objectReferenceValue = icon;
        so.FindProperty("labelText").objectReferenceValue = label;
        so.FindProperty("selectionFrame").objectReferenceValue = frame;
        so.ApplyModifiedProperties();

        return slotUI;
    }

    // ---------- ของทดสอบ ----------

    private const string TestFolder = "Assets/Items/TestEquipment";

    // รหัส, ชื่อ, ประเภทไอเทม, ช่องที่ใส่ได้
    private static readonly (string id, string name, ItemType type, EquipType equip)[] TestItems =
    {
        ("test_boots", "Test Boots", ItemType.Equipment, EquipType.Shoes),
        ("test_ring_ruby", "Test Ruby Ring", ItemType.Accessory, EquipType.Ring),
        ("test_ring_jade", "Test Jade Ring", ItemType.Accessory, EquipType.Ring),
        ("test_necklace", "Test Necklace", ItemType.Accessory, EquipType.Necklace),
        ("test_hat", "Test Hat", ItemType.Equipment, EquipType.Head),
        ("test_top", "Test Shirt", ItemType.Equipment, EquipType.Top),
        ("test_bottom", "Test Pants", ItemType.Equipment, EquipType.Bottom),
        ("test_wings", "Test Wings", ItemType.Equipment, EquipType.Back),
    };

    [MenuItem("Tools/Second Serving/Create Test Equipment Items")]
    public static void CreateTestItems()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Items"))
        {
            AssetDatabase.CreateFolder("Assets", "Items");
        }
        if (!AssetDatabase.IsValidFolder(TestFolder))
        {
            AssetDatabase.CreateFolder("Assets/Items", "TestEquipment");
        }

        int created = 0;
        int skipped = 0;

        foreach (var data in TestItems)
        {
            string path = $"{TestFolder}/{data.id}.asset";
            if (AssetDatabase.LoadAssetAtPath<ItemData>(path) != null)
            {
                skipped++;
                continue;
            }

            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = data.id;
            item.itemName = data.name;
            item.itemType = data.type;
            item.equipType = data.equip;
            item.description = "Test item for equipment slots.";
            AssetDatabase.CreateAsset(item, path);
            created++;
        }

        AssetDatabase.SaveAssets();

        // อัปเดต ItemDatabase ให้รู้จักของใหม่ (ไม่งั้นโหลดเซฟกลับมาไม่ได้)
        string dbResult = "No ItemDatabase found. Run Find All Items yourself.";
        string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
        if (guids.Length > 0)
        {
            ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (db != null)
            {
                int count = db.RefreshFromProject();
                AssetDatabase.SaveAssets();
                dbResult = $"ItemDatabase now has {count} items.";
            }
        }

        Debug.Log($"EquipmentLayoutBuilder: Test equipment created {created}, skipped {skipped}. {dbResult}");
        EditorUtility.DisplayDialog("Test Equipment",
            $"Created {created} test items (skipped {skipped} that already exist) in {TestFolder}.\n\n{dbResult}", "OK");
    }

    // ---------- ตัวช่วย ----------

    private static void SetTopLeft(RectTransform rt, float left, float top, Vector2 size)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(left, -top);
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
