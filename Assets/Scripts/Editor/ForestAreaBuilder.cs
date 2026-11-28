using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// [2.6c] เมนู: Tools > Second Serving > Build Forest Test Area
// สร้างพื้นที่ป่าทดลองใต้บ้าน + ของป่าสำหรับสูตรเริ่มต้น กดซ้ำได้ (ส่วนที่มีอยู่แล้วไม่ถูกเขียนทับ = kept):
// - prefab ของป่า 13 ชิ้นใน Assets/Prefabs/Forage/Pickup_<itemId>.prefab (โคลนจาก Corn_Pickup) มีแล้ว = kept
// - ทาสีแผนที่: เจาะกำแพงล่างของลาน, ทุ่งหญ้า + ทางดิน, ป่า 30x20, กำแพงรอบ, CameraBoundsMap (ทาซ้ำได้ผลเท่าเดิม)
// - โซนงอกใต้ ForageSpawner: Zone_Field / Zone_Orchard / Zone_EggNest (มีชื่อนี้แล้ว = kept ไม่ทับค่าที่จูน)
// - ย้าย Zone_MushroomForest ลงป่าลึก (เฉพาะตอนยังอยู่ในลานเดิม) / ปิด Zone_CornForest (ไม่ลบ ลบเองได้)
// ทุกอย่างอยู่ใน Undo ครั้งเดียว (Ctrl+Z ย้อนได้) แต่ไฟล์ prefab ที่สร้างแล้วจะไม่ถูกลบตอน Undo
// พิกัดทั้งหมดเป็น "ช่อง" ของ Tilemap (1 ช่อง = 1 หน่วย) แก้ได้ที่ค่าคงที่ด้านล่าง
public static class ForestAreaBuilder
{
    // ---------- ไฟล์ ----------
    private const string ForageFolder = "Assets/Prefabs/Forage";
    private const string TemplatePrefabPath = "Assets/Prefabs/Corn_Pickup.prefab";
    private const string MushroomPrefabPath = "Assets/Prefabs/Mushroom_Pickup.prefab";
    private const string GrassTilePath = "Assets/Tiles/Grass.asset";
    private const string DirtTilePath = "Assets/Tiles/Dirt.asset";
    private const string WallTilePath = "Assets/Tiles/Wall.asset";
    private const string BoundTilePath = "Assets/Tiles/Bound.asset";

    // ---------- ชื่อ Tilemap ในฉาก (Obstables สะกดตามฉากจริง) ----------
    private const string GroundName = "Ground";
    private const string ObstaclesName = "Obstables";
    private const string CameraBoundsName = "CameraBoundsMap";

    // ---------- ชื่อโซน ----------
    private const string FieldZoneName = "Zone_Field";
    private const string OrchardZoneName = "Zone_Orchard";
    private const string EggZoneName = "Zone_EggNest";
    private const string MushroomZoneName = "Zone_MushroomForest";
    private const string CornZoneName = "Zone_CornForest";

    // ---------- พิกัดแผนที่ (ช่อง) ----------
    // ลานเดิม: พื้น x -8..8, y -8..6 กำแพง x = ±9, y = 7 และ y = -9
    private const int YardWallY = -9;
    private const int GapMinX = -2, GapMaxX = 2;            // ช่องที่เจาะในกำแพงล่าง

    // ทุ่งหญ้า (ทางลงป่า) กว้างเท่าลาน กล้องจะได้ไม่แตกเป็น 2 เกาะ
    private const int MeadowMinX = -8, MeadowMaxX = 8;
    private const int MeadowTopY = -9, MeadowBottomY = -29;  // แถว -9 คือแถวกำแพงเดิม (ทาพื้น + Bound ใต้กำแพง)
    private const int MeadowWallX = 9;                        // กำแพงข้างที่ x = -9 และ x = 9

    // ป่า (พื้นที่เดินได้)
    private const int ForestMinX = -15, ForestMaxX = 14;
    private const int ForestTopY = -30, ForestBottomY = -49;

    // ทางดินตรงกลาง
    private const int PathMinX = -1, PathMaxX = 0;
    private const int PathTopY = -9, PathBottomY = -32;

    // ---------- ของป่าและสี placeholder ----------
    private struct ForageDef
    {
        public string itemId;
        public Color color;
        public ForageDef(string id, Color c) { itemId = id; color = c; }
    }

