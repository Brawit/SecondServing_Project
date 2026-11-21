using UnityEngine;
using UnityEngine.InputSystem;

// บังคับให้วัตถุที่ใส่สคริปต์นี้ต้องมี Rigidbody2D เสมอ
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f; // ความเร็วเดิน (ปรับได้ใน Inspector)

    [Header("Run")]
    [Tooltip("วิ่งเร็วกว่าเดินกี่เท่า")]
    [Min(1f)] [SerializeField] private float runSpeedMultiplier = 1.7f;

    [Tooltip("วิ่งแล้วหัก Stamina ครั้งละเท่านี้")]
    [Min(0f)] [SerializeField] private float runStaminaCost = 1f;

    [Tooltip("หัก Stamina ทุกกี่วินาทีระหว่างวิ่ง (นับเฉพาะตอนขยับตัวจริง ยืนเฉยๆ ไม่หัก)")]
    [Min(0.05f)] [SerializeField] private float runStaminaInterval = 1f;

    [Tooltip("ค่าสถานะของผู้เล่น (เว้นว่างไว้ จะหาบน GameObject เดียวกัน) ถ้าไม่มี = วิ่งได้ไม่หัก Stamina")]
    [SerializeField] private PlayerStats stats;

    private Rigidbody2D rb;
    private InputAction moveAction;   // ตัวรับค่าปุ่มเดิน
    private InputAction runToggleAction; // ปุ่มสลับเดิน/วิ่ง (Shift)
    private Vector2 moveInput;        // ทิศทางที่ผู้เล่นกดอยู่ในเฟรมนี้
    private float runStaminaTimer;    // นับเวลาถึงการหัก Stamina ครั้งถัดไป

    // ตอนนี้ผู้เล่นเดินได้หรือไม่ (ปิดไว้ตอนเปิดกระเป๋า/เมนู)
    public bool CanMove { get; private set; } = true;

    // โหมดวิ่งเปิดอยู่หรือไม่ (กด Shift สลับ)
    public bool IsRunMode { get; private set; }

    // กำลังวิ่งจริงอยู่หรือไม่ (โหมดวิ่ง + กำลังขยับตัว) ใช้กับแอนิเมชันในอนาคต
    public bool IsRunning => IsRunMode && CanMove && moveInput.sqrMagnitude > 0.01f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }

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

        // สลับเดิน/วิ่ง: Shift ซ้ายหรือขวา / กดจอยสติ๊กซ้าย
        runToggleAction = new InputAction("RunToggle", InputActionType.Button);
        runToggleAction.AddBinding("<Keyboard>/leftShift");
        runToggleAction.AddBinding("<Keyboard>/rightShift");
        runToggleAction.AddBinding("<Gamepad>/leftStickPress");
    }

    private void OnEnable()
    {
        moveAction.Enable(); // เริ่มรับค่าปุ่มเมื่อวัตถุทำงาน
        runToggleAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable(); // หยุดรับค่าปุ่มเมื่อวัตถุถูกปิด
        runToggleAction.Disable();
    }

    private void Update()
    {
        // ถ้าห้ามเดินอยู่ ไม่ต้องอ่านปุ่ม ให้ทิศทางเป็นศูนย์
        // (หน้าต่างเลือกเมนูใช้ Shift เป็นปุ่ม ±5 จึงต้องไม่สลับวิ่งตอนนี้)
        if (!CanMove)
        {
            moveInput = Vector2.zero;
            return;
        }

        // กด Shift = สลับเดิน/วิ่ง
        if (runToggleAction.WasPressedThisFrame())
        {
            SetRunMode(!IsRunMode);
        }

        // อ่านค่าปุ่มทุกเฟรม และจำกัดความยาวไว้ไม่เกิน 1
        // เพื่อไม่ให้เดินเฉียงเร็วกว่าเดินตรง
        moveInput = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

        UpdateRunStamina();
    }

    // หัก Stamina ระหว่างวิ่ง ถ้าไม่พอจะกลับเป็นเดินเอง
    private void UpdateRunStamina()
    {
        if (!IsRunning || stats == null || runStaminaCost <= 0f)
        {
            return;
        }

        runStaminaTimer -= Time.deltaTime;
        if (runStaminaTimer > 0f)
        {
            return;
        }

        runStaminaTimer = runStaminaInterval;

        // ไม่พอ: กลับเป็นเดิน (PlayerStats แจ้งเตือน "เหนื่อย" ให้เอง)
        if (!stats.TrySpend(StatType.Stamina, runStaminaCost))
        {
            SetRunMode(false);
        }
    }

    // เปิด/ปิดโหมดวิ่ง
    public void SetRunMode(bool value)
    {
        if (IsRunMode == value)
        {
            return;
        }

        IsRunMode = value;

        // เริ่มวิ่งแล้วหักครั้งแรกทันทีที่ขยับ (กดสลับรัวๆ จะได้ไม่วิ่งฟรี)
        runStaminaTimer = 0f;

        Debug.Log($"Player {(IsRunMode ? "running" : "walking")}.");
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
        float speed = IsRunning ? moveSpeed * runSpeedMultiplier : moveSpeed;
        rb.linearVelocity = moveInput * speed;
    }
}
