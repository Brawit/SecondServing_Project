using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// เครื่องมือตั้งค่าระบบลูกค้าอัตโนมัติ (ใช้ใน Editor เท่านั้น)
// เมนู: Tools > Second Serving > Build Customer System
// - สร้างรูปสี่เหลี่ยมสีขาว (Assets/Prefabs/Placeholder/WhiteSquare.png) ไว้ใช้แทนสไปรต์
// - สร้าง prefab ลูกค้า (Assets/Prefabs/Customer.prefab) และโต๊ะ 2 ที่นั่ง (Assets/Prefabs/Table_2Seats.prefab)
//   (ถ้ามี prefab อยู่แล้วจะไม่เขียนทับ จะได้ไม่ลบที่ปรับเอง)
// - สร้าง GameObject "CustomerManager" (CustomerManager + GridPathfinder) ผูก Tilemap Ground / Obstables
//   และจุดเกิด/ทางออก = ShopEntryFront
// - กดซ้ำได้ กด Ctrl+Z ย้อนการเปลี่ยนแปลงในฉากได้ (ไม่รวมไฟล์ prefab)
public static class CustomerSetupBuilder
{
    private const string PrefabFolder = "Assets/Prefabs";
    private const string PlaceholderFolder = "Assets/Prefabs/Placeholder";
    private const string SquarePath = "Assets/Prefabs/Placeholder/WhiteSquare.png";
    private const string CustomerPath = "Assets/Prefabs/Customer.prefab";
    private const string TablePath = "Assets/Prefabs/Table_2Seats.prefab";

    private static readonly Color TableColor = new Color(0.55f, 0.36f, 0.2f, 1f);
    private static readonly Color ChairColor = new Color(0.75f, 0.55f, 0.35f, 1f);

    [MenuItem("Tools/Second Serving/Build Customer System")]
    public static void Build()
    {
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder(PrefabFolder, "Placeholder");

        Sprite square = GetOrCreateSquareSprite();
        if (square == null)
        {
            EditorUtility.DisplayDialog("Customer System", "Could not create the placeholder sprite.", "OK");
            return;
        }

        string customerResult = CreateCustomerPrefab(square, out Customer customerPrefab);
        string tableResult = CreateTablePrefab(square);

        // ---------- ในฉาก ----------

        Undo.SetCurrentGroupName("Build Customer System");
        int undoGroup = Undo.GetCurrentGroup();

        CustomerManager manager = Object.FindFirstObjectByType<CustomerManager>(FindObjectsInactive.Include);
        GameObject managerGo;
        if (manager == null)
        {
            managerGo = new GameObject("CustomerManager");
            Undo.RegisterCreatedObjectUndo(managerGo, "Create CustomerManager");
            manager = Undo.AddComponent<CustomerManager>(managerGo);
        }
        else
        {
            managerGo = manager.gameObject;
        }

        GridPathfinder pathfinder = Object.FindFirstObjectByType<GridPathfinder>(FindObjectsInactive.Include);
        if (pathfinder == null)
        {
            pathfinder = Undo.AddComponent<GridPathfinder>(managerGo);
        }

        // ที่เก็บลูกค้าที่สร้าง
        Transform parent = managerGo.transform.Find("Customers");
        if (parent == null)
        {
            GameObject p = new GameObject("Customers");
            Undo.RegisterCreatedObjectUndo(p, "Create Customers");
            p.transform.SetParent(managerGo.transform, false);
            parent = p.transform;
        }

        // จุดเกิด/ทางออก
        GameObject entry = GameObject.Find("ShopEntryFront");
        string entryResult = entry != null ? "Entry = ShopEntryFront." : "ShopEntryFront not found: assign Entry Point yourself.";

        SerializedObject managerSo = new SerializedObject(manager);
        managerSo.FindProperty("customerPrefab").objectReferenceValue = customerPrefab;
        managerSo.FindProperty("customerParent").objectReferenceValue = parent;
        if (entry != null)
        {
            managerSo.FindProperty("entryPoint").objectReferenceValue = entry.transform;
            managerSo.FindProperty("exitPoint").objectReferenceValue = entry.transform;
        }
        managerSo.ApplyModifiedProperties();

        // Tilemap: Ground = เดินได้, Obstables/Obstacles = เดินไม่ได้
        Tilemap ground = FindTilemap("Ground");
        Tilemap obstacles = FindTilemap("Obstables") ?? FindTilemap("Obstacles");

        SerializedObject pathSo = new SerializedObject(pathfinder);
        Grid grid = ground != null ? ground.layoutGrid : Object.FindFirstObjectByType<Grid>();
        pathSo.FindProperty("grid").objectReferenceValue = grid;

        SerializedProperty walkable = pathSo.FindProperty("walkableTilemaps");
        walkable.arraySize = ground != null ? 1 : 0;
        if (ground != null) walkable.GetArrayElementAtIndex(0).objectReferenceValue = ground;

        SerializedProperty blocking = pathSo.FindProperty("blockingTilemaps");
        blocking.arraySize = obstacles != null ? 1 : 0;
        if (obstacles != null) blocking.GetArrayElementAtIndex(0).objectReferenceValue = obstacles;
        pathSo.ApplyModifiedProperties();

        string tileResult = $"Walkable = {(ground != null ? ground.name : "(none, all cells)")}, Blocking = {(obstacles != null ? obstacles.name : "(none)")}.";

        int tableCount = Object.FindObjectsByType<Table>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        string seatHint = tableCount == 0
            ? "No tables in the scene yet: drag Assets/Prefabs/Table_2Seats into the shop."
            : $"{tableCount} table(s) in the scene.";

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(managerGo.scene);
        Selection.activeGameObject = managerGo;

        string summary = $"{customerResult}\n{tableResult}\n{entryResult}\n{tileResult}\n{seatHint}";
        Debug.Log($"CustomerSetupBuilder: {summary.Replace('\n', ' ')}");
        EditorUtility.DisplayDialog("Customer System", summary + "\n\nSave the scene with Ctrl+S.", "OK");
    }

