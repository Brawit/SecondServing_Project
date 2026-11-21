using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// เครื่องมือสร้าง HUD อัตโนมัติ (ใช้ใน Editor เท่านั้น)
// เมนู: Tools > Second Serving > Build Stats HUD
// - ใส่ PlayerStats ให้ Player ถ้ายังไม่มี
// - สร้าง StatsHUD (แถบ HP / ST / MP / FEVER) มุมซ้ายบนของ Canvas แล้วผูกช่องใน StatsUI ให้
// เมนู: Tools > Second Serving > Build Hotbar HUD
// - ใส่ Hotbar / PlayerStats / PlayerActionController ให้ Player ถ้ายังไม่มี
// - สร้าง HotbarHUD (10 ช่อง) กลางล่างของ Canvas แล้วผูกช่องใน HotbarUI / HotbarSlotUI ให้
// - กดซ้ำได้ จะจัดตำแหน่งใหม่โดยไม่สร้างซ้ำ (หาจากชื่อ)
// กด Ctrl+Z ย้อนได้
public static class HudLayoutBuilder
{
    // ---------- ขนาด (หน่วยตาม Canvas Scaler 1920x1080) ----------

    private const float HudLeft = 24f;
    private const float HudTop = 24f;
    private const float RowHeight = 26f;
    private const float RowSpacing = 8f;
    private const float LabelWidth = 80f;
    private const float BarWidth = 260f;
    private const float FeverBarWidth = 300f;

    private static readonly Color BarBackgroundColor = new Color(0f, 0f, 0f, 0.55f);

    // ชื่อแถว, ข้อความหน้าแถบ, ชื่อ property ใน StatsUI
    private static readonly string[] RowNames = { "HealthRow", "StaminaRow", "ManaRow", "FeverRow" };
    private static readonly string[] RowLabels = { "HP", "ST", "MP", "FEVER" };
    private static readonly string[] BarProperties = { "healthBar", "staminaBar", "manaBar", "feverBar" };

