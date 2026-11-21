using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// ตัวหาเส้นทางแบบตาราง (A*) อ่านช่องที่เดินได้จาก Tilemap ใช้กับลูกค้าในร้าน (และ NPC เฟส 3)
// ช่องที่เดินได้ = มีไทล์ใน Walkable Tilemaps (ถ้าใส่ไว้) + ไม่มีไทล์ใน Blocking Tilemaps + ไม่มี Collider ทึบทับ
// Collider ที่ "ไม่นับ" เป็นสิ่งกีดขวาง:
//   - Trigger (ประตู จุดวาร์ป เคาน์เตอร์ ฯลฯ)
//   - Collider ที่มี Rigidbody2D แบบ Dynamic (ผู้เล่น)
//   - Collider ของ Tilemap ทุกตัว (ไทล์ตัดสินจาก Blocking Tilemaps แทน จึงข้าม CameraBounds_* ที่เป็น Collider ทึบได้)
// ผลการตรวจช่องถูกจำไว้ (cache) เรียก ClearCache() เมื่อย้ายเฟอร์นิเจอร์ (CustomerManager เรียกตอนเปิดร้าน)
// ควรมีตัวเดียวในฉาก
public class GridPathfinder : MonoBehaviour
{
    public static GridPathfinder Instance { get; private set; }

    [Header("Tilemaps")]
    [Tooltip("Grid ที่ใช้แปลงตำแหน่งเป็นช่อง (เว้นว่างไว้ จะใช้ Grid ของ Tilemap ตัวแรก)")]
    [SerializeField] private Grid grid;

    [Tooltip("Tilemap พื้นที่เดินได้ (เช่น Ground) ช่องที่ไม่มีไทล์ในทุกตัวนี้ = เดินไม่ได้ / เว้นว่างไว้ = ทุกช่องเดินได้")]
    [SerializeField] private Tilemap[] walkableTilemaps;

    [Tooltip("Tilemap สิ่งกีดขวาง (เช่น Obstables) ช่องที่มีไทล์ = เดินไม่ได้")]
    [SerializeField] private Tilemap[] blockingTilemaps;

    [Header("Colliders")]
    [Tooltip("Layer ของ Collider ที่ถือว่าเป็นสิ่งกีดขวาง (เช่น โต๊ะ ผนัง)")]
    [SerializeField] private LayerMask blockingLayers = ~0;

    [Tooltip("ขนาดกล่องที่ใช้ตรวจ Collider ในแต่ละช่อง (สัดส่วนของขนาดช่อง)")]
    [Range(0.1f, 1f)] [SerializeField] private float probeSize = 0.7f;

    [Header("Search")]
    [Tooltip("เดินทแยงได้หรือไม่ (ปิด = 4 ทิศแบบ Stardew)")]
    [SerializeField] private bool allowDiagonal = false;

    [Tooltip("จำนวนช่องสูงสุดที่ค้นหาต่อครั้ง กันเกมค้างถ้าหาไม่เจอ")]
    [Min(100)] [SerializeField] private int maxSearchNodes = 6000;

    [Tooltip("ถ้าจุดหมายเดินไม่ได้ ให้หาช่องที่เดินได้ใกล้ที่สุดภายในกี่ช่อง")]
    [Min(0)] [SerializeField] private int nearestSearchRadius = 2;

    [Header("Debug")]
    [Tooltip("วาดเส้นทางล่าสุดใน Scene view")]
    [SerializeField] private bool drawLastPath = true;

    private readonly Dictionary<Vector3Int, bool> walkableCache = new Dictionary<Vector3Int, bool>();
    private readonly Collider2D[] probeBuffer = new Collider2D[16];
    private ContactFilter2D probeFilter;
    private readonly List<Vector3> lastPath = new List<Vector3>();

