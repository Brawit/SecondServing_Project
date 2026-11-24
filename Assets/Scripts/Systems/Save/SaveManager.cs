using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// ตัวเซฟ/โหลดเกม: เซฟอัตโนมัติตอนจบวัน (แบบ Stardew) และโหลดตอนเปิดเกม
// ทำงานก่อนสคริปต์อื่น (-100) เพื่อให้คืนข้อมูลเสร็จก่อนที่ GameClock จะเริ่มวันแรก
[DefaultExecutionOrder(-100)]
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("ไฟล์ข้อมูลรวม ItemData ทุกตัว ใช้แปลงรหัสไอเทมกลับเป็นไอเทม")]
    [SerializeField] private ItemDatabase itemDatabase;

    [Tooltip("กระเป๋าของผู้เล่น (คอมโพเนนต์ Inventory บนตัว Player)")]
    [SerializeField] private Inventory playerInventory;

    [Tooltip("ตัวสุ่มของป่า ใช้เก็บและคืนของบนพื้น")]
    [SerializeField] private ForageSpawner forageSpawner;

    [Tooltip("ไฟล์ข้อมูลรวมสูตรอาหาร ใช้แปลงรหัสสูตรกลับเป็นสูตร")]
    [SerializeField] private RecipeDatabase recipeDatabase;

    [Tooltip("สมุดสูตรของผู้เล่น (คอมโพเนนต์ RecipeBook บนตัว Player)")]
    [SerializeField] private RecipeBook playerRecipeBook;

    [Tooltip("Hotbar ของผู้เล่น (คอมโพเนนต์ Hotbar บนตัว Player) เว้นว่างไว้ จะหาจาก Player Inventory")]
    [SerializeField] private Hotbar playerHotbar;

    [Tooltip("ค่าสถานะของผู้เล่น (คอมโพเนนต์ PlayerStats บนตัว Player) เว้นว่างไว้ จะหาจาก Player Inventory")]
    [SerializeField] private PlayerStats playerStats;

    [Tooltip("ของที่ผู้เล่นสวมอยู่ (คอมโพเนนต์ PlayerEquipment บนตัว Player) เว้นว่างไว้ จะหาจาก Player Inventory")]
    [SerializeField] private PlayerEquipment playerEquipment;

    [Tooltip("เมนูประจำวัน (คอมโพเนนต์ DailyMenu) เว้นว่างไว้ จะหาในฉากเอง")]
    [SerializeField] private DailyMenu dailyMenu;

    [Tooltip("[2.5c] เลเวลร้าน + ชื่อเสียง (คอมโพเนนต์ ShopProgress บนวัตถุ ShopManager) เว้นว่างไว้ จะหาในฉากเอง")]
    [SerializeField] private ShopProgress shopProgress;

    [Tooltip("[2.5c] ระบบจดหมายเชฟ (คอมโพเนนต์ MailSystem) เว้นว่างไว้ จะหาในฉากเอง")]
    [SerializeField] private MailSystem mailSystem;

    [Tooltip("[2.6] กระเป๋าเงิน (คอมโพเนนต์ PlayerWallet บนตัว Player) เว้นว่างไว้ จะหาเอง")]
    [SerializeField] private PlayerWallet playerWallet;

    [Tooltip("[2.6] ตัวจัดการก่อสร้าง (คอมโพเนนต์ ConstructionManager) เว้นว่างไว้ จะหาในฉากเอง")]
    [SerializeField] private ConstructionManager constructionManager;

    [Header("Settings")]
    [Tooltip("ชื่อไฟล์เซฟ")]
    [SerializeField] private string fileName = "save.json";

    [Tooltip("โหลดเซฟอัตโนมัติตอนเปิดเกม")]
    [SerializeField] private bool loadOnStart = true;

    [Tooltip("เซฟอัตโนมัติตอนจบวัน")]
    [SerializeField] private bool autoSaveOnDayEnd = true;

#if UNITY_WEBGL && !UNITY_EDITOR
    // ฟังก์ชันจากไฟล์ Assets/Plugins/WebGL/SaveSync.jslib สั่งให้เบราว์เซอร์บันทึกไฟล์ลง IndexedDB ทันที
    [DllImport("__Internal")]
    private static extern void SaveSync_FlushToIndexedDB();
