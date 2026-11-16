using System;
using System.Collections.Generic;
using UnityEngine;

// โซนเกิดของป่า: กำหนดพื้นที่ ชนิดของที่เกิด และความเร็วในการงอก
// ติดบนวัตถุเปล่าหนึ่งตัวต่อหนึ่งโซน แล้ววางเป็นลูกของวัตถุที่มี ForageSpawner
public class ForageSpawnZone : MonoBehaviour
{
    // หนึ่งแถวในตารางสุ่ม: Prefab ไหน และมีน้ำหนักเท่าไร
    [Serializable]
    public class SpawnEntry
    {
        [Tooltip("Prefab ของที่เก็บได้ (ต้องมีสคริปต์ ForageItem)")]
        public ForageItem prefab;

        [Tooltip("น้ำหนักการสุ่ม ยิ่งมากยิ่งออกบ่อย (0 = ไม่ออกเลย)")]
        [Min(0f)] public float weight = 1f;
    }

    [Header("Save")]
    [Tooltip("รหัสโซน ใช้ในไฟล์เซฟ ต้องไม่ซ้ำกับโซนอื่น (เว้นว่าง = ใช้ชื่อวัตถุ)")]
    [SerializeField] private string zoneId;

    [Header("Zone Area")]
    [Tooltip("ขนาดโซน (กว้าง, สูง) หน่วยเป็น Unit โดยจุดกลางคือตำแหน่งของวัตถุนี้")]
    [SerializeField] private Vector2 size = new Vector2(6f, 4f);

    [Header("Spawn Table")]
    [Tooltip("รายการของที่เกิดได้ในโซนนี้")]
    [SerializeField] private List<SpawnEntry> entries = new List<SpawnEntry>();

    [Header("Amounts")]
    [Tooltip("จำนวนของสูงสุดที่โซนนี้มีได้พร้อมกัน")]
    [SerializeField] private int maxItems = 6;

    [Tooltip("จำนวนของที่มีตั้งแต่เริ่มเกม")]
    [SerializeField] private int initialItems = 3;

    [Tooltip("จำนวนสูงสุดที่พยายามงอกเพิ่มในแต่ละเช้า")]
    [SerializeField] private int maxNewPerDay = 2;

    [Tooltip("โอกาสที่การงอกแต่ละครั้งจะสำเร็จ (0 = ไม่งอกเลย, 1 = งอกแน่นอน)")]
    [SerializeField, Range(0f, 1f)] private float growChance = 0.5f;

    [Header("Gizmo")]
    [Tooltip("สีกรอบโซนใน Scene view")]
    [SerializeField] private Color gizmoColor = new Color(0.4f, 1f, 0.4f, 1f);

    // ของที่โซนนี้สร้างไว้และยังไม่ถูกเก็บ
    private readonly List<ForageItem> aliveItems = new List<ForageItem>();

    // ---------- ค่าที่ ForageSpawner อ่านไปใช้ ----------

    // รหัสโซนที่ใช้จริง (ถ้าไม่ได้กรอก ใช้ชื่อวัตถุแทน)
    public string ZoneId => string.IsNullOrWhiteSpace(zoneId) ? gameObject.name : zoneId.Trim();

    public int MaxItems => maxItems;
    public int InitialItems => initialItems;
    public int MaxNewPerDay => maxNewPerDay;
    public float GrowChance => growChance;

    // รายการของที่ยังอยู่ (ระบบเซฟดึงไปใช้) ควรเรียก RemoveCollectedItems() ก่อนอ่าน
    public IReadOnlyList<ForageItem> AliveItems => aliveItems;

    // ตรวจค่าที่กรอกใน Inspector ไม่ให้ผิดจนเกมพัง
    private void OnValidate()
    {
        size.x = Mathf.Max(0.1f, size.x);
        size.y = Mathf.Max(0.1f, size.y);
        maxItems = Mathf.Max(0, maxItems);
        initialItems = Mathf.Clamp(initialItems, 0, maxItems);
        maxNewPerDay = Mathf.Max(0, maxNewPerDay);
    }

    // ลบของที่ถูกเก็บไปแล้วออกจากรายการ แล้วคืนจำนวนที่ยังเหลืออยู่
    // (ของที่ถูก Destroy แล้วจะเทียบกับ null ได้เป็น true ใน Unity)
    public int RemoveCollectedItems()
    {
        aliveItems.RemoveAll(item => item == null);
        return aliveItems.Count;
    }

    // จดของที่เพิ่งสร้างไว้ในรายการของโซนนี้
    public void RegisterItem(ForageItem item)
    {
        if (item != null)
        {
            aliveItems.Add(item);
        }
    }

    // สุ่มจุดหนึ่งจุดภายในกล่องโซน (ไม่สนใจการหมุนของวัตถุ)
    public Vector3 GetRandomPoint()
    {
        Vector3 center = transform.position;
        float x = UnityEngine.Random.Range(-size.x * 0.5f, size.x * 0.5f);
        float y = UnityEngine.Random.Range(-size.y * 0.5f, size.y * 0.5f);
        return new Vector3(center.x + x, center.y + y, 0f);
    }

    // หา Prefab ในตารางสุ่มที่ให้ไอเทมรหัสนี้ (ใช้ตอนโหลดเซฟ) คืนค่า null ถ้าไม่เจอ
    public ForageItem FindPrefabByItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        foreach (SpawnEntry entry in entries)
        {
            if (entry == null || entry.prefab == null || entry.prefab.ItemData == null)
            {
                continue;
            }

            if (entry.prefab.ItemData.itemId == itemId)
            {
                return entry.prefab;
            }
        }

        return null;
    }

    // สุ่ม Prefab ตามน้ำหนัก คืนค่า null ถ้าไม่มีแถวไหนใช้ได้
    public ForageItem PickRandomPrefab()
    {
        // รวมน้ำหนักเฉพาะแถวที่ใส่ Prefab แล้วและน้ำหนักมากกว่า 0
        float totalWeight = 0f;
        foreach (SpawnEntry entry in entries)
        {
            if (entry != null && entry.prefab != null && entry.weight > 0f)
            {
                totalWeight += entry.weight;
            }
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        // สุ่มเลขในช่วงน้ำหนักรวม แล้วไล่หักทีละแถวจนเจอแถวที่ถูกเลือก
        float roll = UnityEngine.Random.Range(0f, totalWeight);
        ForageItem lastValid = null;

        foreach (SpawnEntry entry in entries)
        {
            if (entry == null || entry.prefab == null || entry.weight <= 0f)
            {
                continue;
            }

            lastValid = entry.prefab;

            if (roll < entry.weight)
            {
                return entry.prefab;
            }
            roll -= entry.weight;
        }

        // กันกรณีทศนิยมคลาดเคลื่อนจนหลุดลูป ให้คืนแถวสุดท้ายที่ใช้ได้
        return lastValid;
    }

    // วาดกรอบโซนใน Scene view ตลอดเวลา จะได้เห็นว่าโซนอยู่ตรงไหน
    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, size.y, 0f));
    }

    // ตอนเลือกโซนอยู่ ให้ระบายสีจางๆ ด้านในเพื่อให้เห็นชัดขึ้น
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.15f);
        Gizmos.DrawCube(transform.position, new Vector3(size.x, size.y, 0f));
    }
}
