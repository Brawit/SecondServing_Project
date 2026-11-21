using UnityEngine;

// ที่นั่ง 1 ที่ (เก้าอี้) ลูกค้า 1 คนต่อ 1 ที่
// วางเป็นลูกของโต๊ะ (Table) หรือวางเดี่ยวๆ ก็ได้ CustomerManager จะหาที่นั่งทุกตัวในฉากตอนเปิดร้าน
// เก้าอี้ไม่ควรมี Collider ทึบ (ลูกค้าต้องเดินเข้าไปนั่งได้) ส่วนโต๊ะมี Collider ทึบได้
public class Seat : MonoBehaviour
{
    [Header("Seat")]
    [Tooltip("จุดที่ลูกค้านั่ง (เว้นว่างไว้ = ตำแหน่งของเก้าอี้นี้)")]
    [SerializeField] private Transform sitPoint;

    [Tooltip("โต๊ะที่เก้าอี้นี้อยู่ (เว้นว่างไว้ จะหาจากวัตถุแม่)")]
    [SerializeField] private Table table;

    // ลูกค้าที่จองหรือนั่งอยู่ (null = ว่าง)
    public Customer Occupant { get; private set; }

    public bool IsFree => Occupant == null;
    public Table Table => table;
    public Vector3 SitPosition => sitPoint != null ? sitPoint.position : transform.position;

    private void Awake()
    {
        if (table == null)
        {
            table = GetComponentInParent<Table>();
        }
    }

    // จองที่นั่งให้ลูกค้า คืนค่า false ถ้ามีคนจองแล้ว
    public bool TryReserve(Customer customer)
    {
        if (customer == null || Occupant != null)
        {
            return false;
        }

        Occupant = customer;
        return true;
    }

    // ปล่อยที่นั่ง (เฉพาะลูกค้าที่จองไว้เท่านั้น)
    public void Release(Customer customer)
    {
        if (Occupant == customer)
        {
            Occupant = null;
        }
    }

    // ปล่อยที่นั่งทันทีไม่ว่าใครนั่ง (ใช้ตอนเก็บลูกค้าทั้งหมดออกเมื่อปิดร้าน)
    public void ForceRelease()
    {
        Occupant = null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Occupant != null ? new Color(1f, 0.4f, 0.3f) : new Color(0.3f, 1f, 0.5f);
        Gizmos.DrawWireSphere(SitPosition, 0.25f);
    }
}