    private static readonly ForageDef[] ForageDefs =
    {
        // ทุ่งผัก
        new ForageDef("chili", new Color(0.9f, 0.1f, 0.1f, 1f)),
        new ForageDef("tomato", new Color(1f, 0.35f, 0.25f, 1f)),
        new ForageDef("peanut", new Color(0.75f, 0.55f, 0.35f, 1f)),
        new ForageDef("cabbage", new Color(0.6f, 0.9f, 0.5f, 1f)),
        new ForageDef("corn", new Color(1f, 0.9f, 0.2f, 1f)),
        new ForageDef("basil", new Color(0.15f, 0.55f, 0.2f, 1f)),
        // สวนผลไม้
        new ForageDef("mango", new Color(1f, 0.7f, 0.1f, 1f)),
        new ForageDef("coconut", new Color(0.5f, 0.35f, 0.2f, 1f)),
        new ForageDef("lemon", new Color(1f, 1f, 0.4f, 1f)),
        new ForageDef("lime", new Color(0.55f, 0.85f, 0.15f, 1f)),
        new ForageDef("green_papaya", new Color(0.35f, 0.7f, 0.45f, 1f)),
        new ForageDef("seasonal_fruit", new Color(0.85f, 0.4f, 0.8f, 1f)),
        // ป่าลึก
        new ForageDef("egg", new Color(1f, 0.97f, 0.88f, 1f)),
    };

    // ---------- ข้อมูลโซน ----------
    private struct ZoneDef
    {
        public string name;
        public int minX, maxX, minY, maxY;   // ขอบเขตเป็นช่อง (รวมขอบ)
        public string[] items;
        public float[] weights;
        public int maxItems, initialItems, maxNewPerDay;
        public float growChance;
        public Color gizmo;
    }

    private static readonly ZoneDef[] ZoneDefs =
    {
        new ZoneDef
        {
            name = FieldZoneName, minX = -14, maxX = -3, minY = -38, maxY = -31,
            items = new[] { "chili", "tomato", "peanut", "cabbage", "corn", "basil" },
            weights = new[] { 2f, 1.5f, 1.5f, 1f, 1f, 1f },
            maxItems = 12, initialItems = 8, maxNewPerDay = 7, growChance = 0.8f,
            gizmo = new Color(0.5f, 1f, 0.3f, 1f)
        },
        new ZoneDef
        {
            name = OrchardZoneName, minX = 2, maxX = 13, minY = -38, maxY = -31,
            items = new[] { "mango", "coconut", "lemon", "lime", "green_papaya", "seasonal_fruit" },
            weights = new[] { 1f, 1f, 1f, 1f, 1f, 1f },
            maxItems = 12, initialItems = 8, maxNewPerDay = 7, growChance = 0.8f,
            gizmo = new Color(1f, 0.75f, 0.2f, 1f)
        },
        new ZoneDef
        {
            // ไข่: ทุกเช้าโอกาส 40% งอก 1 ฟอง มีพร้อมกันไม่เกิน 2 (≈ 1 ฟองต่อ 2–3 วัน)
            name = EggZoneName, minX = 9, maxX = 13, minY = -48, maxY = -45,
            items = new[] { "egg" },
            weights = new[] { 1f },
            maxItems = 2, initialItems = 1, maxNewPerDay = 1, growChance = 0.4f,
            gizmo = new Color(1f, 1f, 1f, 1f)
        },
    };

    // โซนเห็ดเดิมย้ายมาป่าลึก (ขอบเขตเป็นช่อง)
    private const int MushroomMinX = -14, MushroomMaxX = 5, MushroomMinY = -48, MushroomMaxY = -42;

    [MenuItem("Tools/Second Serving/Build Forest Test Area")]
    public static void Build()
    {
        List<string> report = new List<string>();

        Undo.SetCurrentGroupName("Build Forest Test Area");
        int undoGroup = Undo.GetCurrentGroup();

        Dictionary<string, ForageItem> prefabs = EnsurePrefabs(report);
        Tilemap ground = PaintMap(report);
        EnsureZones(prefabs, ground, report);
        RefreshConfiners(report);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("Build Forest Test Area:\n- " + string.Join("\n- ", report));
    }

    // ---------- 1. Prefab ของป่า ----------

