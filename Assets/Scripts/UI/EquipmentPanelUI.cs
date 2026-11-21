using System;
using UnityEngine;

// แผงช่องสวมใส่ในหน้าต่างกระเป๋า (แสดงเฉพาะแท็บ Equipment / Accessory ซึ่ง InventoryUI เป็นคนเปิด/ปิด)
// แถวบน = ช่องเพิ่มค่าสถานะ (Shoes, Ring, Ring, Necklace) / แถวล่าง = ช่องแฟชัน (Head, Top, Bottom, Back)
// สร้างและผูกอัตโนมัติได้จากเมนู Tools > Second Serving > Build Equipment Panel
public class EquipmentPanelUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ของที่ผู้เล่นสวมอยู่ (คอมโพเนนต์ PlayerEquipment บนตัว Player)")]
    [SerializeField] private PlayerEquipment equipment;

    [Tooltip("ช่อง 8 ช่อง เรียงตามลำดับ EquipSlot: Shoes, Ring1, Ring2, Necklace, Head, Top, Bottom, Back")]
    [SerializeField] private EquipmentSlotUI[] slotUIs = new EquipmentSlotUI[PlayerEquipment.SlotCount];

    // ส่งสัญญาณเมื่อคลิกช่อง (ส่งช่องที่คลิก ไอเทมในช่องอาจเป็น null ถ้าช่องว่าง)
    public event Action<EquipmentSlotUI> OnSlotClicked;

    public PlayerEquipment Equipment => equipment;

    private void Awake()
    {
        if (equipment == null)
        {
            Debug.LogWarning("EquipmentPanelUI: Player Equipment is not assigned.", this);
        }

        for (int i = 0; i < slotUIs.Length; i++)
        {
            if (slotUIs[i] == null)
            {
                Debug.LogWarning($"EquipmentPanelUI: Slot {i} ({(EquipSlot)i}) is not assigned.", this);
                continue;
            }

            slotUIs[i].Setup((EquipSlot)i, equipment);
            slotUIs[i].OnClicked += HandleSlotClicked;
        }
    }

    private void OnEnable()
    {
        if (equipment != null)
        {
            equipment.OnChanged += Refresh;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (equipment != null)
        {
            equipment.OnChanged -= Refresh;
        }
    }

    private void OnDestroy()
    {
        foreach (EquipmentSlotUI slot in slotUIs)
        {
            if (slot != null)
            {
                slot.OnClicked -= HandleSlotClicked;
            }
        }
    }

    // วาดทุกช่องใหม่
    public void Refresh()
    {
        for (int i = 0; i < slotUIs.Length; i++)
        {
            if (slotUIs[i] != null)
            {
                slotUIs[i].SetItem(equipment != null ? equipment.GetSlot((EquipSlot)i) : null);
            }
        }
    }

    // ให้กรอบเลือกอยู่ที่ช่องนี้ช่องเดียว (null = ไม่เลือกช่องไหน)
    public void SetSelectedSlot(EquipmentSlotUI selected)
    {
        foreach (EquipmentSlotUI slot in slotUIs)
        {
            if (slot != null)
            {
                slot.SetSelected(slot == selected);
            }
        }
    }

    private void HandleSlotClicked(EquipmentSlotUI slot)
    {
        OnSlotClicked?.Invoke(slot);
    }
}
