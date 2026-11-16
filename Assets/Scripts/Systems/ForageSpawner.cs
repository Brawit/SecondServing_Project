using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// ตัวสุ่มของป่า: ทุกเช้าจะให้แต่ละโซน (ForageSpawnZone) งอกของเพิ่มทีละน้อยจนถึงเพดานของโซน
// ของที่ยังไม่ถูกเก็บจะอยู่ต่อข้ามวัน ไม่ถูกล้าง
// วิธีใช้: วางสคริปต์นี้บนวัตถุเปล่าหนึ่งตัว แล้วสร้างโซนเป็นลูกของวัตถุนี้
public class ForageSpawner : MonoBehaviour
{
    [Header("Tilemaps")]
    [Tooltip("Tilemap พื้น ของจะเกิดได้เฉพาะช่องที่มี Tile ในนี้")]
    [SerializeField] private Tilemap groundTilemap;

    [Tooltip("Tilemap สิ่งกีดขวาง ของจะไม่เกิดบนช่องที่มี Tile ในนี้")]
    [SerializeField] private Tilemap obstaclesTilemap;

    [Header("References")]
    [Tooltip("ตัวผู้เล่น ใช้กันไม่ให้ของเกิดทับช่องที่ผู้เล่นยืน (เว้นว่างได้)")]
    [SerializeField] private Transform player;

    [Tooltip("วัตถุแม่ของของที่สร้างขึ้น เพื่อให้ Hierarchy เป็นระเบียบ (เว้นว่าง = ใช้วัตถุนี้)")]
    [SerializeField] private Transform spawnedParent;

    [Header("Placement")]
    [Tooltip("จำนวนครั้งสูงสุดที่ลองสุ่มตำแหน่งต่อของหนึ่งชิ้น")]
    [SerializeField] private int maxAttemptsPerItem = 30;

    [Header("Debug")]
    [Tooltip("พิมพ์สรุปจำนวนของที่เกิดในแต่ละโซนลง Console")]
    [SerializeField] private bool logSpawns = true;

    // โซนทั้งหมดที่เป็นลูกของวัตถุนี้
    private ForageSpawnZone[] zones = new ForageSpawnZone[0];

    // ช่องที่มีของวางอยู่แล้ว ใช้กันไม่ให้ของซ้อนกันในช่องเดียว
    private readonly HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();

    // วันล่าสุดที่ประมวลผลไปแล้ว (0 = ยังไม่เคย) ใช้กันไม่ให้งอกซ้ำในวันเดียวกัน
    private int lastProcessedDay = 0;

    private bool hasStarted = false;
    private bool isSubscribed = false;

    private void Start()
    {
        hasStarted = true;

        // หาโซนทั้งหมดที่เป็นลูกของวัตถุนี้
        zones = GetComponentsInChildren<ForageSpawnZone>();
        if (zones.Length == 0)
        {
            Debug.LogWarning("ForageSpawner: No ForageSpawnZone found in children.", this);
        }

        if (groundTilemap == null)
        {
            Debug.LogError("ForageSpawner: Ground Tilemap is not assigned. Nothing will spawn.", this);
        }

        Subscribe();

        // ประมวลผลวันปัจจุบันเองหนึ่งครั้ง เพราะไม่รู้ว่า GameClock ยิง OnDayStarted ของวันแรกไปก่อนหรือยัง
        // ถ้า event ของวันเดียวกันตามมาทีหลัง จะถูกข้ามด้วย lastProcessedDay
        int currentDay = GameClock.Instance != null ? GameClock.Instance.Day : 1;
        ProcessDay(currentDay);
    }