    private static Dictionary<string, ForageItem> EnsurePrefabs(List<string> report)
    {
        Dictionary<string, ForageItem> result = new Dictionary<string, ForageItem>();

        if (!AssetDatabase.IsValidFolder(ForageFolder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Forage");
        }

        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabPath);
        if (template == null || template.GetComponent<ForageItem>() == null)
        {
            report.Add($"WARNING: {TemplatePrefabPath} not found or has no ForageItem. New forage prefabs were not created.");
        }

        Dictionary<string, ItemData> items = LoadItemsById();
        int created = 0, kept = 0;

        foreach (ForageDef def in ForageDefs)
        {
            string path = $"{ForageFolder}/Pickup_{def.itemId}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            // มีอยู่แล้ว = kept (ไม่ทับสี/ภาพที่ผู้ใช้อาจแก้ไว้)
            if (existing != null)
            {
                ForageItem existingItem = existing.GetComponent<ForageItem>();
                if (existingItem != null) result[def.itemId] = existingItem;
                kept++;
                continue;
            }

            if (template == null) continue;

            if (!items.TryGetValue(def.itemId, out ItemData data))
            {
                report.Add($"WARNING: ItemData '{def.itemId}' not found. Run Import Recipes & Ingredients first. Prefab skipped.");
                continue;
            }

            // โคลนแบบไม่ผูกกับ prefab ต้นแบบ แล้วบันทึกเป็น prefab ใหม่
            GameObject root = Object.Instantiate(template);
            root.name = $"Pickup_{def.itemId}";

            SpriteRenderer sr = root.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = def.color;

            ForageItem forage = root.GetComponent<ForageItem>();
            SerializedObject so = new SerializedObject(forage);
            so.FindProperty("itemData").objectReferenceValue = data;
            so.FindProperty("amount").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            if (saved != null)
            {
                result[def.itemId] = saved.GetComponent<ForageItem>();
                created++;
            }
            else
            {
                report.Add($"WARNING: Could not save {path}.");
            }
        }

        // เห็ดใช้ prefab เดิม
        GameObject mushroom = AssetDatabase.LoadAssetAtPath<GameObject>(MushroomPrefabPath);
        if (mushroom != null && mushroom.GetComponent<ForageItem>() != null)
        {
            result["mushroom"] = mushroom.GetComponent<ForageItem>();
        }

        report.Add($"Forage prefabs: {created} created, {kept} kept ({ForageFolder}).");
        return result;
    }

