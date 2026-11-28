using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// [2.5] จานที่ผู้เล่นถืออยู่ในมือ (ถือได้ 1 จาน เฉพาะตอนเปิดร้าน)
// - ทำเสร็จที่เตาตอนเปิดร้าน = จานอยู่ในมือ แสดงชื่อเหนือหัว เช่น "Som Tam (Good)"
// - กด E ที่ลูกค้าที่สั่งจานนี้ = เสิร์ฟจานในมือ (Customer เรียก TakeForServe)
// - กด Q = เก็บเข้ากระเป๋า (คุณภาพเดิม) / ร้านปิดตอนยังถือ = เข้ากระเป๋าเอง
// - [v2] ถือเป็นไอเทมจาน (จานแม่ หรือจานแบบ เช่น "Sushi (Salmon)") ไม่ใช่สูตร → เก็บเข้ากระเป๋าได้แบบที่ถูกต้อง
// วางบน Player (ตัวช่วย Build Cooking System ใส่ให้) ข้อความเหนือหัวสร้างเองถ้าไม่ได้ผูก
public class CarriedDish : MonoBehaviour
{
    public static CarriedDish Instance { get; private set; }

    [Header("References")]
    [Tooltip("กระเป๋าของผู้เล่น (เว้นว่าง = หาบน GameObject เดียวกัน)")]
    [SerializeField] private Inventory inventory;

    [Tooltip("ข้อความเหนือหัว (TextMeshPro แบบ 3D) เว้นว่าง = สร้างให้เอง")]
    [SerializeField] private TMP_Text headText;

    [Header("Head text")]
    [Tooltip("ตำแหน่งข้อความเทียบกับตัวผู้เล่น")]
    [SerializeField] private Vector3 headTextOffset = new Vector3(0f, 0.95f, 0f);

    [Tooltip("ข้อความเหนือหัว ({0} = ชื่อจาน, {1} = คุณภาพ)")]
    [SerializeField] private string headFormat = "{0} ({1})\n<size=70%>[Q] stash</size>";

    [Header("Messages")]
    [Tooltip("เก็บเข้ากระเป๋าแล้ว ({0} = ชื่อจาน)")]
    [SerializeField] private string stashedFormat = "Put {0} in the bag.";

    [Tooltip("กระเป๋าเต็ม เก็บไม่ได้ ({0} = ชื่อจาน)")]
    [SerializeField] private string bagFullFormat = "Bag is full. Can't stash {0}.";

    [Tooltip("ร้านปิดแล้วกระเป๋าเต็ม จานหายไป ({0} = ชื่อจาน)")]
    [SerializeField] private string droppedFormat = "Bag is full. {0} was dropped.";

    // จานที่ถืออยู่ (null = มือว่าง) [v2] เป็นไอเทมจาน
    public ItemData Item { get; private set; }
    public DishQuality Quality { get; private set; } = DishQuality.Good;
    public bool HasDish => Item != null;

    // สูตรแม่ของจานในมือ (null = มือว่าง)
    public RecipeData Recipe => DishVariantRegistry.RecipeOf(Item);

    // ส่งสัญญาณเมื่อถือ/วางจาน
    public event Action OnChanged;

