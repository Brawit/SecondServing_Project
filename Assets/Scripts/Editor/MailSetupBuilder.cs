using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// [2.5c] เมนู: Tools > Second Serving > Build Mail System
// สร้างระบบจดหมายเชฟครบชุด กดซ้ำได้ ส่วนที่มีอยู่แล้วไม่ถูกเขียนทับ (kept):
// - ShopProgress บนวัตถุ ShopManager
// - วัตถุ MailSystem
// - prefab Assets/Prefabs/Mailbox.prefab + วางตู้ 1 ตู้ข้าง OutsideFront (ถ้าฉากยังไม่มีตู้)
// - prefab Assets/Prefabs/PopularBoard.prefab + วางกระดานเมนูนิยม 1 อันอีกฝั่งของ OutsideFront (ถ้าฉากยังไม่มี)
// - prefab Assets/Prefabs/MailDesk.prefab + วางโต๊ะคอม 1 ตัวข้างเตียง (ถ้าฉากยังไม่มี) = กล่องจดหมายที่อ่านแล้ว
// - จดหมายตัวอย่างตอนสลบ Assets/Letters/passout_neighbor.asset ไม่มีของแนบ (ถ้ายังไม่มี) + เติมจดหมายทุกไฟล์ใน Letter Library ของ MailSystem
// - หน้าต่าง MailUI บน Canvas หลัก (ถ้ายังไม่มี / ถ้าเป็นแบบร่างเก่าที่ไม่มีช่องของแนบ = สร้างใหม่)
// - ผูก ShopProgress / MailSystem ให้ SaveManager (ถ้าช่องยังว่าง)
public static class MailSetupBuilder
{
    private const string SquarePath = "Assets/Prefabs/Placeholder/WhiteSquare.png";
    private const string MailboxPath = "Assets/Prefabs/Mailbox.prefab";
    private const string BoardPath = "Assets/Prefabs/PopularBoard.prefab";
    private const string DeskPath = "Assets/Prefabs/MailDesk.prefab";
    private const string LettersFolder = "Assets/Letters";
    private const string PassOutLetterPath = "Assets/Letters/passout_neighbor.asset";
    private static readonly Color DeskColor = new Color(0.55f, 0.38f, 0.24f, 1f);
    private static readonly Color ScreenColor = new Color(0.3f, 0.6f, 0.85f, 1f);
    private static readonly Color SlotColor = new Color(0.85f, 0.8f, 0.68f, 1f);
    private static readonly Color BoardColor = new Color(0.18f, 0.24f, 0.2f, 1f);
    private static readonly Color BoardFrameColor = new Color(0.5f, 0.36f, 0.22f, 1f);

    private static readonly Color MailboxColor = new Color(0.75f, 0.22f, 0.2f, 1f);
    private static readonly Color PostColor = new Color(0.45f, 0.3f, 0.2f, 1f);
    private static readonly Color WindowColor = new Color(0.08f, 0.08f, 0.1f, 0.94f);
    private static readonly Color ListColor = new Color(0f, 0f, 0f, 0.25f);
    private static readonly Color PaperColor = new Color(0.96f, 0.93f, 0.85f, 1f);
    private static readonly Color InkColor = new Color(0.2f, 0.16f, 0.12f, 1f);
    private static readonly Color ButtonColor = new Color(0.25f, 0.25f, 0.3f, 1f);

