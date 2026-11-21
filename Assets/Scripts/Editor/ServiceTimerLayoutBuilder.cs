using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// เครื่องมือสร้างหน้าจอจับเวลาร้านอัตโนมัติ (ใช้ใน Editor เท่านั้น)
// เมนู: Tools > Second Serving > Build Service Timer
// - สร้าง ServiceTimerUI → ServiceTimerPanel (TimerText, StatusText) กลางบนของจอ บน Canvas เดียวกับนาฬิกา
// - ผูก Clock Panel = แผงที่มี ClockUI (จะถูกซ่อนระหว่างเปิดร้าน)
// - กดซ้ำได้ จะจัดตำแหน่งใหม่โดยไม่สร้างซ้ำ (หาจากชื่อ) กด Ctrl+Z ย้อนได้
public static class ServiceTimerLayoutBuilder
{
    private const float PanelWidth = 560f;
    private const float PanelHeight = 150f;
    private const float TopMargin = 20f;

    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.6f);

    [MenuItem("Tools/Second Serving/Build Service Timer")]
    public static void Build()
    {
        ClockUI clock = Object.FindFirstObjectByType<ClockUI>(FindObjectsInactive.Include);
        Canvas canvas = clock != null ? clock.GetComponentInParent<Canvas>(true) : Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Service Timer", "No Canvas found in the open scene.", "OK");
            return;
        }
        canvas = canvas.rootCanvas;

        Undo.SetCurrentGroupName("Build Service Timer");
        int undoGroup = Undo.GetCurrentGroup();

        // ตัวควบคุม (เปิดอยู่ตลอด เต็มจอ ไม่มีรูปจึงไม่บังการคลิก)
        GameObject controller = FindOrCreate(canvas.transform, "ServiceTimerUI");
        Undo.RegisterFullObjectHierarchyUndo(controller, "Build Service Timer");
        RectTransform controllerRt = controller.GetComponent<RectTransform>();
        controllerRt.anchorMin = Vector2.zero;
        controllerRt.anchorMax = Vector2.one;
        controllerRt.offsetMin = Vector2.zero;
        controllerRt.offsetMax = Vector2.zero;
        ServiceTimerUI ui = GetOrAdd<ServiceTimerUI>(controller);

        // แผงจับเวลา: กลางบน
        GameObject panel = FindOrCreate(controller.transform, "ServiceTimerPanel");
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 1f);
        panelRt.anchorMax = new Vector2(0.5f, 1f);
        panelRt.pivot = new Vector2(0.5f, 1f);
        panelRt.anchoredPosition = new Vector2(0f, -TopMargin);
        panelRt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        Image bg = GetOrAdd<Image>(panel);
        bg.color = PanelColor;
        bg.raycastTarget = false;

        TMP_Text timer = FindOrCreateText(panel.transform, "TimerText", 72);
        SetTop(timer.rectTransform, 8f, 92f);
        timer.text = "03:00.00";
        timer.fontStyle = FontStyles.Bold;

        TMP_Text status = FindOrCreateText(panel.transform, "StatusText", 26);
        SetTop(status.rectTransform, 102f, 40f);
        status.text = "Dishes left 0  ·  Customers 0";

        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("clockPanel").objectReferenceValue = clock != null ? clock.gameObject : null;
        so.FindProperty("timerPanel").objectReferenceValue = panel;
        so.FindProperty("timerText").objectReferenceValue = timer;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.ApplyModifiedProperties();

        // วางต่อจากนาฬิกา (หน้าต่างอื่นๆ เช่น กระเป๋า/เมนู ยังวาดทับได้)
        if (clock != null && clock.transform.parent == canvas.transform)
        {
            controller.transform.SetSiblingIndex(clock.transform.GetSiblingIndex() + 1);
        }

        panel.SetActive(false);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = controller;

        string clockResult = clock != null ? $"Clock Panel = '{clock.name}' (hidden while the shop is open)." : "No ClockUI found: the normal clock will stay visible.";
        Debug.Log($"ServiceTimerLayoutBuilder: Service timer built. {clockResult}");
        EditorUtility.DisplayDialog("Service Timer", "Service timer is ready (top center, shown only while the shop is open).\n\n" + clockResult + "\n\nSave the scene with Ctrl+S.", "OK");
    }

    // ยืดเต็มความกว้าง ยึดด้านบน
    private static void SetTop(RectTransform rt, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(10f, -(top + height));
        rt.offsetMax = new Vector2(-10f, -top);
    }

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

    private static TMP_Text FindOrCreateText(Transform parent, string name, float fontSize)
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
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
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