    [MenuItem("Tools/Second Serving/Build Stats HUD")]
    public static void BuildStatsHud()
    {
        Canvas canvas = FindMainCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Stats HUD", "No Canvas found in the open scene.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Build Stats HUD");
        int undoGroup = Undo.GetCurrentGroup();

        // 1) ใส่ PlayerStats ให้ผู้เล่น
        PlayerStats stats = Object.FindFirstObjectByType<PlayerStats>(FindObjectsInactive.Include);
        string statsResult;
        if (stats == null)
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player != null)
            {
                stats = Undo.AddComponent<PlayerStats>(player.gameObject);
                statsResult = $"Added PlayerStats to '{player.name}'.";
            }
            else
            {
                statsResult = "No PlayerController found. Add PlayerStats to the Player yourself.";
            }
        }
        else
        {
            statsResult = $"PlayerStats found on '{stats.name}'.";
        }

        // 2) ตัว HUD: ยึดมุมซ้ายบน
        GameObject hud = FindOrCreate(canvas.transform, "StatsHUD");
        RectTransform hudRt = hud.GetComponent<RectTransform>();
        float hudHeight = RowNames.Length * RowHeight + (RowNames.Length - 1) * RowSpacing;
        SetTopLeft(hudRt, HudLeft, HudTop, new Vector2(LabelWidth + FeverBarWidth + 10f, hudHeight));

        StatsUI statsUI = hud.GetComponent<StatsUI>();
        if (statsUI == null)
        {
            statsUI = Undo.AddComponent<StatsUI>(hud);
        }

        SerializedObject so = new SerializedObject(statsUI);
        so.FindProperty("playerStats").objectReferenceValue = stats;

        // 3) แถวละ 1 แถบ
        for (int i = 0; i < RowNames.Length; i++)
        {
            float top = i * (RowHeight + RowSpacing);
            float barWidth = i == RowNames.Length - 1 ? FeverBarWidth : BarWidth;

            BuildRow(hud.transform, RowNames[i], RowLabels[i], top, barWidth, out Image fill, out TMP_Text valueText);

            SerializedProperty barProp = so.FindProperty(BarProperties[i]);
            barProp.FindPropertyRelative("fillImage").objectReferenceValue = fill;
            barProp.FindPropertyRelative("valueText").objectReferenceValue = valueText;
        }

        so.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = hud;

        Debug.Log($"HudLayoutBuilder: Stats HUD built. {statsResult}");
        EditorUtility.DisplayDialog("Stats HUD",
            "Stats HUD is ready (top-left of the Canvas).\n\n" + statsResult + "\n\nSave the scene with Ctrl+S.", "OK");
    }

    // ---------- Hotbar ----------

    private const float SlotSize = 72f;
    private const float SlotSpacing = 6f;
    private const float HotbarBottom = 16f;
    private const float SelectedNameHeight = 30f;

    private static readonly Color SlotBackgroundColor = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color SlotSelectionColor = new Color(1f, 0.85f, 0.3f, 0.5f);

    [MenuItem("Tools/Second Serving/Build Hotbar HUD")]
    public static void BuildHotbarHud()
    {
        Canvas canvas = FindMainCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Hotbar HUD", "No Canvas found in the open scene.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Build Hotbar HUD");
        int undoGroup = Undo.GetCurrentGroup();

        // 1) ใส่ Hotbar / PlayerStats / PlayerActionController ให้ผู้เล่น
        string playerResult;
        Hotbar hotbar = null;
        PlayerController player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            GameObject p = player.gameObject;
            hotbar = p.GetComponent<Hotbar>();
            if (hotbar == null) hotbar = Undo.AddComponent<Hotbar>(p);
            if (p.GetComponent<PlayerStats>() == null) Undo.AddComponent<PlayerStats>(p);
            if (p.GetComponent<PlayerActionController>() == null) Undo.AddComponent<PlayerActionController>(p);
            playerResult = $"Hotbar, PlayerStats and PlayerActionController are on '{p.name}'.";
        }
        else
        {
            playerResult = "No PlayerController found. Add Hotbar and PlayerActionController to the Player yourself.";
        }

        // 2) ตัวแถบ: ยึดกลางล่าง
        GameObject bar = FindOrCreate(canvas.transform, "HotbarHUD");
        RectTransform barRt = bar.GetComponent<RectTransform>();
        float barWidth = Hotbar.SlotCount * SlotSize + (Hotbar.SlotCount - 1) * SlotSpacing;
        barRt.anchorMin = new Vector2(0.5f, 0f);
        barRt.anchorMax = new Vector2(0.5f, 0f);
        barRt.pivot = new Vector2(0.5f, 0f);
        barRt.sizeDelta = new Vector2(barWidth, SlotSize);
        barRt.anchoredPosition = new Vector2(0f, HotbarBottom);

        HotbarUI hotbarUI = bar.GetComponent<HotbarUI>();
        if (hotbarUI == null)
        {
            hotbarUI = Undo.AddComponent<HotbarUI>(bar);
        }

        // 3) ช่อง 10 ช่อง
        HotbarSlotUI[] slotUIs = new HotbarSlotUI[Hotbar.SlotCount];
        for (int i = 0; i < Hotbar.SlotCount; i++)
        {
            slotUIs[i] = BuildHotbarSlot(bar.transform, i);
        }

        // 4) ชื่อไอเทมที่เลือก (เหนือแถบ)
        TMP_Text selectedName = FindOrCreateText(bar.transform, "SelectedNameText", 22, TextAlignmentOptions.Center);
        selectedName.text = "";
        RectTransform nameRt = selectedName.rectTransform;
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot = new Vector2(0.5f, 0f);
        nameRt.offsetMin = new Vector2(0f, 4f);
        nameRt.offsetMax = new Vector2(0f, 4f + SelectedNameHeight);

        // 5) ผูกช่องใน HotbarUI
        SerializedObject so = new SerializedObject(hotbarUI);
        so.FindProperty("hotbar").objectReferenceValue = hotbar;
        so.FindProperty("selectedNameText").objectReferenceValue = selectedName;
        SerializedProperty slotsProp = so.FindProperty("slots");
        slotsProp.arraySize = slotUIs.Length;
        for (int i = 0; i < slotUIs.Length; i++)
        {
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotUIs[i];
        }
        so.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = bar;

        Debug.Log($"HudLayoutBuilder: Hotbar HUD built. {playerResult}");
        EditorUtility.DisplayDialog("Hotbar HUD",
            "Hotbar HUD is ready (bottom-center of the Canvas).\n\n" + playerResult + "\n\nSave the scene with Ctrl+S.", "OK");
    }

    // ช่อง 1 ช่อง: พื้นหลัง (รับคลิก) > กรอบเลือก > ไอคอน > เลขปุ่ม > จำนวน
    private static HotbarSlotUI BuildHotbarSlot(Transform parent, int index)
    {
        GameObject slot = FindOrCreate(parent, $"Slot_{index + 1}");
        RectTransform rt = slot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(SlotSize, SlotSize);
        rt.anchoredPosition = new Vector2(index * (SlotSize + SlotSpacing), 0f);

        Image bg = GetOrAdd<Image>(slot);
        bg.color = SlotBackgroundColor;
        bg.raycastTarget = true; // ให้คลิก/ลากช่องได้

        // กรอบเลือก
        GameObject frame = FindOrCreate(slot.transform, "SelectionFrame");
        Image frameImage = GetOrAdd<Image>(frame);
        frameImage.color = SlotSelectionColor;
        frameImage.raycastTarget = false;
        Stretch(frame.GetComponent<RectTransform>(), 0f);
        frame.SetActive(false);

        // ไอคอน
        GameObject icon = FindOrCreate(slot.transform, "Icon");
        Image iconImage = GetOrAdd<Image>(icon);
        iconImage.raycastTarget = false;
        iconImage.enabled = false;
        Stretch(icon.GetComponent<RectTransform>(), 10f);

        // เลขปุ่ม มุมซ้ายบน
        TMP_Text keyText = FindOrCreateText(slot.transform, "KeyText", 14, TextAlignmentOptions.TopLeft);
        keyText.text = index == 9 ? "0" : (index + 1).ToString();
        Stretch(keyText.rectTransform, 4f);

        // จำนวน มุมขวาล่าง
        TMP_Text countText = FindOrCreateText(slot.transform, "CountText", 18, TextAlignmentOptions.BottomRight);
        countText.text = "";
        countText.fontStyle = FontStyles.Bold;
        Stretch(countText.rectTransform, 4f);

        // เรียงลำดับการวาด
        frame.transform.SetSiblingIndex(0);
        icon.transform.SetSiblingIndex(1);
        keyText.transform.SetSiblingIndex(2);
        countText.transform.SetSiblingIndex(3);

        HotbarSlotUI slotUI = slot.GetComponent<HotbarSlotUI>();
        if (slotUI == null)
        {
            slotUI = Undo.AddComponent<HotbarSlotUI>(slot);
        }

        SerializedObject so = new SerializedObject(slotUI);
        so.FindProperty("iconImage").objectReferenceValue = iconImage;
        so.FindProperty("countText").objectReferenceValue = countText;
        so.FindProperty("keyText").objectReferenceValue = keyText;
        so.FindProperty("selectionFrame").objectReferenceValue = frame;
        so.ApplyModifiedProperties();

        return slotUI;
    }

    // ยืดเต็มพ่อ เว้นขอบเท่ากันทุกด้าน
    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    // ---------- ส่วนประกอบ ----------

    // แถว 1 แถว: ข้อความหน้าแถบ + พื้นหลังแถบ (มีแถบสีกับตัวเลขอยู่ข้างใน)
    private static void BuildRow(Transform parent, string rowName, string label, float top, float barWidth,
        out Image fill, out TMP_Text valueText)
    {
        GameObject row = FindOrCreate(parent, rowName);
        SetTopLeft(row.GetComponent<RectTransform>(), 0f, top, new Vector2(LabelWidth + barWidth, RowHeight));

        // ข้อความหน้าแถบ
        TMP_Text labelText = FindOrCreateText(row.transform, "Label", 20, TextAlignmentOptions.Left);
        labelText.text = label;
        labelText.fontStyle = FontStyles.Bold;
        SetLeftFixed(labelText.rectTransform, 0f, LabelWidth);

        // พื้นหลังแถบ
        GameObject bg = FindOrCreate(row.transform, "BarBackground");
        Image bgImage = GetOrAdd<Image>(bg);
        bgImage.color = BarBackgroundColor;
        bgImage.raycastTarget = false;
        SetLeftFixed(bg.GetComponent<RectTransform>(), LabelWidth, barWidth);

        // แถบสี (StatsUI จะยืด/หดด้วย Anchor ตอนเล่น) เริ่มต้นให้เต็มไว้ก่อน
        GameObject fillGo = FindOrCreate(bg.transform, "Fill");
        fill = GetOrAdd<Image>(fillGo);
        fill.raycastTarget = false;
        RectTransform fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        // ตัวเลขทับบนแถบ (ต้องอยู่หลัง Fill จึงจะแสดงอยู่ด้านบน)
        valueText = FindOrCreateText(bg.transform, "ValueText", 16, TextAlignmentOptions.Center);
        valueText.text = "0/0";
        RectTransform valueRt = valueText.rectTransform;
        valueRt.anchorMin = Vector2.zero;
        valueRt.anchorMax = Vector2.one;
        valueRt.offsetMin = Vector2.zero;
        valueRt.offsetMax = Vector2.zero;
        valueRt.SetAsLastSibling();
    }

    // ---------- ตัวช่วย ----------

    // หา Canvas หลัก: ใช้ Canvas เดียวกับ InventoryUI ถ้ามี ไม่งั้นใช้ Canvas แรกที่เจอ
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

        Canvas any = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        return any != null ? any.rootCanvas : null;
    }

    private static GameObject FindOrCreate(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null)
        {
            Undo.RecordObject(found, $"Move {name}");
            return found.gameObject;
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
        }
        else
        {
            Undo.RecordObject(text, $"Edit {name}");
        }

        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
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

    // ยึดมุมซ้ายบนของพ่อ ห่างจากซ้าย left และจากบน top
    private static void SetTopLeft(RectTransform rt, float left, float top, Vector2 size)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(left, -top);
    }

    // ยึดซ้าย ยืดเต็มความสูงของพ่อ กว้างคงที่
    private static void SetLeftFixed(RectTransform rt, float left, float width)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = new Vector2(left, 0f);
        rt.offsetMax = new Vector2(left + width, 0f);
    }
}
