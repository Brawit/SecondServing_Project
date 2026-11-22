using System.Collections.Generic;
using TMPro;
using UnityEngine;

// แถบ Hotbar ด้านล่างจอ: แสดงช่อง 10 ช่องตามข้อมูลใน Hotbar ของผู้เล่น
// - อัปเดตเมื่อช่องเปลี่ยน เลือกช่องใหม่ หรือจำนวนของในกระเป๋าเปลี่ยน
// - จางลงตอนเปิดร้าน (ล็อก ใช้ของไม่ได้)
// - แสดงชื่อไอเทมที่เลือกอยู่เหนือแถบ
// - ลาก/วางของ จัดการที่ HotbarSlotUI แต่ละช่อง
// สร้างให้อัตโนมัติได้จากเมนู Tools > Second Serving > Build Hotbar HUD
[RequireComponent(typeof(CanvasGroup))]
public class HotbarUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Hotbar ของผู้เล่น (เว้นว่างไว้ จะหาในฉากให้เอง)")]
    [SerializeField] private Hotbar hotbar;

    [Tooltip("ช่องทั้ง 10 ช่อง เรียงจากซ้ายไปขวา")]
    [SerializeField] private List<HotbarSlotUI> slots = new List<HotbarSlotUI>();

    [Tooltip("ข้อความชื่อไอเทมที่เลือก (เว้นว่างได้)")]
    [SerializeField] private TMP_Text selectedNameText;

    [Header("Lock")]
    [Tooltip("[2.5] ซ่อนแถบทั้งหมดตอนเปิดร้าน (ไม่ติ๊ก = แค่จางตาม Locked Alpha)")]
    [SerializeField] private bool hideWhileServing = true;

    [Tooltip("ความโปร่งของแถบตอนล็อก (เปิดร้าน) ใช้เมื่อไม่ได้ติ๊ก Hide While Serving")]
    [Range(0f, 1f)]
    [SerializeField] private float lockedAlpha = 0.45f;

    private CanvasGroup canvasGroup;
    private Inventory inventory;
    private bool wasLocked;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (hotbar == null)
        {
            hotbar = FindFirstObjectByType<Hotbar>();
        }

        if (hotbar == null)
        {
            Debug.LogWarning("HotbarUI: No Hotbar found in the scene.", this);
            return;
        }

        inventory = hotbar.GetComponent<Inventory>();

        if (slots.Count != Hotbar.SlotCount)
        {
            Debug.LogWarning($"HotbarUI: Expected {Hotbar.SlotCount} slots but has {slots.Count}.", this);
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;
            slots[i].Setup(i, hotbar);
            slots[i].OnClicked += HandleSlotClicked;
        }
    }

    private void OnEnable()
    {
        if (hotbar != null)
        {
            hotbar.OnSlotsChanged += Refresh;
            hotbar.OnSelectionChanged += HandleSelectionChanged;
        }

        if (inventory != null)
        {
            inventory.OnChanged += Refresh;
        }

        Refresh();
        ApplyLock(IsLocked());
    }

    private void OnDisable()
    {
        if (hotbar != null)
        {
            hotbar.OnSlotsChanged -= Refresh;
            hotbar.OnSelectionChanged -= HandleSelectionChanged;
        }

        if (inventory != null)
        {
            inventory.OnChanged -= Refresh;
        }
    }

    private void OnDestroy()
    {
        foreach (HotbarSlotUI slot in slots)
        {
            if (slot != null)
            {
                slot.OnClicked -= HandleSlotClicked;
            }
        }
    }

    private void Update()
    {
        // เช็กสถานะร้านทุกเฟรม (เปลี่ยนสีเฉพาะตอนสถานะเปลี่ยน)
        bool locked = IsLocked();
        if (locked != wasLocked)
        {
            ApplyLock(locked);
        }
    }

    // วาดทุกช่องใหม่
    public void Refresh()
    {
        if (hotbar == null)
        {
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;
            slots[i].SetItem(hotbar.GetSlot(i), hotbar.GetCount(i));
            slots[i].SetSelected(i == hotbar.SelectedIndex);
        }

        UpdateSelectedName();
    }

    private void HandleSelectionChanged(int index)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
            {
                slots[i].SetSelected(i == index);
            }
        }

        UpdateSelectedName();
    }

    private void HandleSlotClicked(HotbarSlotUI slot)
    {
        if (hotbar != null)
        {
            hotbar.Select(slot.Index);
        }
    }

    private void UpdateSelectedName()
    {
        if (selectedNameText == null || hotbar == null)
        {
            return;
        }

        ItemData item = hotbar.SelectedItem;
        selectedNameText.text = item != null ? item.itemName : "";
    }

    private static bool IsLocked()
    {
        return ShopManager.Instance != null && ShopManager.Instance.IsServing;
    }

    private void ApplyLock(bool locked)
    {
        wasLocked = locked;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = locked ? (hideWhileServing ? 0f : lockedAlpha) : 1f;

            // ซ่อนอยู่ = คลิก/ลากไม่ได้
            bool hidden = locked && hideWhileServing;
            canvasGroup.blocksRaycasts = !hidden;
            canvasGroup.interactable = !hidden;
        }
    }
}
