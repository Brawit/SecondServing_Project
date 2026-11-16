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

        Debug.Log($"SaveManager: Game saved (next day {data.day}, {data.inventory.Count} inventory entries, {data.groundItems.Count} ground items).");
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
            result.Add(entry);
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

        int inventoryCount = data.inventory != null ? data.inventory.Count : 0;
        int groundCount = data.groundItems != null ? data.groundItems.Count : 0;
        Debug.Log($"SaveManager: Game loaded (day {data.day}, {inventoryCount} inventory entries, {groundCount} ground items).");
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
        }

        playerInventory.SetItems(items);
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
