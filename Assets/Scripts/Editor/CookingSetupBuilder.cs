using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// [2.5] เครื่องมือตั้งค่าระบบทำอาหารอัตโนมัติ (ใช้ใน Editor เท่านั้น)
// เมนู: Tools > Second Serving > Build Cooking System
// - สร้าง Assets/Cooking/CookingSettings.asset + ชุดท่าตัวอย่างใน Assets/Cooking/Profiles (มีอยู่แล้วจะไม่เขียนทับ)
// - ตั้งขั้นตอนให้สูตรเริ่มต้น 8 สูตร + จานผัด 5 สูตร (เฉพาะสูตรที่ยังไม่ได้ตั้ง Cooking Profile / Custom Steps)
// - สร้าง prefab เตา (Assets/Prefabs/Stove.prefab) และวางในฉาก 1 เตาข้างเคาน์เตอร์ ถ้ายังไม่มีเตาในฉาก
// - สร้างวัตถุ CookingSystem (CookingSession) / ใส่ CarriedDish ให้ Player
// - สร้างแผงมินิเกม (CookingMinigamePanel) และหน้าต่างทำอาหารนอกเวลาร้าน (CookingMenuUI) บน Canvas หลัก
// - กดซ้ำได้ (จัดตำแหน่ง UI ใหม่ ผูกช่องใหม่) กด Ctrl+Z ย้อนการเปลี่ยนแปลงในฉากได้ (ไม่รวมไฟล์ asset/prefab)
// เมนู: Tools > Second Serving > Assign Sample Cooking Profiles = ผูกชุดท่าให้สูตรเริ่มต้น + จานผัดอย่างเดียว (ไม่แตะฉาก)
// เมนู: Tools > Second Serving > Reset Cooking Tuning
// - เขียนทับค่าความยากใน CookingSettings (รวม Hold / Stir / Whisk / Shake / ตัวคูณ ★) + ขั้นตอนของชุดท่าตัวอย่าง 9 ไฟล์ ด้วยค่าล่าสุดในโค้ด (ภาพ/ชื่อท่าตั้งต้น/ข้อความดาว/Profile ตาม Course คงเดิม)
// - ไม่แตะ Custom Steps ในสูตร และชุดท่าที่สร้างเองชื่ออื่น
public static class CookingSetupBuilder
{
    private const string CookingFolder = "Assets/Cooking";
    private const string ProfileFolder = "Assets/Cooking/Profiles";
    private const string SettingsPath = "Assets/Cooking/CookingSettings.asset";
    private const string PlaceholderFolder = "Assets/Prefabs/Placeholder";
    private const string SquarePath = "Assets/Prefabs/Placeholder/WhiteSquare.png";
    private const string StovePath = "Assets/Prefabs/Stove.prefab";