    private void OnEnable()
    {
        // ครั้งแรกจะสมัครใน Start (รอให้ GameClock.Instance พร้อมก่อน) ส่วนตรงนี้ใช้ตอนถูกเปิดกลับมาใหม่
        if (hasStarted)
        {
            Subscribe();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    // สมัครรับ event เริ่มวันใหม่จากนาฬิกา
    private void Subscribe()
    {
        if (isSubscribed)
        {
            return;
        }

        if (GameClock.Instance == null)
        {
            Debug.LogWarning("ForageSpawner: GameClock.Instance not found. Daily growth will not run.", this);
            return;
        }

        GameClock.Instance.OnDayStarted += HandleDayStarted;
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
            GameClock.Instance.OnDayStarted -= HandleDayStarted;
        }
        isSubscribed = false;
    }

    private void HandleDayStarted(int day)
    {
        ProcessDay(day);
    }

    // ประมวลผลหนึ่งวัน: ครั้งแรกสุดจะเติมของเริ่มต้น ครั้งต่อไปจะงอกเพิ่มตามโอกาส
    private void ProcessDay(int day)
    {
        // วันนี้ทำไปแล้ว ไม่ทำซ้ำ
        if (day == lastProcessedDay)
        {
            return;
        }

        bool isInitialFill = lastProcessedDay == 0;
        lastProcessedDay = day;

        GrowAllZones(isInitialFill, day);
    }

    // ให้ทุกโซนงอกของ (isInitialFill = เติมของเริ่มต้นตอนเริ่มเกม)
    private void GrowAllZones(bool isInitialFill, int day)
    {
        if (groundTilemap == null)
        {
            return;
        }

        RebuildOccupiedCells();

        foreach (ForageSpawnZone zone in zones)
        {
            if (zone == null || !zone.isActiveAndEnabled)
            {
                continue;
            }

            // โซนยังรับของเพิ่มได้อีกกี่ชิ้น
            int aliveCount = zone.RemoveCollectedItems();
            int room = Mathf.Max(0, zone.MaxItems - aliveCount);

            // คำนวณว่ารอบนี้อยากให้เกิดกี่ชิ้น
            int wanted = 0;
            if (isInitialFill)
            {
                wanted = Mathf.Min(zone.InitialItems, room);
            }
            else
            {
                // ทอยทีละครั้ง แต่ละครั้งมีโอกาสสำเร็จตาม GrowChance
                int rolls = Mathf.Min(zone.MaxNewPerDay, room);
                for (int i = 0; i < rolls; i++)
                {
                    if (Random.value < zone.GrowChance)
                    {
                        wanted++;
                    }
                }
            }

            // สร้างของจริง
            int spawned = 0;
            for (int i = 0; i < wanted; i++)
            {
                if (TrySpawnInZone(zone))
                {
                    spawned++;
                }
            }

            if (spawned < wanted)
            {
                Debug.LogWarning($"ForageSpawner: Zone '{zone.name}' spawned {spawned}/{wanted}. Check zone position, Spawn Table, or free tiles.", zone);
            }

            if (logSpawns)
            {
                Debug.Log($"ForageSpawner: Day {day} - Zone '{zone.name}' +{spawned} (total {aliveCount + spawned}/{zone.MaxItems})");
            }
        }
    }

    // สร้างรายการช่องที่มีของอยู่แล้วขึ้นมาใหม่ จากของที่ยังไม่ถูกเก็บในทุกโซน
    private void RebuildOccupiedCells()
    {
        occupiedCells.Clear();

        foreach (ForageSpawnZone zone in zones)
        {
            if (zone == null)
            {
                continue;
            }

            zone.RemoveCollectedItems();

            foreach (ForageItem item in zone.AliveItems)
            {
                occupiedCells.Add(WorldToGroundCell(item.transform.position));
            }
        }
    }

    // ลองวางของหนึ่งชิ้นในโซน คืนค่า true ถ้าวางสำเร็จ
    private bool TrySpawnInZone(ForageSpawnZone zone)
    {
        ForageItem prefab = zone.PickRandomPrefab();
        if (prefab == null)
        {
            Debug.LogWarning($"ForageSpawner: Zone '{zone.name}' has no valid prefab in Spawn Table.", zone);
            return false;
        }

        for (int attempt = 0; attempt < maxAttemptsPerItem; attempt++)
        {
            Vector3Int cell = WorldToGroundCell(zone.GetRandomPoint());

            if (!IsCellValid(cell))
            {
                continue;
            }

            // วางที่กลางช่อง Tile
            Vector3 position = groundTilemap.GetCellCenterWorld(cell);
            position.z = 0f;

            Transform parent = spawnedParent != null ? spawnedParent : transform;
            ForageItem item = Instantiate(prefab, position, Quaternion.identity, parent);

            zone.RegisterItem(item);
            occupiedCells.Add(cell);
            return true;
        }

        return false;
    }

    // ช่องนี้วางของได้หรือไม่
    private bool IsCellValid(Vector3Int cell)
    {
        // ต้องมีพื้น (ถ้าไม่มี Tile แปลว่าอยู่นอกแผนที่)
        if (!groundTilemap.HasTile(cell))
        {
            return false;
        }

        // ต้องไม่มีสิ่งกีดขวาง
        if (obstaclesTilemap != null && obstaclesTilemap.HasTile(cell))
        {
            return false;
        }

        // ต้องยังไม่มีของวางอยู่
        if (occupiedCells.Contains(cell))
        {
            return false;
        }

        // ต้องไม่ใช่ช่องที่ผู้เล่นยืนอยู่
        if (player != null && WorldToGroundCell(player.position) == cell)
        {
            return false;
        }

        return true;
    }

    // แปลงตำแหน่งในโลกเป็นพิกัดช่องของ Tilemap พื้น (บังคับ z = 0 เพราะ Tile ถูกวาดที่ชั้น 0)
    private Vector3Int WorldToGroundCell(Vector3 worldPosition)
    {
        Vector3Int cell = groundTilemap.WorldToCell(worldPosition);
        cell.z = 0;
        return cell;
    }

    // ---------- Debug ----------

    // คลิกขวาที่ชื่อคอมโพเนนต์ใน Inspector ตอนกำลัง Play เพื่อสั่งให้งอกหนึ่งรอบทันที
    [ContextMenu("Debug: Grow Now")]
    private void DebugGrowNow()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("ForageSpawner: Grow Now only works in Play mode.");
            return;
        }

        GrowAllZones(false, lastProcessedDay);
    }

    private void OnValidate()
    {
        maxAttemptsPerItem = Mathf.Max(1, maxAttemptsPerItem);
    }
}