    private bool isSubscribed;
    private PlayerController playerController;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("CarriedDish: Duplicate instance found, destroying this one.");
            Destroy(this);
            return;
        }
        Instance = this;

        if (inventory == null) inventory = GetComponent<Inventory>();
        playerController = GetComponent<PlayerController>();
        if (inventory == null) Debug.LogWarning("CarriedDish: Inventory is not assigned.", this);

        EnsureHeadText();
        RefreshHeadText();
    }

    private void Start()
    {
        Subscribe();
    }

    private void OnEnable()
    {
        if (Instance == this && ShopManager.Instance != null) Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Subscribe()
    {
        if (isSubscribed || ShopManager.Instance == null) return;
        ShopManager.Instance.OnShopClosed += HandleShopClosed;
        if (GameClock.Instance != null) GameClock.Instance.OnDayStarted += HandleDayStarted;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed) return;
        if (ShopManager.Instance != null) ShopManager.Instance.OnShopClosed -= HandleShopClosed;
        if (GameClock.Instance != null) GameClock.Instance.OnDayStarted -= HandleDayStarted;
        isSubscribed = false;
    }

    private void Update()
    {
        if (!HasDish)
        {
            return;
        }

        // กด Q = เก็บจานเข้ากระเป๋า (ระหว่างมินิเกม/เปิดหน้าต่าง ไม่รับ)
        bool canAct = (playerController == null || playerController.CanMove) && !CookingSession.IsBusy;
        Keyboard keyboard = Keyboard.current;
        if (canAct && keyboard != null && keyboard.qKey.wasPressedThisFrame)
        {
            Stash(false);
        }
    }

    // ---------- ถือ / ปล่อย ----------

    // ถือจาน คืนค่า false ถ้าถืออยู่แล้ว [v2] dish = ไอเทมจาน (จานแม่ หรือจานแบบ)
    public bool Hold(ItemData dish, DishQuality quality)
    {
        if (dish == null || HasDish)
        {
            return false;
        }

        Item = dish;
        Quality = quality;
        Debug.Log($"CarriedDish: Holding {dish.itemName} ({DishQualityUtil.Label(quality)}).");
        Changed();
        return true;
    }

    // ถือจานของสูตรนี้อยู่ไหม (แบบไหนก็ได้)
    public bool IsHolding(RecipeData recipe)
    {
        return recipe != null && HasDish && Recipe == recipe;
    }

    // เอาจานออกจากมือเพื่อเสิร์ฟ (Customer เรียกหลังเสิร์ฟสำเร็จ)
    public void Clear()
    {
        if (!HasDish) return;
        Item = null;
        Changed();
    }

    // เก็บจานในมือเข้ากระเป๋า (คุณภาพเดิม) force = ร้านปิด (กระเป๋าเต็ม = จานหาย) คืนค่า true ถ้าเก็บได้
    public bool Stash(bool force)
    {
        if (!HasDish)
        {
            return false;
        }

        ItemData dish = Item;
        bool added = inventory != null && inventory.AddItem(dish, 1, Quality);

        if (!added && !force)
        {
            NotificationUI.Post(string.Format(bagFullFormat, dish.itemName));
            return false;
        }

        if (!added)
        {
            Debug.LogWarning($"CarriedDish: Could not stash {dish.itemName} (bag full). Dropped.");
            NotificationUI.Post(string.Format(droppedFormat, dish.itemName));
        }
        else if (!force)
        {
            NotificationUI.Post(string.Format(stashedFormat, dish.itemName));
        }

        Debug.Log($"CarriedDish: Stashed {dish.itemName} ({dish.itemId}, {DishQualityUtil.Label(Quality)}) into the bag: {added}.");
        Item = null;
        Changed();
        return added;
    }

    // ---------- เหตุการณ์ ----------

    private void HandleShopClosed()
    {
        Stash(true);
    }

    private void HandleDayStarted(int day)
    {
        // กันค้าง (ปกติร้านปิดแล้วจานเข้ากระเป๋าไปแล้ว)
        Stash(true);
    }

    private void Changed()
    {
        RefreshHeadText();
        OnChanged?.Invoke();
    }

    // ---------- ข้อความเหนือหัว ----------

    private void EnsureHeadText()
    {
        if (headText != null)
        {
            return;
        }

        GameObject go = new GameObject("CarriedDishText");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = headTextOffset;

        TextMeshPro text = go.AddComponent<TextMeshPro>();
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 3f;
        text.alignment = TextAlignmentOptions.Bottom;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(4f, 1.2f);
        text.sortingOrder = 31;
        headText = text;
    }

    private void RefreshHeadText()
    {
        if (headText == null)
        {
            return;
        }

        headText.gameObject.SetActive(HasDish);
        if (HasDish)
        {
            headText.text = string.Format(headFormat, Item.itemName, DishQualityUtil.ColoredLabel(Quality));
        }
    }
}
