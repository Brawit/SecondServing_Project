using System.Collections.Generic;
using UnityEngine;

// โต๊ะในร้าน: รวมเก้าอี้ (Seat) ที่เป็นลูกของมัน
// ตอนนี้ลูกค้านั่งทีละคนต่อที่นั่ง (ยังไม่มีลูกค้าเป็นกลุ่ม) โต๊ะใช้จัดกลุ่มที่นั่งไว้สำหรับระบบกลุ่มในอนาคต
// โต๊ะควรมี Collider2D ทึบ (ไม่ใช่ Trigger) เพื่อให้ GridPathfinder รู้ว่าเดินผ่านไม่ได้
public class Table : MonoBehaviour
{
    private readonly List<Seat> seats = new List<Seat>();

    public IReadOnlyList<Seat> Seats
    {
        get
        {
            if (seats.Count == 0)
            {
                GetComponentsInChildren(true, seats);
            }
            return seats;
        }
    }

    // จำนวนที่นั่งที่ยังว่าง
    public int FreeSeatCount
    {
        get
        {
            int count = 0;
            foreach (Seat seat in Seats)
            {
                if (seat != null && seat.IsFree) count++;
            }
            return count;
        }
    }

    private void Awake()
    {
        seats.Clear();
        GetComponentsInChildren(true, seats);

        if (seats.Count == 0)
        {
            Debug.LogWarning($"Table '{name}': No Seat found in children.", this);
        }
    }
}
