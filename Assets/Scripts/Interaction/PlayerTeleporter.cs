using Unity.Cinemachine;
using UnityEngine;

// ย้ายผู้เล่นไปจุดอื่นในฉาก และเปลี่ยนขอบกล้องให้ตรงพื้นที่ใหม่ (ใช้กับประตู การตื่นนอน ฯลฯ)
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerTeleporter : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("กล้อง Cinemachine ที่ตามผู้เล่น (PlayerFollowCam)")]
    [SerializeField] private CinemachineCamera followCamera;

    [Tooltip("คอมโพเนนต์ Confiner 2D บนกล้องตัวเดียวกัน (เว้นว่างได้ จะหาจากกล้องให้เอง)")]
    [SerializeField] private CinemachineConfiner2D confiner;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (confiner == null && followCamera != null)
        {
            confiner = followCamera.GetComponent<CinemachineConfiner2D>();
        }

        if (followCamera == null) Debug.LogWarning("PlayerTeleporter: Follow Camera is not assigned.", this);
        if (confiner == null) Debug.LogWarning("PlayerTeleporter: Confiner 2D not found. Camera bounds will not change.", this);
    }

    // ย้ายผู้เล่นไปที่ตำแหน่ง แล้วเปลี่ยนขอบกล้อง (cameraBounds = null คือใช้ขอบเดิม)
    public void TeleportTo(Vector3 position, Collider2D cameraBounds)
    {
        // คงค่า Z เดิมของผู้เล่นไว้
        Vector3 target = new Vector3(position.x, position.y, transform.position.z);

        // หยุดความเร็วก่อน ไม่ให้ไถลต่อหลังวาร์ป
        rb.linearVelocity = Vector2.zero;
        rb.position = target;
        transform.position = target;

        // ถ้าลากวัตถุ Tilemap มาใส่ ช่องนี้อาจได้ Tilemap Collider 2D แทน Composite ให้ใช้ Composite ที่อยู่บนวัตถุเดียวกันแทน
        if (cameraBounds != null && !(cameraBounds is CompositeCollider2D))
        {
            CompositeCollider2D composite = cameraBounds.GetComponent<CompositeCollider2D>();
            if (composite != null)
            {
                cameraBounds = composite;
            }
        }

        // เปลี่ยนขอบกล้อง
        if (confiner != null && cameraBounds != null && confiner.BoundingShape2D != cameraBounds)
        {
            confiner.BoundingShape2D = cameraBounds;
            confiner.InvalidateBoundingShapeCache();
        }

        // ให้กล้องกระโดดไปที่ผู้เล่นทันที ไม่ค่อยๆ เลื่อนข้ามแผนที่
        if (followCamera != null)
        {
            followCamera.PreviousStateIsValid = false;
        }

        Debug.Log($"Player teleported to {target}.");
    }
}