    // ---------- Prefab ----------

    private static string CreateCustomerPrefab(Sprite square, out Customer prefab)
    {
        prefab = AssetDatabase.LoadAssetAtPath<Customer>(CustomerPath);
        if (prefab != null)
        {
            return "Customer prefab already exists (kept as is).";
        }

        GameObject root = new GameObject("Customer");
        try
        {
            // ตัวลูกค้า: รูปอยู่ที่ลูก (ย่อขนาดได้โดยไม่กระทบข้อความ)
            GameObject body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 1f);
            SpriteRenderer sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.sortingOrder = 5;

            BoxCollider2D col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.8f, 1f);

            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // ข้อความเหนือหัว
            GameObject bubble = new GameObject("Bubble");
            bubble.transform.SetParent(root.transform, false);
            bubble.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            TextMeshPro text = bubble.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }
            text.text = "...";
            text.fontSize = 3f;
            text.alignment = TextAlignmentOptions.Bottom;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(4f, 1.2f);
            text.sortingOrder = 30;

            Customer customer = root.AddComponent<Customer>();
            SerializedObject so = new SerializedObject(customer);
            so.FindProperty("bubbleText").objectReferenceValue = text;
            so.FindProperty("bodyRenderer").objectReferenceValue = sr;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, CustomerPath);
            prefab = saved != null ? saved.GetComponent<Customer>() : null;
            return prefab != null ? $"Created {CustomerPath}." : "Failed to create the Customer prefab.";
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static string CreateTablePrefab(Sprite square)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TablePath) != null)
        {
            return "Table prefab already exists (kept as is).";
        }

        GameObject root = new GameObject("Table_2Seats");
        try
        {
            root.AddComponent<Table>();

            // โต๊ะ: Collider ทึบ ให้ตัวหาเส้นทางรู้ว่าเดินผ่านไม่ได้
            BoxCollider2D col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = new Vector2(1.6f, 0.8f);

            GameObject top = new GameObject("Top");
            top.transform.SetParent(root.transform, false);
            top.transform.localScale = new Vector3(1.6f, 0.8f, 1f);
            SpriteRenderer topSr = top.AddComponent<SpriteRenderer>();
            topSr.sprite = square;
            topSr.color = TableColor;
            topSr.sortingOrder = 2;

            CreateSeat(root.transform, "Seat_Left", new Vector3(-1.3f, 0f, 0f), square);
            CreateSeat(root.transform, "Seat_Right", new Vector3(1.3f, 0f, 0f), square);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, TablePath);
            return saved != null ? $"Created {TablePath}." : "Failed to create the Table prefab.";
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // เก้าอี้: ไม่มี Collider (ลูกค้าต้องเดินเข้าไปนั่งได้)
    private static void CreateSeat(Transform parent, string name, Vector3 localPosition, Sprite square)
    {
        GameObject seat = new GameObject(name);
        seat.transform.SetParent(parent, false);
        seat.transform.localPosition = localPosition;
        seat.AddComponent<Seat>();

        GameObject look = new GameObject("Chair");
        look.transform.SetParent(seat.transform, false);
        look.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        SpriteRenderer sr = look.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = ChairColor;
        sr.sortingOrder = 1;
    }

    // ---------- รูปสี่เหลี่ยม ----------

    // รูปสีขาว 4x4 พิกเซล = 1 หน่วยพอดี (Pixels Per Unit = 4)
    private static Sprite GetOrCreateSquareSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
        if (existing != null)
        {
            return existing;
        }

        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), SquarePath);
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(SquarePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(SquarePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 4f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
    }

    // ---------- ตัวช่วย ----------

    private static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static Tilemap FindTilemap(string name)
    {
        foreach (Tilemap map in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (map.name == name)
            {
                return map;
            }
        }
        return null;
    }
}