    private static readonly Vector3Int[] Dirs4 =
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
    };

    private static readonly Vector3Int[] Dirs8 =
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
        new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0), new Vector3Int(-1, 1, 0), new Vector3Int(-1, -1, 0)
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("GridPathfinder: Duplicate instance found, destroying this one.");
            Destroy(this);
            return;
        }
        Instance = this;

        if (grid == null)
        {
            Tilemap first = FirstTilemap();
            if (first != null)
            {
                grid = first.layoutGrid;
            }
        }

        if (grid == null)
        {
            Debug.LogWarning("GridPathfinder: No Grid found. Assign Grid or at least one Tilemap.", this);
        }

        probeFilter = new ContactFilter2D();
        probeFilter.useTriggers = false;
        probeFilter.SetLayerMask(blockingLayers);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ล้างผลการตรวจช่อง (เรียกเมื่อย้าย/เพิ่มเฟอร์นิเจอร์)
    public void ClearCache()
    {
        walkableCache.Clear();
    }

    // ---------- แปลงตำแหน่ง ----------

    public Vector3Int WorldToCell(Vector3 world)
    {
        return grid != null ? grid.WorldToCell(world) : Vector3Int.RoundToInt(world);
    }

    public Vector3 CellToWorld(Vector3Int cell)
    {
        return grid != null ? grid.GetCellCenterWorld(cell) : (Vector3)cell;
    }

    // ---------- ตรวจช่อง ----------

    public bool IsWalkable(Vector3Int cell)
    {
        if (walkableCache.TryGetValue(cell, out bool cached))
        {
            return cached;
        }

        bool result = ComputeWalkable(cell);
        walkableCache[cell] = result;
        return result;
    }

    private bool ComputeWalkable(Vector3Int cell)
    {
        // 1) ต้องมีพื้น (ถ้าใส่ Walkable Tilemaps ไว้)
        if (walkableTilemaps != null && walkableTilemaps.Length > 0)
        {
            bool hasFloor = false;
            foreach (Tilemap map in walkableTilemaps)
            {
                if (map != null && map.HasTile(map.WorldToCell(CellToWorld(cell))))
                {
                    hasFloor = true;
                    break;
                }
            }
            if (!hasFloor)
            {
                return false;
            }
        }

        // 2) ต้องไม่มีไทล์สิ่งกีดขวาง
        if (blockingTilemaps != null)
        {
            foreach (Tilemap map in blockingTilemaps)
            {
                if (map != null && map.HasTile(map.WorldToCell(CellToWorld(cell))))
                {
                    return false;
                }
            }
        }

        // 3) ต้องไม่มี Collider ทึบของเฟอร์นิเจอร์
        Vector3 center = CellToWorld(cell);
        Vector2 size = (grid != null ? (Vector2)grid.cellSize : Vector2.one) * probeSize;
        int count = Physics2D.OverlapBox(center, size, 0f, probeFilter, probeBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D col = probeBuffer[i];
            if (col == null || col.isTrigger)
            {
                continue;
            }

            // ตัวละครที่เคลื่อนที่ได้ (ผู้เล่น) ไม่นับ
            if (col.attachedRigidbody != null && col.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic)
            {
                continue;
            }

            // Collider ของ Tilemap ไม่นับ (ใช้ไทล์ตัดสินแทน)
            if (col is TilemapCollider2D || col.GetComponent<Tilemap>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    // หาช่องที่เดินได้ใกล้ที่สุดภายในรัศมี (คืนค่า false ถ้าไม่เจอ)
    public bool TryFindNearestWalkable(Vector3Int cell, int radius, out Vector3Int result)
    {
        result = cell;
        if (IsWalkable(cell))
        {
            return true;
        }

        for (int r = 1; r <= radius; r++)
        {
            float best = float.MaxValue;
            bool found = false;

            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    if (Mathf.Abs(x) != r && Mathf.Abs(y) != r)
                    {
                        continue; // ตรวจเฉพาะขอบวงรอบนี้
                    }

                    Vector3Int c = new Vector3Int(cell.x + x, cell.y + y, cell.z);
                    if (!IsWalkable(c))
                    {
                        continue;
                    }

                    float d = x * x + y * y;
                    if (d < best)
                    {
                        best = d;
                        result = c;
                        found = true;
                    }
                }
            }

            if (found)
            {
                return true;
            }
        }

        return false;
    }

    // ---------- หาเส้นทาง ----------

    // หาเส้นทางจาก from ไป to ใส่จุดกลางช่องลงใน result (ไม่รวมช่องเริ่มต้น จุดสุดท้าย = ตำแหน่ง to จริง)
    // คืนค่า false ถ้าหาไม่เจอ (result จะว่าง)
    public bool FindPath(Vector3 from, Vector3 to, List<Vector3> result)
    {
        result.Clear();

        Vector3Int startCell = WorldToCell(from);
        Vector3Int goalCell = WorldToCell(to);

        // จุดเริ่ม/จุดหมายอาจทับเฟอร์นิเจอร์ (เช่น เก้าอี้ติดโต๊ะ) ให้ใช้ช่องที่เดินได้ใกล้ที่สุดแทน
        if (!TryFindNearestWalkable(startCell, nearestSearchRadius, out Vector3Int start))
        {
            Debug.LogWarning($"GridPathfinder: No walkable cell near start {startCell}.");
            return false;
        }

        if (!TryFindNearestWalkable(goalCell, nearestSearchRadius, out Vector3Int goal))
        {
            Debug.LogWarning($"GridPathfinder: No walkable cell near goal {goalCell}.");
            return false;
        }

        if (start == goal)
        {
            result.Add(to);
            RememberPath(result);
            return true;
        }

        Vector3Int[] dirs = allowDiagonal ? Dirs8 : Dirs4;

        Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        Dictionary<Vector3Int, float> gScore = new Dictionary<Vector3Int, float> { [start] = 0f };
        List<Vector3Int> open = new List<Vector3Int> { start };
        Dictionary<Vector3Int, float> fScore = new Dictionary<Vector3Int, float> { [start] = Heuristic(start, goal) };
        HashSet<Vector3Int> closed = new HashSet<Vector3Int>();

        int searched = 0;

        while (open.Count > 0)
        {
            // ช่องที่ f ต่ำสุด (ร้านเล็ก ใช้ค้นแบบเรียงธรรมดาพอ)
            int bestIndex = 0;
            float bestF = fScore[open[0]];
            for (int i = 1; i < open.Count; i++)
            {
                float f = fScore[open[i]];
                if (f < bestF)
                {
                    bestF = f;
                    bestIndex = i;
                }
            }

            Vector3Int current = open[bestIndex];
            open.RemoveAt(bestIndex);

            if (current == goal)
            {
                BuildPath(cameFrom, current, start, to, result);
                RememberPath(result);
                return true;
            }

            closed.Add(current);

            if (++searched > maxSearchNodes)
            {
                Debug.LogWarning($"GridPathfinder: Search limit reached ({maxSearchNodes}) from {start} to {goal}.");
                return false;
            }

            foreach (Vector3Int dir in dirs)
            {
                Vector3Int next = current + dir;
                if (closed.Contains(next) || !IsWalkable(next))
                {
                    continue;
                }

                // ทแยง: ห้ามตัดมุมผ่านช่องที่เดินไม่ได้
                bool diagonal = dir.x != 0 && dir.y != 0;
                if (diagonal &&
                    (!IsWalkable(new Vector3Int(current.x + dir.x, current.y, current.z)) ||
                     !IsWalkable(new Vector3Int(current.x, current.y + dir.y, current.z))))
                {
                    continue;
                }

                float tentative = gScore[current] + (diagonal ? 1.4142f : 1f);
                if (gScore.TryGetValue(next, out float existing) && tentative >= existing)
                {
                    continue;
                }

                cameFrom[next] = current;
                gScore[next] = tentative;
                fScore[next] = tentative + Heuristic(next, goal);

                if (!open.Contains(next))
                {
                    open.Add(next);
                }
            }
        }

        return false;
    }

    private float Heuristic(Vector3Int a, Vector3Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        if (allowDiagonal)
        {
            return (dx + dy) + (1.4142f - 2f) * Mathf.Min(dx, dy);
        }
        return dx + dy;
    }

    private void BuildPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int end, Vector3Int start, Vector3 finalPoint, List<Vector3> result)
    {
        List<Vector3Int> cells = new List<Vector3Int>();
        Vector3Int current = end;
        while (current != start)
        {
            cells.Add(current);
            current = cameFrom[current];
        }
        cells.Reverse();

        foreach (Vector3Int cell in cells)
        {
            result.Add(CellToWorld(cell));
        }

        // จุดสุดท้ายคือตำแหน่งจริง (เช่น จุดนั่งบนเก้าอี้)
        result.Add(finalPoint);
    }

    private void RememberPath(List<Vector3> path)
    {
        lastPath.Clear();
        lastPath.AddRange(path);
    }

    private Tilemap FirstTilemap()
    {
        if (walkableTilemaps != null)
        {
            foreach (Tilemap map in walkableTilemaps)
            {
                if (map != null) return map;
            }
        }
        if (blockingTilemaps != null)
        {
            foreach (Tilemap map in blockingTilemaps)
            {
                if (map != null) return map;
            }
        }
        return null;
    }

    // ---------- Debug ----------

    private void OnDrawGizmos()
    {
        if (!drawLastPath || lastPath.Count == 0)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        for (int i = 0; i < lastPath.Count - 1; i++)
        {
            Gizmos.DrawLine(lastPath[i], lastPath[i + 1]);
        }
        Gizmos.DrawWireSphere(lastPath[lastPath.Count - 1], 0.2f);
    }

    [ContextMenu("Debug: Clear Cache")]
    private void DebugClearCache()
    {
        ClearCache();
        Debug.Log("GridPathfinder: Cache cleared.");
    }
}
