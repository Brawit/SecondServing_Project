using UnityEngine;
using UnityEngine.InputSystem;

// บังคับให้วัตถุที่ใส่สคริปต์นี้ต้องมี Rigidbody2D เสมอ
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f; // ความเร็วเดิน (ปรับได้ใน Inspector)

    private Rigidbody2D rb;
    private InputAction moveAction;   // ตัวรับค่าปุ่มเดิน
    private Vector2 moveInput;        // ทิศทางที่ผู้เล่นกดอยู่ในเฟรมนี้

    // ตอนนี้ผู้เล่นเดินได้หรือไม่ (ปิดไว้ตอนเปิดกระเป๋า/เมนู)
    public bool CanMove { get; private set; } = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // สร้าง Input Action สำหรับเดิน โดยไม่ต้องพึ่งไฟล์ Input Actions ภายนอก
        moveAction = new InputAction("Move", InputActionType.Value);

        // WASD
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        // ปุ่มลูกศร
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        // จอยสติ๊กซ้าย (เผื่อรองรับจอย)
        moveAction.AddBinding("<Gamepad>/leftStick");
    }

    private void OnEnable()
    {
        moveAction.Enable(); // เริ่มรับค่าปุ่มเมื่อวัตถุทำงาน
    }

    private void OnDisable()
    {
        moveAction.Disable(); // หยุดรับค่าปุ่มเมื่อวัตถุถูกปิด
    }

    private void Update()
    {
        // ถ้าห้ามเดินอยู่ ไม่ต้องอ่านปุ่ม ให้ทิศทางเป็นศูนย์
        if (!CanMove)
        {
            moveInput = Vector2.zero;
            return;
        }

        // อ่านค่าปุ่มทุกเฟรม และจำกัดความยาวไว้ไม่เกิน 1
        // เพื่อไม่ให้เดินเฉียงเร็วกว่าเดินตรง
        moveInput = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
    }

    // เปิด/ปิดการเดิน ให้ระบบอื่นเรียกใช้ เช่น หน้าต่างกระเป๋า
    public void SetCanMove(bool value)
    {
        CanMove = value;

        if (!CanMove)
        {
            // หยุดตัวละครทันที ไม่ให้ไถลต่อด้วยความเร็วเดิม
            moveInput = Vector2.zero;
            rb.linearVelocity = Vector2.zero;
        }

        Debug.Log($"Player movement {(CanMove ? "enabled" : "disabled")}.");
    }

    private void FixedUpdate()
    {
        // ขยับตัวละครผ่านระบบฟิสิกส์ เพื่อให้ชนกำแพงและของต่างๆ ได้ถูกต้อง
        // หมายเหตุ: ใน Unity 6 ใช้ linearVelocity แทน velocity แบบเดิม
        rb.linearVelocity = moveInput * moveSpeed;
    }
}