    [MenuItem("Tools/Second Serving/Build Mail System")]
    public static void Build()
    {
        List<string> report = new List<string>();

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        if (square == null)
        {
            report.Add($"{SquarePath} not found (run Build Customer System or Build Cooking System first). The mailbox will have no sprite.");
        }

        Mailbox prefab = CreateMailboxPrefab(square, report);
        PopularBoard boardPrefab = CreateBoardPrefab(square, report);
        MailDesk deskPrefab = CreateDeskPrefab(square, report);
        EnsureSampleLetters(report);

        Undo.SetCurrentGroupName("Build Mail System");
        int undoGroup = Undo.GetCurrentGroup();

        ShopProgress progress = EnsureShopProgress(report);
        MailSystem mail = EnsureMailSystem(report);
        PlaceMailbox(prefab, report);
        PlaceBoard(boardPrefab, report);
        PlaceDesk(deskPrefab, report);
        FillLetterLibrary(mail, report);

        Canvas canvas = FindMainCanvas();
        if (canvas == null)
        {
            report.Add("No Canvas found: MailUI was not built.");
        }
        else
        {
            BuildMailWindow(canvas, report);
        }

        LinkSaveManager(progress, mail, report);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        string summary = string.Join("\n", report);
        Debug.Log($"MailSetupBuilder: {summary.Replace('\n', ' ')}");
        EditorUtility.DisplayDialog("Mail System", summary + "\n\nSave the scene with Ctrl+S.", "OK");
    }

    // ---------- ระบบ ----------

    private static ShopProgress EnsureShopProgress(List<string> report)
    {
        ShopProgress existing = Object.FindFirstObjectByType<ShopProgress>(FindObjectsInactive.Include);
        if (existing != null)
        {
            report.Add($"ShopProgress already on '{existing.name}' (kept).");
            return existing;
        }

        ShopManager shop = Object.FindFirstObjectByType<ShopManager>(FindObjectsInactive.Include);
        GameObject host = shop != null ? shop.gameObject : new GameObject("ShopProgress");
        if (shop == null)
        {
            Undo.RegisterCreatedObjectUndo(host, "Create ShopProgress");
        }

        ShopProgress progress = Undo.AddComponent<ShopProgress>(host);
        report.Add($"Added ShopProgress to '{host.name}'.");
        return progress;
    }

