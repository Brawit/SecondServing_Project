using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ตัวกด E ของผู้เล่น: หาสิ่งที่ใช้งานได้ (IInteractable) ที่ใกล้ที่สุดแล้วใช้งานชิ้นเดียว
// ใช้แทน PlayerForager เดิม (ของป่า ประตู เคาน์เตอร์ เตียง ใช้ปุ่มเดียวกันหมด)
[RequireComponent(typeof(Inventory))]
public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("ระยะที่กด E ใช้งานของได้")]
    [SerializeField] private float interactRange = 1.2f;

    // ให้สิ่งที่ถูกใช้งานเข้าถึงส่วนต่างๆ ของผู้เล่นได้
    public Inventory Inventory { get; private set; }
    public PlayerController PlayerController { get; private set; }
    public PlayerTeleporter Teleporter { get; private set; }

    // ใช้ซ้ำทุกครั้ง ไม่ต้องสร้าง List ใหม่ทุกครั้งที่กด
    private readonly List<Collider2D> hitBuffer = new List<Collider2D>();
    private ContactFilter2D contactFilter;

    private void Awake()
    {
        Inventory = GetComponent<Inventory>();
        PlayerController = GetComponent<PlayerController>();
        Teleporter = GetComponent<PlayerTeleporter>();

        // รวม Collider แบบ Trigger ด้วย เพราะของป่า/ประตูใช้ Trigger
        contactFilter = new ContactFilter2D();
        contactFilter.NoFilter();
        contactFilter.useTriggers = true;

        if (Teleporter == null)
        {
            Debug.LogWarning("PlayerInteractor: PlayerTeleporter not found on Player. Doors will not work.", this);
        }
    }

    private void Update()
    {
        // ถ้าผู้เล่นถูกล็อกอยู่ (เช่น เปิดกระเป๋า หน้าสรุปวัน) ห้ามกด E
        bool canAct = PlayerController == null || PlayerController.CanMove;

        Keyboard keyboard = Keyboard.current;
        if (canAct && keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    // หาสิ่งที่ใกล้ที่สุดที่ใช้งานได้ แล้วใช้งาน คืนค่า true ถ้าเจอ
    public bool TryInteract()
    {
        int count = Physics2D.OverlapCircle(transform.position, interactRange, contactFilter, hitBuffer);

        IInteractable nearest = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit == null)
            {
                continue;
            }

            // ข้าม Collider ของตัวผู้เล่นเอง
            if (hit.attachedRigidbody != null && hit.attachedRigidbody.gameObject == gameObject)
            {
                continue;
            }

            // สนใจเฉพาะวัตถุที่ใช้งานได้
            if (!hit.TryGetComponent(out IInteractable interactable))
            {
                continue;
            }

            if (!interactable.CanInteract(this))
            {
                continue;
            }

            // วัดระยะถึงจุดที่ใกล้ที่สุดของ Collider (ประตูกว้างๆ จะวัดได้แม่นกว่าวัดจากจุดกลาง)
            Vector2 closestPoint = hit.ClosestPoint(transform.position);
            float distance = Vector2.Distance(transform.position, closestPoint);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = interactable;
            }
        }

        if (nearest == null)
        {
            return false;
        }

        nearest.Interact(this);
        return true;
    }

    // วาดวงกลมระยะกด E ใน Scene view เพื่อให้ปรับค่าง่าย (เห็นเฉพาะตอนเลือกตัวละคร)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
