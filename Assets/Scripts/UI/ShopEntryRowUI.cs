using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// [2.6b] แถวสินค้าหนึ่งแถวในหน้าต่างร้าน (ShopUI สร้างจากแถวต้นแบบ)
// ชื่อ / ราคา / จำนวนที่มี / ปุ่ม Buy 1 + Buy 5 (ไอเทม) หรือ Buy (กระเป๋า, สมุด) / ซื้อไม่ได้ = ปุ่มจาง + เหตุผล
// ค้าง Shift = ×10 (Buy 10 / Buy 50) / ที่ว่างหรือเงินไม่พอ = ได้เท่าที่ได้ (ShopPurchase จัดการ)
// [v2] กรอบไฮไลท์ตอนเมาส์วาง (HoverHighlight ใส่ด้วยโค้ด) เฉพาะแถวที่ซื้อได้ตอนนี้
public class ShopEntryRowUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text ownedText;
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private Button buyOneButton;
    [SerializeField] private TMP_Text buyOneLabel;
    [SerializeField] private Button buyFiveButton;
    [SerializeField] private TMP_Text buyFiveLabel;

    [Header("Text")]
    [Tooltip("{0} = จำนวน")]
    [SerializeField] private string buyItemFormat = "Buy {0}";
    [Min(1)] [SerializeField] private int smallAmount = 1;
    [Min(1)] [SerializeField] private int largeAmount = 5;
    [Tooltip("ค้าง Shift = คูณจำนวนเท่านี้")]
    [Min(1)] [SerializeField] private int shiftMultiplier = 10;
    [SerializeField] private string buySingleLabel = "Buy";
    [Tooltip("{0} = จำนวนที่มี")]
    [SerializeField] private string ownedFormat = "Have {0}";
    [Tooltip("{0} = ระดับปัจจุบัน, {1} = เพดานต่อชนิด")]
    [SerializeField] private string bagFormat = "Now: level {0} ({1} each)";
    [Tooltip("{0} = เพดานต่อชนิดหลังอัปเกรด")]
    [SerializeField] private string bagNextFormat = "Carry up to {0} of each item";
    [SerializeField] private string unknownRecipeText = "New recipe";
    [SerializeField] private Color problemColor = new Color(1f, 0.45f, 0.4f);
    [SerializeField] private Color learnedColor = new Color(0.55f, 0.9f, 0.55f);

    private ShopCatalog catalog;
    private ShopEntry entry;
    private Inventory inventory;
    private RecipeBook recipeBook;
    private Action<ShopEntry, int> onBuy;

    private bool shiftShown;

    // [v2] กรอบไฮไลท์ตอนเมาส์วาง (ใส่ด้วยโค้ด)
    private HoverHighlight hover;
    private HoverHighlight Hover => hover != null ? hover : (hover = HoverHighlight.Ensure(gameObject));

    public ShopEntry Entry => entry;

    private int Multiplier => IsShiftHeld() ? Mathf.Max(1, shiftMultiplier) : 1;

    private void Awake()
    {
        if (buyOneButton != null) buyOneButton.onClick.AddListener(() => onBuy?.Invoke(entry, smallAmount * Multiplier));
        if (buyFiveButton != null) buyFiveButton.onClick.AddListener(() => onBuy?.Invoke(entry, largeAmount * Multiplier));
    }

    // ค้าง/ปล่อย Shift = เปลี่ยนป้ายปุ่มทันที
    private void Update()
    {
        bool shift = IsShiftHeld();
        if (shift != shiftShown) Refresh();
    }

    private static bool IsShiftHeld()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.shiftKey.isPressed;
    }

    public void Bind(ShopCatalog shopCatalog, ShopEntry shopEntry, Inventory playerInventory, RecipeBook playerRecipeBook, Action<ShopEntry, int> buyCallback)
    {
        catalog = shopCatalog;
        entry = shopEntry;
        inventory = playerInventory;
        recipeBook = playerRecipeBook;
        onBuy = buyCallback;
        Refresh();
    }

    public void Refresh()
    {
        if (entry == null || catalog == null) return;

        bool isItem = entry.kind == ShopEntryKind.Item;

        if (nameText != null) nameText.text = entry.DisplayName;
        if (priceText != null) priceText.text = PlayerWallet.Format(catalog.GetPrice(entry));

        if (ownedText != null)
        {
            switch (entry.kind)
            {
                case ShopEntryKind.Item:
                    ownedText.text = string.Format(ownedFormat, inventory != null ? inventory.GetCount(entry.item) : 0);
                    break;
                case ShopEntryKind.BagUpgrade:
                    ownedText.text = inventory != null && inventory.CanUpgradeBag
                        ? string.Format(bagNextFormat, inventory.GetStackCapForLevel(entry.bagLevel))
                        : (inventory != null ? string.Format(bagFormat, inventory.BagLevel, inventory.StackCap) : "");
                    break;
                case ShopEntryKind.RecipeBook:
                    ownedText.text = recipeBook != null && entry.recipe != null && recipeBook.Knows(entry.recipe) ? "" : unknownRecipeText;
                    break;
            }
        }

        shiftShown = IsShiftHeld();
        int multiplier = Multiplier;

        // ซื้อได้อย่างน้อย 1 ชิ้น = กดได้ทั้งสองปุ่ม (ได้เท่าที่ได้)
        bool canOne = ShopPurchase.CanBuy(catalog, entry, 1, inventory, recipeBook, out string reason);

        if (buyOneButton != null) buyOneButton.interactable = canOne;

        // [v2] กรอบไฮไลท์: แถวที่ซื้อได้ = ขึ้น / ซื้อไม่ได้ (เงินไม่พอ กระเป๋าเต็ม รู้สูตรแล้ว) = ไม่ขึ้น
        Hover.Interactable = canOne;
        if (buyOneLabel != null) buyOneLabel.text = isItem ? string.Format(buyItemFormat, smallAmount * multiplier) : buySingleLabel;
        if (buyFiveButton != null)
        {
            buyFiveButton.gameObject.SetActive(isItem);
            buyFiveButton.interactable = canOne;
        }
        if (buyFiveLabel != null) buyFiveLabel.text = string.Format(buyItemFormat, largeAmount * multiplier);

        if (reasonText != null)
        {
            reasonText.text = canOne ? "" : reason;
            reasonText.color = reason == ShopPurchase.ReasonLearned ? learnedColor : problemColor;
        }
    }
}
