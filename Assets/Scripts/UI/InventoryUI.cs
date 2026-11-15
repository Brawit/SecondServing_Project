using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// หน้าต่างกระเป๋า: กด I เปิด/ปิด และวาดช่องไอเทมใหม่ทุกครั้งที่กระเป๋าเปลี่ยน
// หมายเหตุ: ต้องวางสคริปต์นี้บนวัตถุที่เปิดอยู่ตลอด (ไม่ใช่ตัวหน้าต่างที่ถูกซ่อน)
// เพราะถ้าวัตถุถูกปิด Update จะไม่ทำงาน และจะกด I เปิดกลับไม่ได้
public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("กระเป๋าของผู้เล่น (ลาก Player มาใส่)")]
    [SerializeField] private Inventory inventory;

    [Tooltip("ตัวหน้าต่างที่จะซ่อน/แสดง")]
    [SerializeField] private GameObject windowRoot;

    [Tooltip("วัตถุที่มี Grid Layout Group ไว้เป็นที่วางช่องไอเทม")]
    [SerializeField] private Transform slotContainer;

    [Tooltip("Prefab ของช่องไอเทม 1 ช่อง")]
    [SerializeField] private InventorySlotUI slotPrefab;

    [Tooltip("ตัวควบคุมการเดินของผู้เล่น (ลาก Player มาใส่) ใช้หยุดเดินตอนเปิดกระเป๋า")]
    [SerializeField] private PlayerController playerController;

    [Header("Settings")]
    [Tooltip("แสดงช่องว่างให้ครบตามจำนวนช่องสูงสุดของกระเป๋า")]
    [SerializeField] private bool showEmptySlots = true;

    [Tooltip("หยุดนาฬิกาในเกมระหว่างเปิดกระเป๋า")]
    [SerializeField] private bool pauseClockWhileOpen = true;

    [Tooltip("ห้ามผู้เล่นเดินระหว่างเปิดกระเป๋า")]
    [SerializeField] private bool blockMovementWhileOpen = true;

    // หน้าต่างเปิดอยู่หรือไม่
    public bool IsOpen { get; private set; }

    // ช่องที่สร้างไว้แล้ว นำกลับมาใช้ซ้ำ ไม่ต้องลบแล้วสร้างใหม่ทุกครั้ง
    private readonly List<InventorySlotUI> slots = new List<InventorySlotUI>();

    // จำไว้ว่าหน้าต่างนี้เป็นคนหยุดนาฬิกาเอง จะได้ไม่ไปเดินนาฬิกาที่ระบบอื่นหยุดไว้
    private bool pausedClockByThis = false;

    private void Awake()
    {
        // เตือนถ้าลืมลากของใส่ช่องใน Inspector
        if (inventory == null) Debug.LogWarning("InventoryUI: Inventory is not assigned.", this);
        if (windowRoot == null) Debug.LogWarning("InventoryUI: Window Root is not assigned.", this);
        if (slotContainer == null) Debug.LogWarning("InventoryUI: Slot Container is not assigned.", this);
        if (slotPrefab == null) Debug.LogWarning("InventoryUI: Slot Prefab is not assigned.", this);
        if (blockMovementWhileOpen && playerController == null) Debug.LogWarning("InventoryUI: Player Controller is not assigned.", this);

        // ป้องกันการวางสคริปต์ผิดที่ (บนตัวหน้าต่างเอง)
        if (windowRoot == gameObject)
        {
            Debug.LogWarning("InventoryUI: Do not put this script on the Window Root itself. Put it on its parent.", this);
        }
    }

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.OnChanged += HandleInventoryChanged;
        }
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnChanged -= HandleInventoryChanged;
        }
    }

    private void Start()
    {
        // เริ่มเกมโดยปิดหน้าต่างไว้
        SetWindowVisible(false);
        IsOpen = false;
        Refresh();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        // I = เปิด/ปิดกระเป๋า
        if (keyboard.iKey.wasPressedThisFrame)
        {
            Toggle();
        }

        // Esc = ปิดกระเป๋า (ถ้าเปิดอยู่)
        if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    // ---------- เปิด / ปิด ----------

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (IsOpen) return;

        IsOpen = true;
        Refresh();
        SetWindowVisible(true);

        // หยุดนาฬิกา เฉพาะกรณีที่นาฬิกายังเดินอยู่
        if (pauseClockWhileOpen && GameClock.Instance != null && !GameClock.Instance.IsPaused)
        {
            GameClock.Instance.Pause();
            pausedClockByThis = true;
        }

        // ห้ามเดินระหว่างเปิดกระเป๋า
        if (blockMovementWhileOpen && playerController != null)
        {
            playerController.SetCanMove(false);
        }

        Debug.Log("Inventory opened.");
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        SetWindowVisible(false);

        // ให้นาฬิกาเดินต่อ เฉพาะกรณีที่หน้าต่างนี้เป็นคนหยุดไว้
        if (pausedClockByThis && GameClock.Instance != null)
        {
            GameClock.Instance.Resume();
        }
        pausedClockByThis = false;

        // ให้เดินได้อีกครั้ง
        if (blockMovementWhileOpen && playerController != null)
        {
            playerController.SetCanMove(true);
        }

        Debug.Log("Inventory closed.");
    }

    private void SetWindowVisible(bool visible)
    {
        if (windowRoot != null)
        {
            windowRoot.SetActive(visible);
        }
    }

    // ---------- การวาดช่องไอเทม ----------

    private void HandleInventoryChanged()
    {
        Refresh();
    }

    // อัปเดตทุกช่องให้ตรงกับของในกระเป๋า
    public void Refresh()
    {
        if (inventory == null || slotContainer == null || slotPrefab == null)
        {
            return;
        }

        // จำนวนช่องที่ต้องแสดง
        int itemCount = inventory.Items.Count;
        int slotsNeeded = showEmptySlots ? Mathf.Max(inventory.MaxSlots, itemCount) : itemCount;

        EnsureSlotCount(slotsNeeded);

        // ใส่ไอเทมลงช่องตามลำดับ
        int index = 0;
        foreach (KeyValuePair<ItemData, int> pair in inventory.Items)
        {
            slots[index].gameObject.SetActive(true);
            slots[index].SetItem(pair.Key, pair.Value);
            index++;
        }

        // ช่องที่เหลือ: แสดงเป็นช่องว่าง หรือซ่อนไป
        for (int i = index; i < slots.Count; i++)
        {
            if (i < slotsNeeded)
            {
                slots[i].gameObject.SetActive(true);
                slots[i].SetEmpty();
            }
            else
            {
                slots[i].gameObject.SetActive(false);
            }
        }
    }

    // สร้างช่องเพิ่มจนมีครบตามจำนวนที่ต้องการ
    private void EnsureSlotCount(int count)
    {
        while (slots.Count < count)
        {
            InventorySlotUI newSlot = Instantiate(slotPrefab, slotContainer);
            newSlot.name = $"Slot_{slots.Count}";
            slots.Add(newSlot);
        }
    }
}