#endif

    private bool isSubscribed = false;

    // ที่อยู่ไฟล์เซฟจริง
    public string SavePath => Path.Combine(Application.persistentDataPath, fileName);

    // ไฟล์ชั่วคราวที่เขียนก่อนสลับเข้าไฟล์จริง
    private string TempPath => SavePath + ".tmp";

    // มีไฟล์เซฟอยู่หรือไม่ (นับไฟล์ชั่วคราวด้วย เผื่อเกมดับระหว่างสลับไฟล์)
    public bool HasSave => File.Exists(SavePath) || File.Exists(TempPath);

    private void Awake()
    {
        // ป้องกันไม่ให้มีตัวเซฟซ้ำสองตัวในฉาก
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("SaveManager: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (itemDatabase == null)
        {
            Debug.LogError("SaveManager: Item Database is not assigned. Inventory cannot be saved or loaded.", this);
        }
        if (playerInventory == null)
        {
            Debug.LogError("SaveManager: Player Inventory is not assigned.", this);
        }
        if (forageSpawner == null)
        {
            Debug.LogWarning("SaveManager: Forage Spawner is not assigned. Ground items will not be saved.", this);
        }
        if (recipeDatabase == null || playerRecipeBook == null)
        {
            Debug.LogWarning("SaveManager: Recipe Database or Player Recipe Book is not assigned. Known recipes will not be saved.", this);
        }

        // [v3] ถ้าลืมผูก Hotbar / PlayerStats ให้หาบนตัว Player เดียวกับกระเป๋า
        if (playerHotbar == null && playerInventory != null)
        {
            playerHotbar = playerInventory.GetComponent<Hotbar>();
        }
        if (playerStats == null && playerInventory != null)
        {
            playerStats = playerInventory.GetComponent<PlayerStats>();
        }
        if (playerEquipment == null && playerInventory != null)
        {
            playerEquipment = playerInventory.GetComponent<PlayerEquipment>();
        }
        if (playerEquipment == null)
        {
            Debug.LogWarning("SaveManager: Player Equipment is not assigned. Equipped items will not be saved.", this);
        }
        if (dailyMenu == null)
        {
            dailyMenu = FindFirstObjectByType<DailyMenu>(FindObjectsInactive.Include);
        }
        if (dailyMenu == null)
        {
            Debug.LogWarning("SaveManager: Daily Menu not found. The last menu will not be saved.", this);
        }
        if (shopProgress == null)
        {
            shopProgress = FindFirstObjectByType<ShopProgress>(FindObjectsInactive.Include);
        }
        if (shopProgress == null)
        {
            Debug.LogWarning("SaveManager: Shop Progress not found. Shop level and reputation will not be saved.", this);
        }
        if (mailSystem == null)
        {
            mailSystem = FindFirstObjectByType<MailSystem>(FindObjectsInactive.Include);
        }
        if (mailSystem == null)
        {
            Debug.LogWarning("SaveManager: Mail System not found. Chef letters will not be saved.", this);
        }
        if (playerWallet == null && playerInventory != null)
        {
            playerWallet = playerInventory.GetComponent<PlayerWallet>();
        }
        if (playerWallet == null)
        {
            playerWallet = FindFirstObjectByType<PlayerWallet>(FindObjectsInactive.Include);
        }
        if (playerWallet == null)
        {
            Debug.LogWarning("SaveManager: Player Wallet not found. Money will not be saved. Run Build Economy.", this);
        }
        if (constructionManager == null)
        {
            constructionManager = FindFirstObjectByType<ConstructionManager>(FindObjectsInactive.Include);
        }
        if (constructionManager == null)
        {
            Debug.LogWarning("SaveManager: Construction Manager not found. Construction will not be saved. Run Build Economy.", this);
        }
        if (playerHotbar == null)
        {
            Debug.LogWarning("SaveManager: Player Hotbar is not assigned. Hotbar slots will not be saved.", this);
        }
        if (playerStats == null)
        {
            Debug.LogWarning("SaveManager: Player Stats is not assigned. FEVER and bonus max values will not be saved.", this);
        }

        Debug.Log($"SaveManager: Save path = {SavePath}");

        if (loadOnStart && HasSave)
        {
            LoadGame();
        }

        Subscribe();
    }

    private void OnEnable()
    {
        // ครั้งแรกจะสมัครใน Start ส่วนตรงนี้ใช้ตอนวัตถุถูกเปิดกลับมาใหม่
        if (Instance == this && GameClock.Instance != null)
        {
            Subscribe();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        // F9 = ลบไฟล์เซฟ (ใช้ตอนทดสอบ ให้เปิดเกมครั้งหน้าเริ่มใหม่ตั้งแต่วันที่ 1)
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
        {
            DeleteSave();
        }
    }

    // ---------- Events ----------

    private void Subscribe()
    {
        if (isSubscribed)
        {
            return;
        }

        if (GameClock.Instance == null)
        {
            Debug.LogWarning("SaveManager: GameClock.Instance not found. Auto save will not run.", this);
            return;
        }

        GameClock.Instance.OnDayEnded += HandleDayEnded;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed)
        {
            return;
        }

        if (GameClock.Instance != null)
        {
            GameClock.Instance.OnDayEnded -= HandleDayEnded;
        }
        isSubscribed = false;
    }

    // จบวันแล้ว เซฟโดยให้โหลดครั้งหน้าเริ่มที่วันถัดไป
    private void HandleDayEnded(int endedDay)
    {
        if (autoSaveOnDayEnd)
        {
            SaveGame(endedDay + 1);
        }
    }

    // ---------- เซฟ ----------

    // เซฟเกม โดยระบุวันที่จะเริ่มเล่นเมื่อโหลด คืนค่า true ถ้าสำเร็จ
    public bool SaveGame(int dayToLoad)
    {
        SaveData data = new SaveData();
        data.day = Mathf.Max(1, dayToLoad);
        data.inventory = CaptureInventory();

        if (forageSpawner != null)
        {
            data.groundItems = forageSpawner.CaptureState();
        }

        // [v2] ไอเทมที่เคยเจอ ระดับกระเป๋า และสูตรที่รู้
        if (playerInventory != null)
        {
            data.discoveredItems = CaptureDiscoveredItems();
            data.bagLevel = playerInventory.BagLevel;
        }
        data.knownRecipes = CaptureKnownRecipes();

        // [v3] ช่อง Hotbar, FEVER และค่าสูงสุดที่เพิ่มถาวร
        if (playerHotbar != null)
        {
            data.hotbarItems = playerHotbar.CaptureSlotIds();
        }
        if (playerStats != null)
        {
            data.fever = playerStats.Fever;
            data.bonusMaxHealth = playerStats.GetBonusMax(StatType.Health);
            data.bonusMaxStamina = playerStats.GetBonusMax(StatType.Stamina);
            data.bonusMaxMana = playerStats.GetBonusMax(StatType.Mana);
        }

        // [v4] ของที่สวมอยู่
        if (playerEquipment != null)
        {
            data.equippedItems = playerEquipment.CaptureSlotIds();
        }

        // [v5] เมนูที่ตั้งไว้ล่าสุด
        if (dailyMenu != null)
        {
            dailyMenu.CaptureMenu(data.menuRecipes, data.menuServings);
        }

        // [v7] เลเวลร้าน + ชื่อเสียง และจดหมายเชฟ
        if (shopProgress != null)
        {
            data.shopLevel = shopProgress.Level;
            data.reputation = shopProgress.Reputation;
            data.lifetimeReputation = shopProgress.LifetimeReputation;
            data.reputationBarSpent = shopProgress.BarSpent;
        }

        // [v8] เงิน + งานก่อสร้าง
        if (playerWallet != null)
        {
            data.money = playerWallet.Money;
        }
        if (constructionManager != null)
        {
            constructionManager.CaptureState(data);
        }
        if (mailSystem != null)
        {
            mailSystem.CaptureState(data);
        }

        string json = JsonUtility.ToJson(data, true);

        try
        {
            // เขียนลงไฟล์ชั่วคราวก่อน ถ้าเกมดับกลางทาง ไฟล์เซฟเดิมจะยังไม่เสีย
            File.WriteAllText(TempPath, json);

            // สลับไฟล์ชั่วคราวเข้าไปแทนไฟล์จริง
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }
            File.Move(TempPath, SavePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"SaveManager: Save failed. {e.Message}");
            return false;
        }

        FlushWebStorage();

        Debug.Log($"SaveManager: Game saved (version {data.version}, next day {data.day}, {data.inventory.Count} inventory entries, {data.groundItems.Count} ground items, FEVER {data.fever:0}).");
        return true;
    }

    // แปลงของในกระเป๋าเป็นรายการ (รหัส, จำนวน)
    private List<InventoryEntrySave> CaptureInventory()
    {
        List<InventoryEntrySave> result = new List<InventoryEntrySave>();

        if (playerInventory == null)
        {
            return result;
        }

        foreach (KeyValuePair<ItemData, int> pair in playerInventory.Items)
        {
            if (pair.Key == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(pair.Key.itemId))
            {
                Debug.LogWarning($"SaveManager: Item '{pair.Key.itemName}' has no itemId and will not be saved.", pair.Key);
                continue;
            }

            InventoryEntrySave entry = new InventoryEntrySave();
            entry.itemId = pair.Key.itemId;
            entry.amount = pair.Value;

            // [v6] จานอาหาร: เก็บจำนวนแยกตามคุณภาพ
            if (Inventory.TracksQuality(pair.Key))
            {
                entry.qualityOk = playerInventory.GetCount(pair.Key, DishQuality.Ok);
                entry.qualityGood = playerInventory.GetCount(pair.Key, DishQuality.Good);
                entry.qualityPerfect = playerInventory.GetCount(pair.Key, DishQuality.Perfect);
            }

            result.Add(entry);
        }

        return result;
    }

    // แปลงไอเทมที่เคยเจอเป็นรายการรหัส
    private List<string> CaptureDiscoveredItems()
    {
        List<string> result = new List<string>();

        foreach (ItemData item in playerInventory.DiscoveredItems)
        {
            if (item != null && !string.IsNullOrWhiteSpace(item.itemId))
            {
                result.Add(item.itemId);
            }
        }

        return result;
    }

    // แปลงสูตรที่รู้เป็นรายการรหัส
    private List<string> CaptureKnownRecipes()
    {
        List<string> result = new List<string>();

        if (playerRecipeBook == null)
        {
            return result;
        }

        foreach (RecipeData recipe in playerRecipeBook.KnownRecipes)
        {
            if (recipe == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(recipe.recipeId))
            {
                Debug.LogWarning($"SaveManager: Recipe '{recipe.name}' has no recipeId and will not be saved.", recipe);
                continue;
            }

            result.Add(recipe.recipeId);
        }

        return result;
    }

    // ---------- โหลด ----------

    // โหลดเกมจากไฟล์ คืนค่า true ถ้าสำเร็จ (ควรเรียกตอนเปิดเกมเท่านั้น ก่อนนาฬิกาเริ่มเดิน)
    public bool LoadGame()
    {
        // ถ้าไฟล์จริงไม่มี แต่มีไฟล์ชั่วคราว แปลว่าเกมดับระหว่างสลับไฟล์ ให้ใช้ไฟล์ชั่วคราวแทน
        string path = File.Exists(SavePath) ? SavePath : TempPath;

        if (!File.Exists(path))
        {
            Debug.Log("SaveManager: No save file found.");
            return false;
        }

        SaveData data;
        try
        {
            string json = File.ReadAllText(path);
            data = JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"SaveManager: Load failed. {e.Message}");
            return false;
        }

        if (data == null)
        {
            Debug.LogError("SaveManager: Save file is empty or invalid.");
            return false;
        }

        if (data.version > SaveData.CurrentVersion)
        {
            Debug.LogWarning($"SaveManager: Save version {data.version} is newer than game version {SaveData.CurrentVersion}. Loading anyway.");
        }

        // 1) วันที่ ต้องตั้งก่อน GameClock เริ่มวันแรก
        if (GameClock.Instance != null)
        {
            GameClock.Instance.SetStartDay(data.day);
        }
        else
        {
            Debug.LogWarning("SaveManager: GameClock.Instance not found. Day was not restored.", this);
        }

        // 2) กระเป๋า
        RestoreInventory(data.inventory);

        // 3) ของบนพื้น
        if (forageSpawner != null && data.groundItems != null)
        {
            forageSpawner.RestoreState(data.groundItems);
        }

        // 4) [v2] ไอเทมที่เคยเจอ ระดับกระเป๋า และสูตรที่รู้
        // เซฟ v1 ไม่มีข้อมูลนี้: ไอเทมที่เคยเจอ = ของที่อยู่ในกระเป๋า, กระเป๋าระดับ 1, รู้แค่สูตรเริ่มต้น
        if (data.version >= 2)
        {
            RestoreDiscoveredItems(data.discoveredItems);

            if (playerInventory != null)
            {
                playerInventory.SetBagLevel(data.bagLevel);
            }

            RestoreKnownRecipes(data.knownRecipes);
        }
        else
        {
            Debug.Log($"SaveManager: Save version {data.version} has no discovered items or recipes. Using defaults.");
        }

        // 5) [v3] ช่อง Hotbar, FEVER และค่าสูงสุดที่เพิ่มถาวร
        // เซฟ v1/v2 ไม่มีข้อมูลนี้: Hotbar ว่าง, FEVER 0, โบนัส 0 (ค่าตั้งต้นของเกมอยู่แล้ว ไม่ต้องทำอะไร)
        if (data.version >= 3)
        {
            RestoreHotbar(data.hotbarItems);
            RestorePlayerStats(data);
        }
        else
        {
            Debug.Log($"SaveManager: Save version {data.version} has no hotbar or FEVER data. Using defaults.");
        }

        // 6) [v4] ของที่สวมอยู่ (เซฟ v1–v3 = ไม่ได้สวมอะไร ค่าตั้งต้นอยู่แล้ว)
        // ต้องทำหลังคืนกระเป๋า เพราะของที่ใส่ช่องไม่ได้แล้วจะถูกคืนเข้ากระเป๋า
        if (data.version >= 4)
        {
            RestoreEquipment(data.equippedItems);
        }

        // 6.5) [v7/v8] เลเวลร้าน + ชื่อเสียง (ต้องก่อนคืนเมนู: เลเวลให้ช่องเมนูเพิ่ม ไม่งั้นเมนูช่องที่ 4+ จะหาย)
        if (data.version >= 7 && shopProgress != null)
        {
            int lifetime = data.version >= 8 ? data.lifetimeReputation : data.reputation;
            bool barSpent = data.version >= 8 && data.reputationBarSpent;
            shopProgress.SetAll(data.shopLevel, data.reputation, lifetime, barSpent);
        }

        // 7) [v5] เมนูที่ตั้งไว้ล่าสุด (เซฟ v1–v4 = เมนูว่าง) จำนวนจานจะถูกตรวจกับของในกระเป๋าตอนเปิดหน้าต่างเมนู
        if (data.version >= 5)
        {
            RestoreMenu(data.menuRecipes, data.menuServings);
        }

        // 8) [v7] เลเวลร้าน + ชื่อเสียง และจดหมายเชฟ (เซฟ v1–v6 = เลเวล 1 ชื่อเสียง 0 ไม่มีจดหมาย ค่าตั้งต้นอยู่แล้ว)
        if (data.version >= 7)
        {
            if (mailSystem != null)
            {
                mailSystem.RestoreState(data);
            }
        }

        // 9) [v8] เงิน + งานก่อสร้าง (เซฟเก่า = เงินเริ่มต้น ไม่มีงาน) คืนค่าเฉยๆ ไม่หักเงินซ้ำ
        if (playerWallet != null)
        {
            playerWallet.SetMoney(data.version >= 8 ? data.money : playerWallet.StartMoney);
        }
        if (data.version >= 8 && constructionManager != null)
        {
            constructionManager.RestoreState(data);
        }

        int inventoryCount = data.inventory != null ? data.inventory.Count : 0;
        int groundCount = data.groundItems != null ? data.groundItems.Count : 0;
        Debug.Log($"SaveManager: Game loaded (version {data.version}, day {data.day}, {inventoryCount} inventory entries, {groundCount} ground items).");
        return true;
    }

    // แปลงรายการ (รหัส, จำนวน) กลับเป็นของในกระเป๋า
    private void RestoreInventory(List<InventoryEntrySave> entries)
    {
        if (playerInventory == null || itemDatabase == null || entries == null)
        {
            return;
        }

        Dictionary<ItemData, int> items = new Dictionary<ItemData, int>();

        // [v6] คุณภาพจาน "จาน -> [OK, Good, Perfect]" (รวมถ้ามีรหัสซ้ำ)
        Dictionary<ItemData, int[]> qualities = new Dictionary<ItemData, int[]>();

        foreach (InventoryEntrySave entry in entries)
        {
            if (entry == null || entry.amount <= 0)
            {
                continue;
            }

            ItemData item = itemDatabase.GetById(entry.itemId);
            if (item == null)
            {
                Debug.LogWarning($"SaveManager: Unknown itemId '{entry.itemId}' in save. Skipped.");
                continue;
            }

            // ถ้ามีรหัสซ้ำในไฟล์ ให้รวมจำนวน
            items.TryGetValue(item, out int current);
            items[item] = current + entry.amount;

            // [v6] เซฟเก่า (v1–v5) ไม่มีคุณภาพ = 0 ทั้งหมด → ข้าม (Inventory นับเป็น Good ให้เอง)
            if (Inventory.TracksQuality(item) && entry.qualityOk + entry.qualityGood + entry.qualityPerfect == entry.amount)
            {
                if (!qualities.TryGetValue(item, out int[] q))
                {
                    q = new int[DishQualityUtil.Count];
                    qualities[item] = q;
                }
                q[(int)DishQuality.Ok] += entry.qualityOk;
                q[(int)DishQuality.Good] += entry.qualityGood;
                q[(int)DishQuality.Perfect] += entry.qualityPerfect;
            }
        }

        playerInventory.SetItems(items);

        foreach (KeyValuePair<ItemData, int[]> pair in qualities)
        {
            playerInventory.SetDishQualities(pair.Key, pair.Value[(int)DishQuality.Ok], pair.Value[(int)DishQuality.Good], pair.Value[(int)DishQuality.Perfect]);
        }
    }

    // แปลงรายการรหัสกลับเป็นไอเทมที่เคยเจอ
    private void RestoreDiscoveredItems(List<string> itemIds)
    {
        if (playerInventory == null || itemDatabase == null || itemIds == null)
        {
            return;
        }

        List<ItemData> result = new List<ItemData>();

        foreach (string itemId in itemIds)
        {
            ItemData item = itemDatabase.GetById(itemId);
            if (item != null)
            {
                result.Add(item);
            }
            else
            {
                Debug.LogWarning($"SaveManager: Unknown discovered itemId '{itemId}' in save. Skipped.");
            }
        }

        playerInventory.SetDiscovered(result);
    }

    // แปลงรายการรหัสกลับเป็นสูตรที่รู้
    private void RestoreKnownRecipes(List<string> recipeIds)
    {
        if (playerRecipeBook == null || recipeDatabase == null || recipeIds == null)
        {
            return;
        }

        List<RecipeData> result = new List<RecipeData>();

        foreach (string recipeId in recipeIds)
        {
            RecipeData recipe = recipeDatabase.GetById(recipeId);
            if (recipe != null)
            {
                result.Add(recipe);
            }
            else
            {
                Debug.LogWarning($"SaveManager: Unknown recipeId '{recipeId}' in save. Skipped.");
            }
        }

        playerRecipeBook.SetKnown(result);
    }

    // แปลงรายการรหัสกลับเป็นช่อง Hotbar (ช่องว่างหรือรหัสที่หาไม่เจอ = ช่องว่าง)
    private void RestoreHotbar(List<string> itemIds)
    {
        if (playerHotbar == null || itemDatabase == null || itemIds == null)
        {
            return;
        }

        List<ItemData> result = new List<ItemData>(Hotbar.SlotCount);

        for (int i = 0; i < Hotbar.SlotCount; i++)
        {
            string itemId = i < itemIds.Count ? itemIds[i] : "";
            ItemData item = null;

            if (!string.IsNullOrWhiteSpace(itemId))
            {
                item = itemDatabase.GetById(itemId);
                if (item == null)
                {
                    // ไอเทมต้องอยู่ใน ItemDatabase (กด Find All Items ที่ ItemDatabase ถ้าเพิ่งสร้างไอเทมใหม่)
                    Debug.LogWarning($"SaveManager: Unknown hotbar itemId '{itemId}' in slot {i + 1}. Slot left empty. Is it in the Item Database?");
                }
            }

            result.Add(item);
        }

        playerHotbar.SetAllSlots(result);
    }

    // แปลงรายการรหัสกลับเป็นของที่สวมอยู่ (ช่องว่างหรือรหัสที่หาไม่เจอ = ช่องว่าง)
    private void RestoreEquipment(List<string> itemIds)
    {
        if (playerEquipment == null || itemDatabase == null || itemIds == null)
        {
            return;
        }

        List<ItemData> result = new List<ItemData>(PlayerEquipment.SlotCount);

        for (int i = 0; i < PlayerEquipment.SlotCount; i++)
        {
            string itemId = i < itemIds.Count ? itemIds[i] : "";
            ItemData item = null;

            if (!string.IsNullOrWhiteSpace(itemId))
            {
                item = itemDatabase.GetById(itemId);
                if (item == null)
                {
                    Debug.LogWarning($"SaveManager: Unknown equipped itemId '{itemId}' in slot {(EquipSlot)i}. Slot left empty. Is it in the Item Database?");
                }
            }

            result.Add(item);
        }

        playerEquipment.SetAllSlots(result);
    }

    // แปลงรหัสสูตรกลับเป็นเมนูที่ตั้งไว้ (รหัสที่หาไม่เจอ = ช่องว่าง)
    private void RestoreMenu(List<string> recipeIds, List<int> servings)
    {
        if (dailyMenu == null || recipeDatabase == null || recipeIds == null)
        {
            return;
        }

        List<RecipeData> recipes = new List<RecipeData>(recipeIds.Count);
        foreach (string recipeId in recipeIds)
        {
            RecipeData recipe = null;
            if (!string.IsNullOrWhiteSpace(recipeId))
            {
                recipe = recipeDatabase.GetById(recipeId);
                if (recipe == null)
                {
                    Debug.LogWarning($"SaveManager: Unknown menu recipeId '{recipeId}' in save. Slot left empty.");
                }
            }
            recipes.Add(recipe);
        }

        dailyMenu.RestoreMenu(recipes, servings);
    }

    // คืน FEVER และค่าสูงสุดที่เพิ่มถาวร แล้วเติม Health/Stamina/Mana ให้เต็มค่าสูงสุดใหม่ (โหลด = เริ่มวันใหม่)
    private void RestorePlayerStats(SaveData data)
    {
        if (playerStats == null)
        {
            return;
        }

        playerStats.SetBonusMax(StatType.Health, data.bonusMaxHealth);
        playerStats.SetBonusMax(StatType.Stamina, data.bonusMaxStamina);
        playerStats.SetBonusMax(StatType.Mana, data.bonusMaxMana);
        playerStats.SetFever(data.fever);

        // ตอน Awake ค่าปัจจุบันถูกตั้งเท่าค่าสูงสุดพื้นฐาน ต้องเติมใหม่หลังได้โบนัส
        playerStats.RefillForNewDay();
    }

    // ---------- ลบเซฟ ----------

    // ลบไฟล์เซฟ (เกมที่เล่นอยู่ไม่เปลี่ยน แต่ถ้าจบวันจะถูกเซฟใหม่อีกครั้ง)
    public void DeleteSave()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }
            if (File.Exists(TempPath))
            {
                File.Delete(TempPath);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"SaveManager: Delete save failed. {e.Message}");
            return;
        }

        FlushWebStorage();
        Debug.Log("SaveManager: Save deleted. Restart the game to begin from Day 1.");
    }

    // คลิกขวาที่หัวคอมโพเนนต์ใน Inspector เพื่อลบเซฟได้แม้ไม่ได้ Play
    [ContextMenu("Debug: Delete Save")]
    private void DebugDeleteSave()
    {
        DeleteSave();
    }

    // ---------- Web ----------

    // บนเว็บ: สั่งให้เบราว์เซอร์บันทึกไฟล์ลง IndexedDB ทันที / บน PC และใน Editor: ไม่ทำอะไร
    private void FlushWebStorage()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SaveSync_FlushToIndexedDB();
#endif
    }
}