    private static readonly Color StoveColor = new Color(0.35f, 0.33f, 0.36f, 1f);
    private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.1f, 0.88f);
    private static readonly Color WindowColor = new Color(0.08f, 0.08f, 0.1f, 0.92f);
    private static readonly Color ListColor = new Color(0f, 0f, 0f, 0.25f);
    private static readonly Color ButtonColor = new Color(0.25f, 0.25f, 0.3f, 1f);

    // ---------- เมนู ----------

    [MenuItem("Tools/Second Serving/Build Cooking System")]
    public static void Build()
    {
        List<string> report = new List<string>();

        // ---------- asset ----------
        EnsureFolder("Assets", "Cooking");
        EnsureFolder(CookingFolder, "Profiles");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "Placeholder");

        CookingSettings settings = GetOrCreateSettings(report);
        Dictionary<string, CookingProfile> profiles = CreateProfiles(report);
        AssignCourseDefaults(settings, profiles, report);
        AssignStarterRecipes(profiles, report);
        AssetDatabase.SaveAssets();

        Sprite square = GetOrCreateSquareSprite();
        Stove stovePrefab = CreateStovePrefab(square, report);

        // ---------- ในฉาก ----------
        Undo.SetCurrentGroupName("Build Cooking System");
        int undoGroup = Undo.GetCurrentGroup();

        PlayerInteractor player = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        if (player == null)
        {
            report.Add("Player (PlayerInteractor) not found: CarriedDish was not added.");
        }
        else if (player.GetComponent<CarriedDish>() == null)
        {
            Undo.AddComponent<CarriedDish>(player.gameObject);
            report.Add("Added CarriedDish to the Player.");
        }

        PlaceStove(stovePrefab, report);

        Canvas canvas = FindMainCanvas();
        CookingMinigameView view = null;
        if (canvas == null)
        {
            report.Add("No Canvas found: UI was not built.");
        }
        else
        {
            view = BuildMinigamePanel(canvas);
            BuildCookingWindow(canvas, player);
            report.Add($"Built CookingMinigamePanel and CookingMenuUI on '{canvas.name}'.");
        }

        BuildCookingSystem(settings, view, player);
        report.Add("CookingSystem (CookingSession) is ready.");

        Undo.CollapseUndoOperations(undoGroup);
        if (canvas != null) EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        else EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        string summary = string.Join("\n", report);
        Debug.Log($"CookingSetupBuilder: {summary.Replace('\n', ' ')}");
        EditorUtility.DisplayDialog("Cooking System", summary + "\n\nSave the scene with Ctrl+S.", "OK");
    }

    // ---------- ผูกชุดท่าให้สูตรตัวอย่าง ----------

    // ผูกชุดท่าให้สูตรเริ่มต้น + จานผัด อย่างเดียว (ไม่แตะ UI ในฉาก) สูตรที่ตั้งไว้แล้วไม่ถูกเขียนทับ
    [MenuItem("Tools/Second Serving/Assign Sample Cooking Profiles")]
    public static void AssignSampleProfiles()
    {
        List<string> report = new List<string>();
        EnsureFolder("Assets", "Cooking");
        EnsureFolder(CookingFolder, "Profiles");

        Dictionary<string, CookingProfile> profiles = CreateProfiles(report);
        AssignStarterRecipes(profiles, report);
        AssetDatabase.SaveAssets();

        string summary = string.Join("\n", report);
        Debug.Log($"CookingSetupBuilder: {summary.Replace('\n', ' ')}");
        EditorUtility.DisplayDialog("Assign Sample Cooking Profiles", summary, "OK");
    }

    // ---------- รีเซ็ตค่าความยาก ----------

    [MenuItem("Tools/Second Serving/Reset Cooking Tuning")]
    public static void ResetTuning()
    {
        bool ok = EditorUtility.DisplayDialog("Reset Cooking Tuning",
            "Overwrite the difficulty values in CookingSettings and the steps of the 9 sample cooking profiles " +
            "(Default_Main, Default_Dessert, Default_Drink, StirFry, Pound, Toast, Steam, SweetPlate, Juice) with the latest defaults?\n\n" +
            "Visuals already assigned to steps are kept. Custom Steps on recipes and your own profiles are not touched.",
            "Reset", "Cancel");
        if (!ok)
        {
            return;
        }

        List<string> report = new List<string>();
        EnsureFolder("Assets", "Cooking");
        EnsureFolder(CookingFolder, "Profiles");

        CookingSettings settings = GetOrCreateSettings(report);
        ResetSettingsValues(settings);
        report.Add("Reset difficulty values in CookingSettings.");

        Dictionary<string, CookingProfile> profiles = CreateProfiles(report, true);
        AssignCourseDefaults(settings, profiles, report);
        AssetDatabase.SaveAssets();

        string summary = string.Join("\n", report);
        Debug.Log($"CookingSetupBuilder: {summary.Replace('\n', ' ')}");
        EditorUtility.DisplayDialog("Reset Cooking Tuning", summary, "OK");
    }

    // คัดลอกค่าความยาก/เวลา/เกณฑ์คุณภาพจากค่าตั้งต้นในโค้ด (ไม่แตะ Profile ตาม Course / ภาพ / ชื่อท่า / ตัวอักษรปุ่ม)
    private static void ResetSettingsValues(CookingSettings settings)
    {
        CookingSettings defaults = ScriptableObject.CreateInstance<CookingSettings>();
        try
        {
            Undo.RecordObject(settings, "Reset Cooking Tuning");
            settings.timingZoneWidth = defaults.timingZoneWidth;
            settings.timingPerfectWidth = defaults.timingPerfectWidth;
            settings.timingNeedleSpeed = defaults.timingNeedleSpeed;
            settings.timingHits = defaults.timingHits;
            settings.timingZoneMoveSpeed = defaults.timingZoneMoveSpeed;
            settings.timingTimeLimit = defaults.timingTimeLimit;
            settings.mashPresses = defaults.mashPresses;
            settings.mashTimeLimit = defaults.mashTimeLimit;
            settings.mashPerfectTimeRatio = defaults.mashPerfectTimeRatio;
            settings.mashDecayPerSecond = defaults.mashDecayPerSecond;
            settings.mashDecayDelay = defaults.mashDecayDelay;
            settings.sequenceLength = defaults.sequenceLength;
            settings.sequenceTimePerKey = defaults.sequenceTimePerKey;
            settings.sequenceMistakesForGood = defaults.sequenceMistakesForGood;
            settings.holdFillSeconds = defaults.holdFillSeconds;
            settings.holdZoneWidth = defaults.holdZoneWidth;
            settings.holdPerfectWidth = defaults.holdPerfectWidth;
            settings.holdTargetMin = defaults.holdTargetMin;
            settings.holdTargetMax = defaults.holdTargetMax;
            settings.holdTimeLimit = defaults.holdTimeLimit;
            settings.stirPanWidth = defaults.stirPanWidth;
            settings.stirFoodSpeed = defaults.stirFoodSpeed;
            settings.stirFoodRetargetMin = defaults.stirFoodRetargetMin;
            settings.stirFoodRetargetMax = defaults.stirFoodRetargetMax;
            settings.stirPanAcceleration = defaults.stirPanAcceleration;
            settings.stirPanMaxSpeed = defaults.stirPanMaxSpeed;
            settings.stirPanFriction = defaults.stirPanFriction;
            settings.stirFillPerSecond = defaults.stirFillPerSecond;
            settings.stirDrainPerSecond = defaults.stirDrainPerSecond;
            settings.stirStartProgress = defaults.stirStartProgress;
            settings.stirTimeLimit = defaults.stirTimeLimit;
            settings.stirPerfectTimeRatio = defaults.stirPerfectTimeRatio;
            settings.whiskPresses = defaults.whiskPresses;
            settings.whiskTimeLimit = defaults.whiskTimeLimit;
            settings.whiskDecayPerSecond = defaults.whiskDecayPerSecond;
            settings.whiskDecayDelay = defaults.whiskDecayDelay;
            settings.whiskPerfectTimeRatio = defaults.whiskPerfectTimeRatio;
            settings.shakePresses = defaults.shakePresses;
            settings.shakeTimeLimit = defaults.shakeTimeLimit;
            settings.shakeDecayPerSecond = defaults.shakeDecayPerSecond;
            settings.shakeDecayDelay = defaults.shakeDecayDelay;
            settings.shakePerfectTimeRatio = defaults.shakePerfectTimeRatio;
            settings.difficultySpeed = (float[])defaults.difficultySpeed.Clone();
            settings.difficultyZone = (float[])defaults.difficultyZone.Clone();
            settings.difficultyPresses = (float[])defaults.difficultyPresses.Clone();
            settings.difficultyTime = (float[])defaults.difficultyTime.Clone();
            settings.difficultyExtraKeys = (int[])defaults.difficultyExtraKeys.Clone();
            settings.difficultySatisfaction = (float[])defaults.difficultySatisfaction.Clone();
            settings.perfectAverage = defaults.perfectAverage;
            settings.goodAverage = defaults.goodAverage;
            settings.resultFlashDuration = defaults.resultFlashDuration;
            settings.stepIntroDelay = defaults.stepIntroDelay;
            EditorUtility.SetDirty(settings);
        }
        finally
        {
            Object.DestroyImmediate(defaults);
        }
    }

    // ---------- Settings / Profiles ----------

    private static CookingSettings GetOrCreateSettings(List<string> report)
    {
        CookingSettings settings = AssetDatabase.LoadAssetAtPath<CookingSettings>(SettingsPath);
        if (settings != null)
        {
            report.Add("CookingSettings already exists (kept).");
            return settings;
        }

        settings = ScriptableObject.CreateInstance<CookingSettings>();
        AssetDatabase.CreateAsset(settings, SettingsPath);
        report.Add($"Created {SettingsPath}.");
        return settings;
    }

    // สร้างชุดท่าตัวอย่าง (ชื่อไฟล์ → ขั้นตอน) overwrite = false: มีอยู่แล้วไม่เขียนทับ / true: เขียนทับขั้นตอน (เก็บภาพเดิมไว้)
    private static Dictionary<string, CookingProfile> CreateProfiles(List<string> report, bool overwrite = false)
    {
        Dictionary<string, CookingProfile> result = new Dictionary<string, CookingProfile>();
        int created = 0;
        updatedCount = 0;
        overwriteProfiles = overwrite;

        // ค่าตั้งต้นตามประเภท
        created += AddProfile(result, "Default_Main", "Default Main",
            CookingStep.Create(CookingMinigameType.Mash, "Chop!"),
            CookingStep.Create(CookingMinigameType.Timing, "Cook!"),
            CookingStep.Create(CookingMinigameType.Sequence, "Season!"));

        created += AddProfile(result, "Default_Dessert", "Default Dessert",
            CookingStep.Create(CookingMinigameType.Whisk, "Mix!"),
            CookingStep.Create(CookingMinigameType.Timing, "Bake!"),
            CookingStep.Create(CookingMinigameType.Sequence, "Decorate!"));

        created += AddProfile(result, "Default_Drink", "Default Drink",
            CookingStep.Create(CookingMinigameType.Hold, "Pour!"),
            CookingStep.Create(CookingMinigameType.Shake, "Shake!"));

        // ชุดท่าใช้ร่วมหลายสูตร
        created += AddProfile(result, "StirFry", "Stir-fry (keep the food in the pan)",
            CookingStep.Create(CookingMinigameType.Stir, "Stir-fry!"),
            CookingStep.Create(CookingMinigameType.Sequence, "Plate!"));

        CookingStep pound = CookingStep.Create(CookingMinigameType.Mash, "Pound!");
        pound.presses = 10;
        pound.timeLimit = 4.5f;
        CookingStep season = CookingStep.Create(CookingMinigameType.Sequence, "Season!");
        season.length = 5;
        created += AddProfile(result, "Pound", "Pound in a mortar",
            pound,
            season);

        CookingStep toast = CookingStep.Create(CookingMinigameType.Timing, "Toast!");
        toast.zoneWidth = 0.32f;
        toast.perfectWidth = 0.1f;
        toast.needleSpeed = 0.45f;
        created += AddProfile(result, "Toast", "Chop and toast (easy)",
            CookingStep.Create(CookingMinigameType.Mash, "Chop!"),
            toast);

        CookingStep knead = CookingStep.Create(CookingMinigameType.Mash, "Knead!");
        knead.presses = 8;
        CookingStep wrap = CookingStep.Create(CookingMinigameType.Sequence, "Wrap!");
        wrap.length = 4;
        created += AddProfile(result, "Steam", "Knead, wrap and steam",
            knead,
            wrap,
            CookingStep.Create(CookingMinigameType.Timing, "Steam!"));

        CookingStep slice = CookingStep.Create(CookingMinigameType.Mash, "Slice!");
        slice.presses = 6;
        created += AddProfile(result, "SweetPlate", "Steam, slice and plate",
            CookingStep.Create(CookingMinigameType.Timing, "Steam rice!"),
            slice,
            CookingStep.Create(CookingMinigameType.Sequence, "Plate!"));

        CookingStep squeeze = CookingStep.Create(CookingMinigameType.Mash, "Squeeze!");
        squeeze.presses = 8;
        created += AddProfile(result, "Juice", "Squeeze and pour",
            squeeze,
            CookingStep.Create(CookingMinigameType.Hold, "Pour!"));

        if (created > 0) report.Add($"Created {created} cooking profile(s) in {ProfileFolder}.");
        if (updatedCount > 0) report.Add($"Reset steps of {updatedCount} sample profile(s) (visuals kept).");
        if (created == 0 && updatedCount == 0) report.Add("Cooking profiles already exist (kept).");
        overwriteProfiles = false;
        return result;
    }

    private static bool overwriteProfiles;
    private static int updatedCount;

    private static int AddProfile(Dictionary<string, CookingProfile> result, string fileName, string displayName, params CookingStep[] steps)
    {
        string path = $"{ProfileFolder}/{fileName}.asset";
        CookingProfile profile = AssetDatabase.LoadAssetAtPath<CookingProfile>(path);
        int created = 0;

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<CookingProfile>();
            profile.displayName = displayName;
            profile.steps = new List<CookingStep>(steps);
            AssetDatabase.CreateAsset(profile, path);
            created = 1;
        }
        else if (overwriteProfiles)
        {
            // เขียนทับขั้นตอน: ขั้นลำดับเดียวกันชนิดเดียวกันที่มีภาพอยู่แล้ว เก็บภาพไว้
            List<CookingStep> fresh = new List<CookingStep>(steps);
            for (int i = 0; i < fresh.Count; i++)
            {
                if (profile.steps != null && i < profile.steps.Count && profile.steps[i] != null &&
                    profile.steps[i].type == fresh[i].type && profile.steps[i].visual != null)
                {
                    fresh[i].visual = profile.steps[i].visual;
                }
            }

            Undo.RecordObject(profile, "Reset Cooking Profile");
            profile.displayName = displayName;
            profile.steps = fresh;
            EditorUtility.SetDirty(profile);
            updatedCount++;
        }

        result[fileName] = profile;
        return created;
    }

    private static void AssignCourseDefaults(CookingSettings settings, Dictionary<string, CookingProfile> profiles, List<string> report)
    {
        bool changed = false;
        if (settings.mainProfile == null) { settings.mainProfile = profiles["Default_Main"]; changed = true; }
        if (settings.dessertProfile == null) { settings.dessertProfile = profiles["Default_Dessert"]; changed = true; }
        if (settings.drinkProfile == null) { settings.drinkProfile = profiles["Default_Drink"]; changed = true; }

        if (changed)
        {
            EditorUtility.SetDirty(settings);
            report.Add("Set default steps by course in CookingSettings.");
        }
    }

    // สูตรเริ่มต้น 8 สูตร + จานผัด: ใส่ชุดท่า (หรือขั้นตอนเฉพาะสูตร) เฉพาะสูตรที่ยังไม่ได้ตั้ง
    private static void AssignStarterRecipes(Dictionary<string, CookingProfile> profiles, List<string> report)
    {
        Dictionary<string, string> map = new Dictionary<string, string>
        {
            { "th_som_tam", "Pound" },
            { "cn_kung_pao_chicken", "StirFry" },
            // [2.5b] จานผัดอื่นๆ ใช้ท่าผัด (คุมกระทะ) ด้วย
            { "th_kaprao_rice", "StirFry" },
            { "th_pad_thai", "StirFry" },
            { "cn_yangzhou_fried_rice", "StirFry" },
            { "cn_sweet_sour_pork", "StirFry" },
            { "cn_dan_dan_noodles", "StirFry" },
            { "it_bruschetta", "Toast" },
            { "mx_tamales", "Steam" },
            { "th_mango_sticky_rice", "SweetPlate" },
            { "us_lemonade", "Juice" },
            { "mx_agua_fresca", "Juice" },
        };

        int assigned = 0;
        int kept = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:RecipeData"))
        {
            RecipeData recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(AssetDatabase.GUIDToAssetPath(guid));
            if (recipe == null || string.IsNullOrEmpty(recipe.recipeId))
            {
                continue;
            }

            bool hasSetup = recipe.cookingProfile != null || (recipe.customSteps != null && recipe.customSteps.Count > 0);

            if (map.TryGetValue(recipe.recipeId, out string profileName))
            {
                map.Remove(recipe.recipeId);
                if (hasSetup) { kept++; continue; }

                Undo.RecordObject(recipe, "Assign Cooking Profile");
                recipe.cookingProfile = profiles[profileName];
                EditorUtility.SetDirty(recipe);
                assigned++;
            }
            else if (recipe.recipeId == "jp_okonomiyaki")
            {
                // ตัวอย่างขั้นตอนเฉพาะสูตร: พลิกบนกระทะที่โซนเลื่อนได้
                if (hasSetup) { kept++; continue; }

                CookingStep mix = CookingStep.Create(CookingMinigameType.Mash, "Mix batter!");
                CookingStep flip = CookingStep.Create(CookingMinigameType.Timing, "Flip!");
                flip.movingZone = true;
                flip.zoneWidth = 0.26f;
                CookingStep sauce = CookingStep.Create(CookingMinigameType.Sequence, "Sauce!");
                sauce.length = 3;

                Undo.RecordObject(recipe, "Assign Custom Steps");
                recipe.customSteps = new List<CookingStep> { mix, flip, sauce };
                EditorUtility.SetDirty(recipe);
                assigned++;
            }
        }

        int missing = map.Count;
        report.Add($"Sample recipes: {assigned} set up, {kept} already set (kept)" + (missing > 0 ? $", {missing} not found ({string.Join(", ", map.Keys)})." : "."));
    }

    // ---------- เตา ----------

    private static Stove CreateStovePrefab(Sprite square, List<string> report)
    {
        Stove existing = AssetDatabase.LoadAssetAtPath<Stove>(StovePath);
        if (existing != null)
        {
            report.Add("Stove prefab already exists (kept as is).");
            return existing;
        }

        GameObject root = new GameObject("Stove");
        try
        {
            GameObject body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(1f, 0.9f, 1f);
            SpriteRenderer sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = StoveColor;
            sr.sortingOrder = 2;

            // เตาเล็กๆ ด้านบน (ให้ดูออกว่าเป็นเตา)
            GameObject burner = new GameObject("Burner");
            burner.transform.SetParent(root.transform, false);
            burner.transform.localScale = new Vector3(0.45f, 0.35f, 1f);
            SpriteRenderer burnerSr = burner.AddComponent<SpriteRenderer>();
            burnerSr.sprite = square;
            burnerSr.color = new Color(0.95f, 0.45f, 0.2f, 1f);
            burnerSr.sortingOrder = 3;

            // Collider ทึบ: ลูกค้าเดินอ้อม ผู้เล่นกด E ได้ในระยะ
            BoxCollider2D col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = new Vector2(1f, 0.9f);

            GameObject bubble = new GameObject("Bubble");
            bubble.transform.SetParent(root.transform, false);
            bubble.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            TextMeshPro text = bubble.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.text = "";
            text.fontSize = 3f;
            text.alignment = TextAlignmentOptions.Bottom;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(6f, 1.2f);
            text.sortingOrder = 30;

            Stove stove = root.AddComponent<Stove>();
            SerializedObject so = new SerializedObject(stove);
            so.FindProperty("bubbleText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, StovePath);
            report.Add(saved != null ? $"Created {StovePath}." : "Failed to create the Stove prefab.");
            return saved != null ? saved.GetComponent<Stove>() : null;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void PlaceStove(Stove prefab, List<string> report)
    {
        int count = Object.FindObjectsByType<Stove>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        if (count > 0)
        {
            report.Add($"{count} stove(s) already in the scene.");
            return;
        }

        if (prefab == null)
        {
            report.Add("No Stove prefab: place one yourself.");
            return;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
        Undo.RegisterCreatedObjectUndo(go, "Place Stove");

        ShopCounter counter = Object.FindFirstObjectByType<ShopCounter>(FindObjectsInactive.Include);
        if (counter != null)
        {
            go.transform.position = counter.transform.position + new Vector3(1.5f, 0f, 0f);
            report.Add("Placed a Stove 1.5 units right of the ShopCounter: move it to your kitchen.");
        }
        else
        {
            report.Add("Placed a Stove at (0, 0): move it to your kitchen.");
        }

        Selection.activeGameObject = go;
    }

    // ---------- ระบบ ----------

    private static void BuildCookingSystem(CookingSettings settings, CookingMinigameView view, PlayerInteractor player)
    {
        CookingSession session = Object.FindFirstObjectByType<CookingSession>(FindObjectsInactive.Include);
        if (session == null)
        {
            GameObject go = new GameObject("CookingSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create CookingSystem");
            session = Undo.AddComponent<CookingSession>(go);
        }

        SerializedObject so = new SerializedObject(session);
        so.FindProperty("settings").objectReferenceValue = settings;
        if (view != null) so.FindProperty("view").objectReferenceValue = view;
        if (player != null)
        {
            so.FindProperty("inventory").objectReferenceValue = player.GetComponent<Inventory>();
            so.FindProperty("playerController").objectReferenceValue = player.GetComponent<PlayerController>();
        }
        so.ApplyModifiedProperties();
    }

    // ---------- แผงมินิเกม ----------

    private static CookingMinigameView BuildMinigamePanel(Canvas canvas)
    {
        GameObject panel = FindOrCreate(canvas.transform, "CookingMinigamePanel");
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 110f);
        rt.sizeDelta = new Vector2(680f, 250f);

        Image bg = GetOrAdd<Image>(panel);
        bg.color = PanelColor;
        bg.raycastTarget = false;
        GetOrAdd<CanvasGroup>(panel);

        TMP_Text header = FindOrCreateText(panel.transform, "HeaderText", 22, TextAlignmentOptions.Center);
        SetTopStretch(header.rectTransform, 20f, 20f, 10f, 28f);
        header.color = new Color(1f, 1f, 1f, 0.75f);
        header.raycastTarget = false;

        TMP_Text label = FindOrCreateText(panel.transform, "LabelText", 40, TextAlignmentOptions.Center);
        SetTopStretch(label.rectTransform, 20f, 20f, 40f, 52f);
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;

        GameObject visualGo = FindOrCreate(panel.transform, "Visual");
        SetTopLeft(visualGo.GetComponent<RectTransform>(), 12f, 96f, new Vector2(64f, 64f));
        Image visual = GetOrAdd<Image>(visualGo);
        visual.raycastTarget = false;
        visual.preserveAspect = true;
        visualGo.SetActive(false);

        // แถบหลัก
        GameObject barGo = FindOrCreate(panel.transform, "Bar");
        SetTopCenter(barGo.GetComponent<RectTransform>(), 104f, new Vector2(520f, 44f));
        Image barBg = GetOrAdd<Image>(barGo);
        barBg.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
        barBg.raycastTarget = false;

        RectTransform fill = BuildBarPart(barGo.transform, "Fill", new Color(1f, 0.6f, 0.25f, 0.9f));
        RectTransform zone = BuildBarPart(barGo.transform, "Zone", new Color(0.4f, 0.85f, 0.35f, 0.7f));
        RectTransform perfect = BuildBarPart(barGo.transform, "PerfectZone", new Color(1f, 0.82f, 0.3f, 0.9f));
        RectTransform needle = BuildBarPart(barGo.transform, "Needle", Color.white);
        fill.SetSiblingIndex(0);
        zone.SetSiblingIndex(1);
        perfect.SetSiblingIndex(2);
        needle.SetSiblingIndex(3);

        // [2.5b] เข็มมีขอบดำ มองเห็นได้ทั้งบนพื้นเข้มและบนโซนสี
        Outline needleOutline = GetOrAdd<Outline>(needle.gameObject);
        needleOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        needleOutline.effectDistance = new Vector2(2f, -2f);

        TMP_Text keys = FindOrCreateText(panel.transform, "KeysText", 44, TextAlignmentOptions.Center);
        SetTopCenter(keys.rectTransform, 96f, new Vector2(620f, 60f));
        keys.textWrappingMode = TextWrappingModes.NoWrap;
        keys.raycastTarget = false;
        keys.gameObject.SetActive(false);

        // [2.5b] แถบความคืบหน้าแยก (สีฟ้า ต่างจากสีกระทะ/โซน) ใช้กับ Stir
        GameObject progressGo = FindOrCreate(panel.transform, "ProgressBar");
        SetTopCenter(progressGo.GetComponent<RectTransform>(), 154f, new Vector2(520f, 14f));
        Image progressBg = GetOrAdd<Image>(progressGo);
        progressBg.color = new Color(1f, 1f, 1f, 0.15f);
        progressBg.raycastTarget = false;
        RectTransform progressFill = BuildBarPart(progressGo.transform, "ProgressFill", new Color(0.35f, 0.75f, 1f, 1f));
        progressGo.SetActive(false);

        // แถบเวลา
        GameObject timerGo = FindOrCreate(panel.transform, "TimerBar");
        SetTopCenter(timerGo.GetComponent<RectTransform>(), 174f, new Vector2(520f, 6f));
        Image timerBg = GetOrAdd<Image>(timerGo);
        timerBg.color = new Color(1f, 1f, 1f, 0.12f);
        timerBg.raycastTarget = false;
        RectTransform timerFill = BuildBarPart(timerGo.transform, "TimerFill", new Color(0.9f, 0.9f, 0.9f, 0.75f));

        TMP_Text hint = FindOrCreateText(panel.transform, "HintText", 20, TextAlignmentOptions.Center);
        SetTopStretch(hint.rectTransform, 20f, 20f, 184f, 30f);
        hint.color = new Color(1f, 1f, 1f, 0.7f);
        hint.raycastTarget = false;

        CookingMinigameView view = GetOrAdd<CookingMinigameView>(panel);
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("headerText").objectReferenceValue = header;
        so.FindProperty("labelText").objectReferenceValue = label;
        so.FindProperty("hintText").objectReferenceValue = hint;
        so.FindProperty("keysText").objectReferenceValue = keys;
        so.FindProperty("visualImage").objectReferenceValue = visual;
        so.FindProperty("barBackground").objectReferenceValue = barBg;
        so.FindProperty("zone").objectReferenceValue = zone;
        so.FindProperty("perfectZone").objectReferenceValue = perfect;
        so.FindProperty("needle").objectReferenceValue = needle;
        so.FindProperty("fill").objectReferenceValue = fill;
        so.FindProperty("timerFill").objectReferenceValue = timerFill;
        so.FindProperty("progressRoot").objectReferenceValue = progressGo;
        so.FindProperty("progressFill").objectReferenceValue = progressFill;
        so.ApplyModifiedProperties();

        // แสดงแผงทับ HUD แต่อยู่ใต้หน้าต่างอื่น (หน้าต่างกระเป๋า/เมนูเปิดพร้อมกันไม่ได้อยู่แล้ว)
        panel.transform.SetAsLastSibling();
        return view;
    }

    private static RectTransform BuildBarPart(Transform parent, string name, Color color)
    {
        GameObject go = FindOrCreate(parent, name);
        RectTransform rt = go.GetComponent<RectTransform>();
        SetStretch(rt, 0f, 0f, 0f, 0f);
        Image img = GetOrAdd<Image>(go);
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    // ---------- หน้าต่างทำอาหารนอกเวลาร้าน ----------

    private static void BuildCookingWindow(Canvas canvas, PlayerInteractor player)
    {
        GameObject host = FindOrCreate(canvas.transform, "CookingMenuUI");
        SetStretch(host.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

        GameObject window = FindOrCreate(host.transform, "CookingWindow");
        RectTransform windowRt = window.GetComponent<RectTransform>();
        windowRt.anchorMin = new Vector2(0.5f, 0.5f);
        windowRt.anchorMax = new Vector2(0.5f, 0.5f);
        windowRt.pivot = new Vector2(0.5f, 0.5f);
        windowRt.anchoredPosition = new Vector2(0f, 40f);
        windowRt.sizeDelta = new Vector2(720f, 760f);
        Image windowBg = GetOrAdd<Image>(window);
        windowBg.color = WindowColor;

        TMP_Text title = FindOrCreateText(window.transform, "TitleText", 36, TextAlignmentOptions.Center);
        SetTopStretch(title.rectTransform, 20f, 20f, 18f, 46f);
        title.text = "Cook";
        title.fontStyle = FontStyles.Bold;

        TMP_Text hint = FindOrCreateText(window.transform, "HintText", 20, TextAlignmentOptions.Center);
        SetTopStretch(hint.rectTransform, 20f, 20f, 66f, 28f);
        hint.text = "Click a recipe to start cooking · Esc to close";
        hint.color = new Color(1f, 1f, 1f, 0.7f);

        // รายการสูตร
        GameObject scrollGo = FindOrCreate(window.transform, "RecipeScrollView");
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
        SetStretch(scrollRt, 24f, 24f, 106f, 90f);
        Image listBg = GetOrAdd<Image>(scrollGo);
        listBg.color = ListColor;

        ScrollRect scroll = GetOrAdd<ScrollRect>(scrollGo);
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        GameObject viewportGo = FindOrCreate(scrollGo.transform, "Viewport");
        RectTransform viewportRt = viewportGo.GetComponent<RectTransform>();
        SetStretch(viewportRt, 0f, 0f, 0f, 0f);
        GetOrAdd<RectMask2D>(viewportGo);

        GameObject contentGo = FindOrCreate(viewportGo.transform, "Content");
        RectTransform contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = GetOrAdd<VerticalLayoutGroup>(contentGo);
        layout.spacing = 6f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(contentGo);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRt;
        scroll.content = contentRt;

        CookingRecipeItemUI template = BuildItemTemplate(contentGo.transform);

        TMP_Text empty = FindOrCreateText(window.transform, "EmptyText", 22, TextAlignmentOptions.Center);
        SetStretch(empty.rectTransform, 40f, 40f, 200f, 300f);
        empty.text = "Nothing you can cook with what's in your bag.";
        empty.color = new Color(1f, 1f, 1f, 0.6f);
        empty.raycastTarget = false;
        empty.gameObject.SetActive(false);

        Button cancel = BuildButton(window.transform, "CancelButton", "Cancel", ButtonColor, 24);
        RectTransform cancelRt = cancel.GetComponent<RectTransform>();
        cancelRt.anchorMin = new Vector2(0.5f, 0f);
        cancelRt.anchorMax = new Vector2(0.5f, 0f);
        cancelRt.pivot = new Vector2(0.5f, 0f);
        cancelRt.anchoredPosition = new Vector2(0f, 24f);
        cancelRt.sizeDelta = new Vector2(220f, 50f);

        CookingMenuUI ui = GetOrAdd<CookingMenuUI>(host);
        SerializedObject so = new SerializedObject(ui);
        if (player != null)
        {
            so.FindProperty("inventory").objectReferenceValue = player.GetComponent<Inventory>();
            so.FindProperty("recipeBook").objectReferenceValue = player.GetComponent<RecipeBook>();
            so.FindProperty("playerController").objectReferenceValue = player.GetComponent<PlayerController>();
        }
        so.FindProperty("windowRoot").objectReferenceValue = window;
        so.FindProperty("listContainer").objectReferenceValue = contentGo.transform;
        so.FindProperty("itemTemplate").objectReferenceValue = template;
        so.FindProperty("emptyText").objectReferenceValue = empty;
        so.FindProperty("cancelButton").objectReferenceValue = cancel;
        so.ApplyModifiedProperties();

        // พื้นหลังของตัวเก็บไม่รับคลิก (ตัวเก็บเปิดอยู่ตลอด)
        Image hostImage = host.GetComponent<Image>();
        if (hostImage != null) hostImage.raycastTarget = false;

        window.SetActive(false);
        host.transform.SetAsLastSibling();
    }

    private static CookingRecipeItemUI BuildItemTemplate(Transform parent)
    {
        GameObject item = FindOrCreate(parent, "CookItemTemplate");
        Image bg = GetOrAdd<Image>(item);
        bg.color = Color.white;

        Button button = GetOrAdd<Button>(item);
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.12f);
        colors.highlightedColor = new Color(1f, 0.85f, 0.4f, 0.35f);
        colors.pressedColor = new Color(1f, 0.85f, 0.4f, 0.55f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0.12f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.03f);
        button.colors = colors;

        LayoutElement le = GetOrAdd<LayoutElement>(item);
        le.preferredHeight = 66f;
        le.minHeight = 66f;

        TMP_Text nameText = FindOrCreateText(item.transform, "NameText", 24, TextAlignmentOptions.Left);
        SetTopStretch(nameText.rectTransform, 14f, 14f, 6f, 30f);
        nameText.overflowMode = TextOverflowModes.Ellipsis;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.raycastTarget = false;

        TMP_Text infoText = FindOrCreateText(item.transform, "InfoText", 17, TextAlignmentOptions.Left);
        SetTopStretch(infoText.rectTransform, 14f, 14f, 36f, 24f);
        infoText.color = new Color(1f, 1f, 1f, 0.7f);
        infoText.overflowMode = TextOverflowModes.Ellipsis;
        infoText.textWrappingMode = TextWrappingModes.NoWrap;
        infoText.raycastTarget = false;

        CookingRecipeItemUI itemUI = GetOrAdd<CookingRecipeItemUI>(item);
        SerializedObject so = new SerializedObject(itemUI);
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("infoText").objectReferenceValue = infoText;
        so.ApplyModifiedProperties();

        item.SetActive(false);
        return itemUI;
    }

    private static Button BuildButton(Transform parent, string name, string label, Color color, float fontSize)
    {
        GameObject go = FindOrCreate(parent, name);
        Image bg = GetOrAdd<Image>(go);
        bg.color = color;

        Button button = GetOrAdd<Button>(go);
        button.targetGraphic = bg;

        TMP_Text text = FindOrCreateText(go.transform, "Label", fontSize, TextAlignmentOptions.Center);
        SetStretch(text.rectTransform, 0f, 0f, 0f, 0f);
        text.text = label;
        text.raycastTarget = false;

        return button;
    }

    // ---------- รูปสี่เหลี่ยม ----------

    // รูปสีขาว 4x4 พิกเซล = 1 หน่วยพอดี (ใช้ไฟล์เดียวกับ Build Customer System)
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

    private static void SetTopCenter(RectTransform rt, float top, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        rt.sizeDelta = size;
    }

    private static void SetStretch(RectTransform rt, float left, float right, float top, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
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

    private static TMP_Text FindOrCreateText(Transform parent, string name, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = FindOrCreate(parent, name);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();

        if (text == null)
        {
            text = Undo.AddComponent<TextMeshProUGUI>(go);
            text.text = "";
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