    private static Dictionary<string, ItemData> LoadItemsById()
    {
        Dictionary<string, ItemData> map = new Dictionary<string, ItemData>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null || string.IsNullOrWhiteSpace(item.itemId)) continue;
            if (!map.ContainsKey(item.itemId)) map.Add(item.itemId, item);
        }
        return map;
    }

    // ---------- 2. ทาสีแผนที่ ----------

    private static Tilemap PaintMap(List<string> report)
    {
        Tilemap ground = FindTilemap(GroundName);
        Tilemap obstacles = FindTilemap(ObstaclesName);
        Tilemap bounds = FindTilemap(CameraBoundsName);

        TileBase grass = AssetDatabase.LoadAssetAtPath<TileBase>(GrassTilePath);
        TileBase dirt = AssetDatabase.LoadAssetAtPath<TileBase>(DirtTilePath);
        TileBase wall = AssetDatabase.LoadAssetAtPath<TileBase>(WallTilePath);
        TileBase bound = AssetDatabase.LoadAssetAtPath<TileBase>(BoundTilePath);

        if (ground == null) report.Add($"WARNING: Tilemap '{GroundName}' not found. Ground not painted.");
        if (obstacles == null) report.Add($"WARNING: Tilemap '{ObstaclesName}' not found. Walls not painted.");
        if (bounds == null) report.Add($"WARNING: Tilemap '{CameraBoundsName}' not found. Camera bounds not painted.");
        if (grass == null) report.Add($"WARNING: {GrassTilePath} not found.");
        if (dirt == null) report.Add($"WARNING: {DirtTilePath} not found.");
        if (wall == null) report.Add($"WARNING: {WallTilePath} not found.");
        if (bound == null) report.Add($"WARNING: {BoundTilePath} not found.");

        if (ground != null) Undo.RegisterCompleteObjectUndo(ground, "Paint Forest Ground");
        if (obstacles != null) Undo.RegisterCompleteObjectUndo(obstacles, "Paint Forest Walls");
        if (bounds != null) Undo.RegisterCompleteObjectUndo(bounds, "Paint Forest Camera Bounds");

        // พื้นหญ้า: ทุ่งหญ้า (รวมใต้กำแพงข้าง) + ป่า (รวมใต้กำแพงรอบ)
        if (ground != null && grass != null)
        {
            FillRect(ground, grass, -MeadowWallX, MeadowWallX, MeadowBottomY, MeadowTopY);
            FillRect(ground, grass, ForestMinX - 1, ForestMaxX + 1, ForestBottomY - 1, ForestTopY + 1);
        }

        // ทางดิน (ทาทับหญ้า)
        if (ground != null && dirt != null)
        {
            FillRect(ground, dirt, PathMinX, PathMaxX, PathBottomY, PathTopY);
        }

        if (obstacles != null)
        {
            // เจาะกำแพงล่างของลาน
            FillRect(obstacles, null, GapMinX, GapMaxX, YardWallY, YardWallY);
            // ทุ่งหญ้าและป่าต้องโล่ง (ทาซ้ำได้ผลเท่าเดิม)
            FillRect(obstacles, null, MeadowMinX, MeadowMaxX, MeadowBottomY, YardWallY - 1);
            FillRect(obstacles, null, ForestMinX, ForestMaxX, ForestBottomY, ForestTopY);

            if (wall != null)
            {
                // กำแพงข้างทุ่งหญ้า
                FillRect(obstacles, wall, -MeadowWallX, -MeadowWallX, MeadowBottomY + 1, YardWallY - 1);
                FillRect(obstacles, wall, MeadowWallX, MeadowWallX, MeadowBottomY + 1, YardWallY - 1);
                // แถวบนของป่า (เว้นช่วงที่ต่อกับทุ่งหญ้า)
                FillRect(obstacles, wall, ForestMinX - 1, -MeadowWallX, MeadowBottomY, MeadowBottomY);
                FillRect(obstacles, wall, MeadowWallX, ForestMaxX + 1, MeadowBottomY, MeadowBottomY);
                // ข้างซ้าย ข้างขวา และแถวล่างของป่า
                FillRect(obstacles, wall, ForestMinX - 1, ForestMinX - 1, ForestBottomY - 1, ForestTopY);
                FillRect(obstacles, wall, ForestMaxX + 1, ForestMaxX + 1, ForestBottomY - 1, ForestTopY);
                FillRect(obstacles, wall, ForestMinX - 1, ForestMaxX + 1, ForestBottomY - 1, ForestBottomY - 1);
            }
        }

        // ขอบกล้อง: ต่อจากลานเดิม (รวมแถวกำแพงล่าง) ลงมาทุ่งหญ้าและป่าเป็นก้อนเดียว
        if (bounds != null && bound != null)
        {
            FillRect(bounds, bound, MeadowMinX, MeadowMaxX, MeadowBottomY, MeadowTopY);
            FillRect(bounds, bound, ForestMinX, ForestMaxX, ForestBottomY, ForestTopY);
        }

        if (ground != null) EditorUtility.SetDirty(ground);
        if (obstacles != null) EditorUtility.SetDirty(obstacles);
        if (bounds != null) EditorUtility.SetDirty(bounds);

        report.Add($"Map painted: meadow x {MeadowMinX}..{MeadowMaxX}, y {MeadowTopY}..{MeadowBottomY} / forest x {ForestMinX}..{ForestMaxX}, y {ForestTopY}..{ForestBottomY} (cells).");
        return ground;
    }

    // ทาสี่เหลี่ยมช่อง (รวมขอบ) tile = null คือลบ
    private static void FillRect(Tilemap map, TileBase tile, int minX, int maxX, int minY, int maxY)
    {
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                map.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }
    }

    private static Tilemap FindTilemap(string name)
    {
        foreach (Tilemap map in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (map.gameObject.name == name) return map;
        }
        return null;
    }

    // ---------- 3. โซนงอก ----------

    private static void EnsureZones(Dictionary<string, ForageItem> prefabs, Tilemap ground, List<string> report)
    {
        ForageSpawner spawner = Object.FindFirstObjectByType<ForageSpawner>(FindObjectsInactive.Include);
        if (spawner == null)
        {
            report.Add("WARNING: ForageSpawner not found in scene. Zones not created.");
            return;
        }

        foreach (ZoneDef def in ZoneDefs)
        {
            if (FindChild(spawner.transform, def.name) != null)
            {
                report.Add($"Zone '{def.name}' kept.");
                continue;
            }

            GameObject go = new GameObject(def.name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {def.name}");
            Undo.SetTransformParent(go.transform, spawner.transform, $"Parent {def.name}");
            go.transform.position = RectCenter(ground, def.minX, def.maxX, def.minY, def.maxY);

            ForageSpawnZone zone = Undo.AddComponent<ForageSpawnZone>(go);
            SerializedObject so = new SerializedObject(zone);
            so.FindProperty("zoneId").stringValue = def.name;
            so.FindProperty("size").vector2Value = new Vector2(def.maxX - def.minX + 1, def.maxY - def.minY + 1);
            so.FindProperty("maxItems").intValue = def.maxItems;
            so.FindProperty("initialItems").intValue = def.initialItems;
            so.FindProperty("maxNewPerDay").intValue = def.maxNewPerDay;
            so.FindProperty("growChance").floatValue = def.growChance;
            so.FindProperty("gizmoColor").colorValue = def.gizmo;

            SerializedProperty entries = so.FindProperty("entries");
            entries.ClearArray();
            int added = 0;
            for (int i = 0; i < def.items.Length; i++)
            {
                if (!prefabs.TryGetValue(def.items[i], out ForageItem prefab) || prefab == null)
                {
                    report.Add($"WARNING: No prefab for '{def.items[i]}'. Not added to {def.name}.");
                    continue;
                }

                entries.InsertArrayElementAtIndex(added);
                SerializedProperty entry = entries.GetArrayElementAtIndex(added);
                entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                entry.FindPropertyRelative("weight").floatValue = def.weights[i];
                added++;
            }

            so.ApplyModifiedProperties();
            report.Add($"Zone '{def.name}' created ({added} items).");
        }

        // โซนเห็ดเดิม: ย้ายลงป่าลึกเฉพาะตอนยังอยู่ในลานเดิม (ถ้าผู้ใช้ย้ายเองแล้ว = ไม่แตะ)
        Transform mushroom = FindChild(spawner.transform, MushroomZoneName);
        if (mushroom != null)
        {
            if (mushroom.position.y > MeadowBottomY)
            {
                Undo.RecordObject(mushroom, "Move Mushroom Zone");
                mushroom.position = RectCenter(ground, MushroomMinX, MushroomMaxX, MushroomMinY, MushroomMaxY);

                ForageSpawnZone zone = mushroom.GetComponent<ForageSpawnZone>();
                if (zone != null)
                {
                    SerializedObject so = new SerializedObject(zone);
                    so.FindProperty("size").vector2Value = new Vector2(MushroomMaxX - MushroomMinX + 1, MushroomMaxY - MushroomMinY + 1);
                    so.ApplyModifiedProperties();
                }
                report.Add($"Zone '{MushroomZoneName}' moved to the deep forest.");
            }
            else
            {
                report.Add($"Zone '{MushroomZoneName}' already outside the yard, kept.");
            }
        }

        // โซนข้าวโพดเดิม: ปิดไว้ (ข้าวโพดย้ายไปอยู่ทุ่งผักแล้ว)
        Transform corn = FindChild(spawner.transform, CornZoneName);
        if (corn != null && corn.gameObject.activeSelf)
        {
            Undo.RecordObject(corn.gameObject, "Disable Corn Zone");
            corn.gameObject.SetActive(false);
            report.Add($"Zone '{CornZoneName}' disabled (you can delete it).");
        }
    }

    // หาลูก (รวมที่ปิดอยู่) ตามชื่อ ทุกระดับ
    private static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t != parent && t.name == name) return t;
        }
        return null;
    }

    // จุดกึ่งกลางของสี่เหลี่ยมช่อง (minX..maxX, minY..maxY) ในพิกัดโลก
    private static Vector3 RectCenter(Tilemap ground, int minX, int maxX, int minY, int maxY)
    {
        if (ground != null)
        {
            Vector3 a = ground.GetCellCenterWorld(new Vector3Int(minX, minY, 0));
            Vector3 b = ground.GetCellCenterWorld(new Vector3Int(maxX, maxY, 0));
            Vector3 c = (a + b) * 0.5f;
            c.z = 0f;
            return c;
        }
        // ไม่มี Tilemap พื้น = ถือว่า 1 ช่อง = 1 หน่วย เริ่มที่จุด 0
        return new Vector3((minX + maxX + 1) * 0.5f, (minY + maxY + 1) * 0.5f, 0f);
    }

    // ---------- 4. แจ้ง Confiner ให้คำนวณขอบกล้องใหม่ ----------

    // ใช้ reflection เพื่อไม่ผูกกับชื่อเมธอดของ Cinemachine เวอร์ชันใดเวอร์ชันหนึ่ง
    private static void RefreshConfiners(List<string> report)
    {
        int refreshed = 0;
        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null || mb.GetType().Name != "CinemachineConfiner2D") continue;

            MethodInfo method = mb.GetType().GetMethod("InvalidateBoundingShapeCache", BindingFlags.Public | BindingFlags.Instance)
                                ?? mb.GetType().GetMethod("InvalidateCache", BindingFlags.Public | BindingFlags.Instance);
            if (method != null && method.GetParameters().Length == 0)
            {
                method.Invoke(mb, null);
                refreshed++;
            }
        }
        report.Add(refreshed > 0 ? $"Camera confiner refreshed ({refreshed})." : "Camera confiner not refreshed (it rebuilds on Play).");
    }
}
