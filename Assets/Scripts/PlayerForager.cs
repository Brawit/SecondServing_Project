using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Inventory))]
public class PlayerForager : MonoBehaviour
{
    [Header("Forage")]
    [SerializeField] private float forageRange = 1.2f; // ระยะที่เก็บของได้

    private Inventory inventory;
    private PlayerController playerController; // ใช้เช็กว่าตอนนี้ผู้เล่นถูกล็อกอยู่หรือไม่

    private void Awake()
    {
        inventory = GetComponent<Inventory>();
        playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        // ถ้าผู้เล่นถูกล็อกอยู่ (เช่น เปิดกระเป๋า) ห้ามเก็บของ
        bool canAct = playerController == null || playerController.CanMove;

        // กด E เพื่อเก็บของ (Keyboard.current อาจเป็น null ถ้าไม่มีคีย์บอร์ด)
        if (canAct && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryForage();
        }

        // กด I เพื่อดูของในกระเป๋า (ชั่วคราว ไว้ทดสอบ)
        if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
        {
            inventory.PrintInventory();
        }
    }

    private void TryForage()
    {
        // หาทุกวัตถุที่มี Collider อยู่ในระยะรอบตัว
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, forageRange);

        ForageItem nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            // สนใจเฉพาะวัตถุที่มี ForageItem
            if (!hit.TryGetComponent(out ForageItem item)) continue;

            float distance = Vector2.Distance(transform.position, hit.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = item;
            }
        }

        // เก็บชิ้นที่ใกล้ที่สุด
        if (nearest != null) nearest.Collect(inventory);
    }

    // วาดวงกลมระยะเก็บของใน Scene view เพื่อให้ปรับค่าง่าย (เห็นเฉพาะตอนเลือกตัวละคร)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, forageRange);
    }
}