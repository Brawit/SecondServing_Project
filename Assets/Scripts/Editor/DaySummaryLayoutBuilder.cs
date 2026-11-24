using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// [2.6] เมนู: Tools > Second Serving > Build Day Summary Layout
// จัดหน้าต่างสรุปวันที่มีอยู่ในฉากใหม่ (เดิมพื้นขาวทับตัวหนังสือขาว และกล่องข้อความเล็ก 200x50 ตัวหนังสือล้น)
// - พื้นหน้าต่างสีเข้ม 900x660 / หัวข้อด้านบน / เนื้อหาเต็มหน้าต่าง ชิดซ้าย ขึ้นบรรทัดได้ / ปุ่ม Continue ด้านล่าง
// ⚠️ เป็นตัวจัด UI: ห้ามกดหลังขัดเกลาหน้าต่างเองแล้ว (จะจัดกลับเป็นแบบนี้)
public static class DaySummaryLayoutBuilder
{
    private static readonly Color WindowColor = new Color(0.08f, 0.08f, 0.1f, 0.94f);
    private static readonly Color ButtonColor = new Color(0.25f, 0.5f, 0.3f, 1f);

    [MenuItem("Tools/Second Serving/Build Day Summary Layout")]
    public static void Build()
    {
        DaySummaryUI ui = Object.FindFirstObjectByType<DaySummaryUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            EditorUtility.DisplayDialog("Day Summary", "DaySummaryUI not found in the scene.", "OK");
            return;
        }

        SerializedObject so = new SerializedObject(ui);
        GameObject window = so.FindProperty("windowRoot").objectReferenceValue as GameObject;
        TMP_Text title = so.FindProperty("titleText").objectReferenceValue as TMP_Text;
        TMP_Text body = so.FindProperty("bodyText").objectReferenceValue as TMP_Text;
        Button button = so.FindProperty("continueButton").objectReferenceValue as Button;

        if (window == null || body == null)
        {
            EditorUtility.DisplayDialog("Day Summary", "DaySummaryUI: Window Root or Body Text is not assigned.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Build Day Summary Layout");
        int group = Undo.GetCurrentGroup();

        // หน้าต่าง
        RectTransform windowRt = window.GetComponent<RectTransform>();
        Undo.RecordObject(windowRt, "Layout");
        windowRt.anchorMin = new Vector2(0.5f, 0.5f);
        windowRt.anchorMax = new Vector2(0.5f, 0.5f);
        windowRt.pivot = new Vector2(0.5f, 0.5f);
        windowRt.anchoredPosition = Vector2.zero;
        windowRt.sizeDelta = new Vector2(900f, 660f);

        Image bg = window.GetComponent<Image>();
        if (bg == null) bg = Undo.AddComponent<Image>(window);
        Undo.RecordObject(bg, "Layout");
        bg.color = WindowColor;

        // หัวข้อ
        if (title != null)
        {
            Undo.RecordObject(title.rectTransform, "Layout");
            Undo.RecordObject(title, "Layout");
            SetTopStretch(title.rectTransform, 30f, 30f, 24f, 52f);
            title.fontSize = 38;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;
        }

        // เนื้อหา
        Undo.RecordObject(body.rectTransform, "Layout");
        Undo.RecordObject(body, "Layout");
        SetStretch(body.rectTransform, 56f, 56f, 96f, 110f);
        body.fontSize = 24;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Overflow;
        body.lineSpacing = 8f;
        body.color = Color.white;
        body.enableAutoSizing = true;
        body.fontSizeMin = 16;
        body.fontSizeMax = 24;

        // ปุ่ม
        if (button != null)
        {
            RectTransform buttonRt = button.GetComponent<RectTransform>();
            Undo.RecordObject(buttonRt, "Layout");
            buttonRt.anchorMin = new Vector2(0.5f, 0f);
            buttonRt.anchorMax = new Vector2(0.5f, 0f);
            buttonRt.pivot = new Vector2(0.5f, 0f);
            buttonRt.anchoredPosition = new Vector2(0f, 30f);
            buttonRt.sizeDelta = new Vector2(240f, 56f);

            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                Undo.RecordObject(buttonImage, "Layout");
                buttonImage.color = ButtonColor;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                Undo.RecordObject(label, "Layout");
                Undo.RecordObject(label.rectTransform, "Layout");
                SetStretch(label.rectTransform, 0f, 0f, 0f, 0f);
                label.text = "Continue";
                label.fontSize = 26;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
            }
            else
            {
                Text legacy = button.GetComponentInChildren<Text>(true);
                if (legacy != null)
                {
                    Undo.RecordObject(legacy, "Layout");
                    legacy.text = "Continue";
                    legacy.fontSize = 24;
                    legacy.color = Color.white;
                    legacy.alignment = TextAnchor.MiddleCenter;
                }
            }
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(window.scene);
        Debug.Log("DaySummaryLayoutBuilder: Day summary window laid out (dark 900x660, full-size body text).");
        EditorUtility.DisplayDialog("Day Summary", "Day summary window laid out.\n\nSave the scene with Ctrl+S.", "OK");
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