    private static MailSystem EnsureMailSystem(List<string> report)
    {
        MailSystem existing = Object.FindFirstObjectByType<MailSystem>(FindObjectsInactive.Include);
        if (existing != null)
        {
            report.Add("MailSystem already in the scene (kept).");
            return existing;
        }

        GameObject go = new GameObject("MailSystem");
        Undo.RegisterCreatedObjectUndo(go, "Create MailSystem");
        MailSystem mail = go.AddComponent<MailSystem>();

        PlayerInteractor player = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        if (player != null)
        {
            SerializedObject so = new SerializedObject(mail);
            so.FindProperty("recipeBook").objectReferenceValue = player.GetComponent<RecipeBook>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        report.Add("Created MailSystem.");
        return mail;
    }

    private static void LinkSaveManager(ShopProgress progress, MailSystem mail, List<string> report)
    {
        SaveManager save = Object.FindFirstObjectByType<SaveManager>(FindObjectsInactive.Include);
        if (save == null)
        {
            report.Add("SaveManager not found: progress and letters will not be saved.");
            return;
        }

        SerializedObject so = new SerializedObject(save);
        SerializedProperty progressProp = so.FindProperty("shopProgress");
        SerializedProperty mailProp = so.FindProperty("mailSystem");
        bool changed = false;

        if (progressProp != null && progressProp.objectReferenceValue == null && progress != null)
        {
            progressProp.objectReferenceValue = progress;
            changed = true;
        }
        if (mailProp != null && mailProp.objectReferenceValue == null && mail != null)
        {
            mailProp.objectReferenceValue = mail;
            changed = true;
        }

        if (changed)
        {
            so.ApplyModifiedProperties();
            report.Add("Linked ShopProgress / MailSystem to SaveManager.");
        }
        else
        {
            report.Add("SaveManager links already set (kept).");
        }
    }

    // ---------- ตู้จดหมาย ----------

    private static Mailbox CreateMailboxPrefab(Sprite square, List<string> report)
    {
        Mailbox existing = AssetDatabase.LoadAssetAtPath<Mailbox>(MailboxPath);
        if (existing != null)
        {
            report.Add("Mailbox prefab already exists (kept as is).");
            return existing;
        }

        GameObject root = new GameObject("Mailbox");
        try
        {
            // เสา
            GameObject post = new GameObject("Post");
            post.transform.SetParent(root.transform, false);
            post.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            post.transform.localScale = new Vector3(0.18f, 0.5f, 1f);
            SpriteRenderer postSr = post.AddComponent<SpriteRenderer>();
            postSr.sprite = square;
            postSr.color = PostColor;
            postSr.sortingOrder = 2;

            // ตัวตู้
            GameObject body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.45f, 1f);
            SpriteRenderer bodySr = body.AddComponent<SpriteRenderer>();
            bodySr.sprite = square;
            bodySr.color = MailboxColor;
            bodySr.sortingOrder = 3;

            // Collider ทึบ: ผู้เล่นกด E ได้ในระยะ
            BoxCollider2D col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.offset = new Vector2(0f, 0f);
            col.size = new Vector2(0.7f, 0.8f);

            GameObject label = new GameObject("Label");
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            TextMeshPro text = label.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.text = "";
            text.fontSize = 3f;
            text.alignment = TextAlignmentOptions.Bottom;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(4f, 1.4f);
            text.rectTransform.pivot = new Vector2(0.5f, 0f);
            text.sortingOrder = 30;

            Mailbox mailbox = root.AddComponent<Mailbox>();
            SerializedObject so = new SerializedObject(mailbox);
            so.FindProperty("labelText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, MailboxPath);
            report.Add(saved != null ? $"Created {MailboxPath}." : "Failed to create the Mailbox prefab.");
            return saved != null ? saved.GetComponent<Mailbox>() : null;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void PlaceMailbox(Mailbox prefab, List<string> report)
    {
        int count = Object.FindObjectsByType<Mailbox>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        if (count > 0)
        {
            report.Add($"{count} mailbox(es) already in the scene (kept).");
            return;
        }

        if (prefab == null)
        {
            report.Add("No Mailbox prefab: place one yourself.");
            return;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
        Undo.RegisterCreatedObjectUndo(go, "Place Mailbox");

        GameObject anchor = GameObject.Find("OutsideFront");
        if (anchor != null)
        {
            go.transform.position = anchor.transform.position + new Vector3(1.5f, 0f, 0f);
            report.Add("Placed a Mailbox 1.5 units right of OutsideFront: move it where you like.");
        }
        else
        {
            report.Add("OutsideFront not found: placed a Mailbox at (0, 0). Move it outside the shop.");
        }

        Selection.activeGameObject = go;
    }

    // ---------- กระดานเมนูนิยม ----------

    private static PopularBoard CreateBoardPrefab(Sprite square, List<string> report)
    {
        PopularBoard existing = AssetDatabase.LoadAssetAtPath<PopularBoard>(BoardPath);
        if (existing != null)
        {
            report.Add("PopularBoard prefab already exists (kept as is).");
            return existing;
        }

        GameObject root = new GameObject("PopularBoard");
        try
        {
            GameObject frame = new GameObject("Frame");
            frame.transform.SetParent(root.transform, false);
            frame.transform.localScale = new Vector3(1.0f, 0.75f, 1f);
            SpriteRenderer frameSr = frame.AddComponent<SpriteRenderer>();
            frameSr.sprite = square;
            frameSr.color = BoardFrameColor;
            frameSr.sortingOrder = 2;

            GameObject board = new GameObject("Board");
            board.transform.SetParent(root.transform, false);
            board.transform.localScale = new Vector3(0.86f, 0.6f, 1f);
            SpriteRenderer boardSr = board.AddComponent<SpriteRenderer>();
            boardSr.sprite = square;
            boardSr.color = BoardColor;
            boardSr.sortingOrder = 3;

            BoxCollider2D col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = new Vector2(1.0f, 0.75f);

            GameObject label = new GameObject("BoardText");
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            TextMeshPro text = label.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.text = "";
            text.fontSize = 2.6f;
            text.alignment = TextAlignmentOptions.Bottom;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(8f, 3f);
            text.rectTransform.pivot = new Vector2(0.5f, 0f);
            text.sortingOrder = 30;

            PopularBoard popularBoard = root.AddComponent<PopularBoard>();
            SerializedObject so = new SerializedObject(popularBoard);
            so.FindProperty("boardText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, BoardPath);
            report.Add(saved != null ? $"Created {BoardPath}." : "Failed to create the PopularBoard prefab.");
            return saved != null ? saved.GetComponent<PopularBoard>() : null;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void PlaceBoard(PopularBoard prefab, List<string> report)
    {
        int count = Object.FindObjectsByType<PopularBoard>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        if (count > 0)
        {
            report.Add($"{count} popular board(s) already in the scene (kept).");
            return;
        }

        if (prefab == null)
        {
            report.Add("No PopularBoard prefab: place one yourself.");
            return;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
        Undo.RegisterCreatedObjectUndo(go, "Place PopularBoard");

        GameObject anchor = GameObject.Find("OutsideFront");
        if (anchor != null)
        {
            go.transform.position = anchor.transform.position + new Vector3(-1.5f, 0f, 0f);
            report.Add("Placed a PopularBoard 1.5 units left of OutsideFront: move it where you like.");
        }
        else
        {
            report.Add("OutsideFront not found: placed a PopularBoard at (0, 0). Move it outside the shop.");
        }
    }

    // ---------- โต๊ะคอม ----------

    private static MailDesk CreateDeskPrefab(Sprite square, List<string> report)
    {
        MailDesk existing = AssetDatabase.LoadAssetAtPath<MailDesk>(DeskPath);
        if (existing != null)
        {
            report.Add("MailDesk prefab already exists (kept as is).");
            return existing;
        }

        GameObject root = new GameObject("MailDesk");
        try
        {
            GameObject top = new GameObject("Desk");
            top.transform.SetParent(root.transform, false);
            top.transform.localScale = new Vector3(1.3f, 0.6f, 1f);
            SpriteRenderer topSr = top.AddComponent<SpriteRenderer>();
            topSr.sprite = square;
            topSr.color = DeskColor;
            topSr.sortingOrder = 2;

            // คอม (จอเล็กๆ บนโต๊ะ)
            GameObject screen = new GameObject("PC");
            screen.transform.SetParent(root.transform, false);
            screen.transform.localPosition = new Vector3(-0.25f, 0.18f, 0f);
            screen.transform.localScale = new Vector3(0.45f, 0.32f, 1f);
            SpriteRenderer screenSr = screen.AddComponent<SpriteRenderer>();
            screenSr.sprite = square;
            screenSr.color = ScreenColor;
            screenSr.sortingOrder = 3;

            // กองจดหมาย
            GameObject letters = new GameObject("Letters");
            letters.transform.SetParent(root.transform, false);
            letters.transform.localPosition = new Vector3(0.35f, 0.05f, 0f);
            letters.transform.localScale = new Vector3(0.3f, 0.2f, 1f);
            SpriteRenderer lettersSr = letters.AddComponent<SpriteRenderer>();
            lettersSr.sprite = square;
            lettersSr.color = PaperColor;
            lettersSr.sortingOrder = 3;

            BoxCollider2D col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = new Vector2(1.3f, 0.6f);

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

            MailDesk desk = root.AddComponent<MailDesk>();
            SerializedObject so = new SerializedObject(desk);
            so.FindProperty("labelText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, DeskPath);
            report.Add(saved != null ? $"Created {DeskPath}." : "Failed to create the MailDesk prefab.");
            return saved != null ? saved.GetComponent<MailDesk>() : null;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void PlaceDesk(MailDesk prefab, List<string> report)
    {
        int count = Object.FindObjectsByType<MailDesk>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        if (count > 0)
        {
            report.Add($"{count} mail desk(s) already in the scene (kept).");
            return;
        }

        if (prefab == null)
        {
            report.Add("No MailDesk prefab: place one yourself.");
            return;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
        Undo.RegisterCreatedObjectUndo(go, "Place MailDesk");

        Bed bed = Object.FindFirstObjectByType<Bed>(FindObjectsInactive.Include);
        if (bed != null)
        {
            go.transform.position = bed.transform.position + new Vector3(1.6f, 0f, 0f);
            report.Add("Placed a MailDesk 1.6 units right of the Bed: move it where you like in the bedroom.");
        }
        else
        {
            report.Add("Bed not found: placed a MailDesk at (0, 0). Move it into the bedroom.");
        }
    }

    // ---------- จดหมายทั่วไป ----------

    // จดหมายตัวอย่างตอนสลบ (ผู้ส่งชั่วคราว เฟส 3 เปลี่ยนเป็น NPC จริง)
    private static void EnsureSampleLetters(List<string> report)
    {
        if (AssetDatabase.LoadAssetAtPath<LetterData>(PassOutLetterPath) != null)
        {
            report.Add("Sample pass-out letter already exists (kept).");
            return;
        }

        if (!AssetDatabase.IsValidFolder(LettersFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Letters");
        }

        LetterData letter = ScriptableObject.CreateInstance<LetterData>();
        letter.letterId = "passout_neighbor";
        letter.senderName = "A worried neighbor";
        letter.senderSubtitle = "";
        letter.subject = "Are you okay?";
        letter.body =
            "Hello,\n\n" +
            "I found you asleep on the path in the middle of the night. You gave me quite a scare! " +
            "A few of us helped get you to the clinic.\n\n" +
            "I know running a shop on your own is a lot of work, but please don't push yourself so hard. " +
            "Go to bed before the moon gets too high, okay?";
        letter.signOff = "Take care of yourself.";
        letter.trigger = LetterTrigger.PassOut;

        // ไม่มีของแนบ: จดหมายนี้แค่บอกว่ามีคนเจอผู้เล่นสลบแล้วพาไปส่ง (ผู้ใช้ตัดสิน)
        AssetDatabase.CreateAsset(letter, PassOutLetterPath);
        AssetDatabase.SaveAssets();
        report.Add($"Created {PassOutLetterPath}.");
    }

    // ใส่ LetterData ทุกไฟล์ในโปรเจกต์ที่ยังไม่อยู่ใน Letter Library ของ MailSystem (ไม่ลบของเดิม)
    private static void FillLetterLibrary(MailSystem mail, List<string> report)
    {
        if (mail == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(mail);
        SerializedProperty list = so.FindProperty("letterLibrary");
        HashSet<Object> existing = new HashSet<Object>();
        for (int i = 0; i < list.arraySize; i++)
        {
            Object item = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (item != null) existing.Add(item);
        }

        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:LetterData"))
        {
            LetterData letter = AssetDatabase.LoadAssetAtPath<LetterData>(AssetDatabase.GUIDToAssetPath(guid));
            if (letter == null || existing.Contains(letter)) continue;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = letter;
            added++;
        }

        if (added > 0)
        {
            so.ApplyModifiedProperties();
            report.Add($"Added {added} letter(s) to MailSystem's Letter Library.");
        }
        else
        {
            report.Add("MailSystem's Letter Library is up to date.");
        }
    }

    // ---------- หน้าต่างจดหมาย ----------

    private static void BuildMailWindow(Canvas canvas, List<string> report)
    {
        MailUI existing = Object.FindFirstObjectByType<MailUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            SerializedObject check = new SerializedObject(existing);
            SerializedProperty slotProp = check.FindProperty("attachmentTemplate");
            if (slotProp != null && slotProp.objectReferenceValue != null)
            {
                report.Add($"MailUI already exists on '{existing.name}' (kept, layout not touched).");
                return;
            }

            // แบบร่างเก่า (ยังไม่มีช่องของแนบ) = ลบแล้วสร้างใหม่
            Undo.DestroyObjectImmediate(existing.gameObject);
            report.Add("Removed the old MailUI (no attachment slots) and rebuilt it.");
        }

        GameObject host = CreateUI(canvas.transform, "MailUI");
        SetStretch(host.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        GameObject window = CreateUI(host.transform, "MailWindow");
        RectTransform windowRt = window.GetComponent<RectTransform>();
        windowRt.anchorMin = new Vector2(0.5f, 0.5f);
        windowRt.anchorMax = new Vector2(0.5f, 0.5f);
        windowRt.pivot = new Vector2(0.5f, 0.5f);
        windowRt.anchoredPosition = new Vector2(0f, 30f);
        windowRt.sizeDelta = new Vector2(1200f, 800f);
        window.AddComponent<Image>().color = WindowColor;

        TMP_Text title = CreateText(window.transform, "TitleText", 36, TextAlignmentOptions.Center);
        SetTopStretch(title.rectTransform, 20f, 20f, 18f, 46f);
        title.text = "Mail";
        title.fontStyle = FontStyles.Bold;

        TMP_Text hint = CreateText(window.transform, "HintText", 20, TextAlignmentOptions.Center);
        SetTopStretch(hint.rectTransform, 20f, 20f, 66f, 28f);
        hint.color = new Color(1f, 1f, 1f, 0.7f);

        // ---------- ซ้าย: รายการจดหมาย (โหมด Archive) ----------
        GameObject listScroll = CreateScrollView(window.transform, "ListScrollView", ListColor, out RectTransform listContent, 6f, 8);
        RectTransform listRt = listScroll.GetComponent<RectTransform>();
        listRt.anchorMin = new Vector2(0f, 0f);
        listRt.anchorMax = new Vector2(0f, 1f);
        listRt.pivot = new Vector2(0f, 0.5f);
        listRt.offsetMin = new Vector2(24f, 90f);
        listRt.offsetMax = new Vector2(24f + 380f, -106f);

        MailListItemUI template = BuildItemTemplate(listContent);

        // ---------- จดหมาย (กระดาษ) ----------
        GameObject letter = CreateUI(window.transform, "LetterPanel");
        RectTransform letterRt = letter.GetComponent<RectTransform>();
        SetStretch(letterRt, 428f, 24f, 106f, 90f);
        letter.AddComponent<Image>().color = PaperColor;

        TMP_Text from = CreateText(letter.transform, "FromText", 24, TextAlignmentOptions.Left);
        SetTopStretch(from.rectTransform, 28f, 28f, 16f, 32f);
        from.color = InkColor;
        from.fontStyle = FontStyles.Bold;

        TMP_Text day = CreateText(letter.transform, "DayText", 18, TextAlignmentOptions.Left);
        SetTopStretch(day.rectTransform, 28f, 28f, 50f, 24f);
        day.color = new Color(InkColor.r, InkColor.g, InkColor.b, 0.65f);

        GameObject bodyScroll = CreateScrollView(letter.transform, "LetterScrollView", new Color(0f, 0f, 0f, 0f), out RectTransform bodyContent, 14f, 0);
        SetStretch(bodyScroll.GetComponent<RectTransform>(), 28f, 20f, 84f, 150f);
        ScrollRect bodyScrollRect = bodyScroll.GetComponent<ScrollRect>();

        TMP_Text body = CreateText(bodyContent, "BodyText", 22, TextAlignmentOptions.TopLeft);
        body.color = InkColor;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.lineSpacing = 6f;
        body.raycastTarget = false;

        TMP_Text signOff = CreateText(bodyContent, "SignOffText", 22, TextAlignmentOptions.TopRight);
        signOff.color = InkColor;
        signOff.fontStyle = FontStyles.Italic;
        signOff.textWrappingMode = TextWrappingModes.Normal;
        signOff.raycastTarget = false;

        // ช่องของแนบ: ด้านล่างตรงกลางกระดาษ
        GameObject row = CreateUI(letter.transform, "AttachmentRow");
        RectTransform rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 0f);
        rowRt.anchorMax = new Vector2(1f, 0f);
        rowRt.pivot = new Vector2(0.5f, 0f);
        rowRt.offsetMin = new Vector2(28f, 16f);
        rowRt.offsetMax = new Vector2(-28f, 16f + 118f);
        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 16f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        LetterAttachmentSlotUI slotTemplate = BuildSlotTemplate(row.transform);

        TMP_Text empty = CreateText(window.transform, "EmptyText", 24, TextAlignmentOptions.Center);
        SetStretch(empty.rectTransform, 440f, 40f, 300f, 300f);
        empty.text = "No letters yet.";
        empty.color = new Color(1f, 1f, 1f, 0.6f);
        empty.raycastTarget = false;
        empty.gameObject.SetActive(false);

        Button close = BuildButton(window.transform, "CloseButton", "Close", ButtonColor, 24);
        RectTransform closeRt = close.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.5f, 0f);
        closeRt.anchorMax = new Vector2(0.5f, 0f);
        closeRt.pivot = new Vector2(0.5f, 0f);
        closeRt.anchoredPosition = new Vector2(0f, 24f);
        closeRt.sizeDelta = new Vector2(220f, 50f);

        MailUI ui = host.AddComponent<MailUI>();
        SerializedObject so = new SerializedObject(ui);
        PlayerInteractor player = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        if (player != null)
        {
            so.FindProperty("playerController").objectReferenceValue = player.GetComponent<PlayerController>();
        }
        so.FindProperty("windowRoot").objectReferenceValue = window;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("hintText").objectReferenceValue = hint;
        so.FindProperty("listRoot").objectReferenceValue = listScroll;
        so.FindProperty("listContainer").objectReferenceValue = listContent;
        so.FindProperty("itemTemplate").objectReferenceValue = template;
        so.FindProperty("emptyText").objectReferenceValue = empty;
        so.FindProperty("letterPanel").objectReferenceValue = letterRt;
        so.FindProperty("fromText").objectReferenceValue = from;
        so.FindProperty("dayText").objectReferenceValue = day;
        so.FindProperty("bodyText").objectReferenceValue = body;
        so.FindProperty("signOffText").objectReferenceValue = signOff;
        so.FindProperty("letterScroll").objectReferenceValue = bodyScrollRect;
        so.FindProperty("attachmentContainer").objectReferenceValue = row.transform;
        so.FindProperty("attachmentTemplate").objectReferenceValue = slotTemplate;
        so.FindProperty("closeButton").objectReferenceValue = close;
        so.ApplyModifiedPropertiesWithoutUndo();

        window.SetActive(false);
        host.transform.SetAsLastSibling();
        report.Add($"Built MailUI on '{canvas.name}'.");
    }

    private static LetterAttachmentSlotUI BuildSlotTemplate(Transform parent)
    {
        GameObject slot = CreateUI(parent, "AttachmentTemplate");
        Image bg = slot.AddComponent<Image>();
        bg.color = Color.white;

        Button button = slot.AddComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.normalColor = SlotColor;
        colors.highlightedColor = new Color(1f, 0.88f, 0.55f, 1f);
        colors.pressedColor = new Color(1f, 0.8f, 0.4f, 1f);
        colors.selectedColor = SlotColor;
        colors.disabledColor = new Color(SlotColor.r, SlotColor.g, SlotColor.b, 0.45f);
        button.colors = colors;

        LayoutElement le = slot.AddComponent<LayoutElement>();
        le.preferredWidth = 180f;
        le.preferredHeight = 110f;

        // กรอบเรืองตอนยังไม่ได้รับ
        GameObject glow = CreateUI(slot.transform, "Highlight");
        SetStretch(glow.GetComponent<RectTransform>(), -4f, -4f, -4f, -4f);
        Image glowImage = glow.AddComponent<Image>();
        glowImage.color = new Color(1f, 0.82f, 0.29f, 0.55f);
        glowImage.raycastTarget = false;
        glow.transform.SetAsFirstSibling();

        GameObject iconGo = CreateUI(slot.transform, "Icon");
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 1f);
        iconRt.anchorMax = new Vector2(0.5f, 1f);
        iconRt.pivot = new Vector2(0.5f, 1f);
        iconRt.anchoredPosition = new Vector2(0f, -8f);
        iconRt.sizeDelta = new Vector2(48f, 48f);
        Image icon = iconGo.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;

        TMP_Text label = CreateText(slot.transform, "Label", 18, TextAlignmentOptions.Bottom);
        SetStretch(label.rectTransform, 6f, 6f, 8f, 8f);
        label.color = InkColor;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;

        LetterAttachmentSlotUI slotUI = slot.AddComponent<LetterAttachmentSlotUI>();
        SerializedObject so = new SerializedObject(slotUI);
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("iconImage").objectReferenceValue = icon;
        so.FindProperty("labelText").objectReferenceValue = label;
        so.FindProperty("highlight").objectReferenceValue = glow;
        so.ApplyModifiedPropertiesWithoutUndo();

        slot.SetActive(false);
        return slotUI;
    }

    private static MailListItemUI BuildItemTemplate(Transform parent)
    {
        GameObject item = CreateUI(parent, "MailItemTemplate");
        Image bg = item.AddComponent<Image>();
        bg.color = Color.white;

        Button button = item.AddComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.12f);
        colors.highlightedColor = new Color(1f, 0.85f, 0.4f, 0.35f);
        colors.pressedColor = new Color(1f, 0.85f, 0.4f, 0.55f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0.12f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.03f);
        button.colors = colors;

        LayoutElement le = item.AddComponent<LayoutElement>();
        le.preferredHeight = 70f;
        le.minHeight = 70f;

        // กรอบฉบับที่กำลังอ่าน
        GameObject frame = CreateUI(item.transform, "SelectedFrame");
        SetStretch(frame.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        Image frameImage = frame.AddComponent<Image>();
        frameImage.color = new Color(1f, 0.82f, 0.29f, 0.3f);
        frameImage.raycastTarget = false;
        frame.SetActive(false);

        TMP_Text titleText = CreateText(item.transform, "TitleText", 24, TextAlignmentOptions.Left);
        SetTopStretch(titleText.rectTransform, 14f, 14f, 6f, 32f);
        titleText.overflowMode = TextOverflowModes.Ellipsis;
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        titleText.raycastTarget = false;

        TMP_Text infoText = CreateText(item.transform, "InfoText", 18, TextAlignmentOptions.Left);
        SetTopStretch(infoText.rectTransform, 14f, 14f, 38f, 26f);
        infoText.color = new Color(1f, 1f, 1f, 0.7f);
        infoText.overflowMode = TextOverflowModes.Ellipsis;
        infoText.textWrappingMode = TextWrappingModes.NoWrap;
        infoText.raycastTarget = false;

        MailListItemUI itemUI = item.AddComponent<MailListItemUI>();
        SerializedObject so = new SerializedObject(itemUI);
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("titleText").objectReferenceValue = titleText;
        so.FindProperty("infoText").objectReferenceValue = infoText;
        so.FindProperty("selectedFrame").objectReferenceValue = frame;
        so.ApplyModifiedPropertiesWithoutUndo();

        item.SetActive(false);
        return itemUI;
    }

    // Scroll View แนวตั้ง (Viewport + Content ที่ยืดตามเนื้อหา) คืนตัว Scroll View และ Content
    private static GameObject CreateScrollView(Transform parent, string name, Color background, out RectTransform content, float spacing, int padding)
    {
        GameObject scrollGo = CreateUI(parent, name);
        Image bg = scrollGo.AddComponent<Image>();
        bg.color = background;

        ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        GameObject viewportGo = CreateUI(scrollGo.transform, "Viewport");
        RectTransform viewportRt = viewportGo.GetComponent<RectTransform>();
        SetStretch(viewportRt, 0f, 0f, 0f, 0f);
        viewportGo.AddComponent<RectMask2D>();

        GameObject contentGo = CreateUI(viewportGo.transform, "Content");
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRt;
        scroll.content = content;
        return scrollGo;
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

    // ---------- ตัวช่วย ----------

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

        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.isRootCanvas)
            {
                return c;
            }
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
