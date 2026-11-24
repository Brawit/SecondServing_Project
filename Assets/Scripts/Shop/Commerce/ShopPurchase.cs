using UnityEngine;

// [2.6b] กติกาการซื้อของจากร้าน (ใช้ร่วมทุกร้าน)
// - ไอเทม: ซื้อได้ไม่เกินที่ว่างในกระเป๋าและเงินที่มี (ขอ 5 ที่ว่างเหลือ 4 = ได้ 4 จ่าย 4 / ผู้ใช้ขอ ไม่เอาเปรียบผู้เล่น)
// - อัปเกรดกระเป๋า: แสดงเฉพาะขั้นถัดไป / ระดับสูงสุดแล้ว = แถวเดียว "Max level"
// - สมุดสูตร: รู้สูตรแล้ว = "Learned" ซื้อไม่ได้ / ซื้อ = RecipeBook.Learn ทันที (ไม่เป็นไอเทม)
// - ตรวจซ้ำก่อนหักเงินทุกครั้ง
public static class ShopPurchase
{
    public const string ReasonNoMoney = "Not enough money";
    public const string ReasonBagFullFormat = "Bag full ({0})";
    public const string ReasonLearned = "Learned";
    public const string ReasonMaxLevel = "Max level";
    public const string ReasonUnavailable = "Unavailable";
    public const string LimitBagFull = "bag full";
    public const string LimitNoMoney = "not enough money";

    // ร้าน/กล่องใช้ไม่ได้ตอนนี้ไหม: เปิดร้านอาหาร (รวม Last call), หน้าสรุปวัน, หรือมีหน้าต่างอื่นเปิดอยู่
    public static bool IsCommerceBlocked()
    {
        if (ShopManager.Instance != null && ShopManager.Instance.IsServing) return true;
        if (DayEndManager.Instance != null && DayEndManager.Instance.IsDayEnding) return true;
        return IsAnyOtherWindowOpen();
    }

    // หน้าต่างที่ห้ามเปิดซ้อน (รวมหน้าต่างร้าน/กล่องเอง)
    public static bool IsAnyOtherWindowOpen()
    {
        return ShopUI.IsAnyOpen || ShippingBoxUI.IsAnyOpen || ConstructionUI.IsAnyOpen || MailUI.IsAnyOpen ||
               CookingSession.IsBusy || MenuSelectionUI.IsAnyOpen || CookingMenuUI.IsAnyOpen;
    }

    // แถวนี้ควรแสดงในร้านไหม
    public static bool IsVisible(ShopEntry entry, Inventory inventory)
    {
        if (entry == null) return false;
        if (entry.kind != ShopEntryKind.BagUpgrade) return true;
        if (inventory == null) return true;

        // ขั้นถัดไปเท่านั้น / ถ้าเต็มระดับแล้ว แสดงแถวของระดับสูงสุดไว้บอกว่า Max level
        if (entry.bagLevel > inventory.MaxBagLevel) return false;
        if (inventory.CanUpgradeBag) return entry.bagLevel == inventory.BagLevel + 1;
        return entry.bagLevel == inventory.MaxBagLevel;
    }

    // ซื้อได้จริงกี่ชิ้นจากที่ขอ (ไอเทม = ไม่เกินที่ว่างในกระเป๋าและเงินที่มี / กระเป๋า, สมุด = 1)
    // 0 = ซื้อไม่ได้ (reason = เหตุผล) / ได้น้อยกว่าที่ขอ = limitNote บอกว่าเพราะอะไร (bag full / not enough money)
    public static int GetBuyableAmount(ShopCatalog catalog, ShopEntry entry, int requested, Inventory inventory, RecipeBook recipeBook,
                                       out string reason, out string limitNote)
    {
        reason = ReasonUnavailable;
        limitNote = string.Empty;
        if (catalog == null || entry == null || requested <= 0) return 0;

        int amount;
        switch (entry.kind)
        {
            case ShopEntryKind.Item:
            {
                if (entry.item == null || inventory == null) return 0;
                int space = inventory.GetSpaceFor(entry.item);
                if (space <= 0)
                {
                    reason = string.Format(ReasonBagFullFormat, inventory.StackCap);
                    return 0;
                }
                amount = requested;
                if (space < amount)
                {
                    amount = space;
                    limitNote = LimitBagFull;
                }
                break;
            }

            case ShopEntryKind.BagUpgrade:
                if (inventory == null) return 0;
                if (!inventory.CanUpgradeBag)
                {
                    reason = ReasonMaxLevel;
                    return 0;
                }
                if (entry.bagLevel != inventory.BagLevel + 1) return 0;
                amount = 1;
                break;

            case ShopEntryKind.RecipeBook:
                if (entry.recipe == null || recipeBook == null) return 0;
                if (recipeBook.Knows(entry.recipe))
                {
                    reason = ReasonLearned;
                    return 0;
                }
                amount = 1;
                break;

            default:
                return 0;
        }

        int unit = catalog.GetPrice(entry);
        if (unit <= 0) return 0;

        // เงินไม่พอทั้งหมด = ซื้อเท่าที่จ่ายไหว (เฉพาะไอเทม)
        int money = PlayerWallet.Instance != null ? PlayerWallet.Instance.Money : 0;
        int affordable = money / unit;
        if (affordable <= 0)
        {
            reason = ReasonNoMoney;
            return 0;
        }
        if (affordable < amount)
        {
            amount = affordable;
            limitNote = LimitNoMoney;
        }

        reason = string.Empty;
        return amount;
    }

    // ซื้อได้อย่างน้อย 1 ชิ้นไหม
    public static bool CanBuy(ShopCatalog catalog, ShopEntry entry, int amount, Inventory inventory, RecipeBook recipeBook, out string reason)
    {
        return GetBuyableAmount(catalog, entry, amount, inventory, recipeBook, out reason, out _) > 0;
    }

    // ซื้อจริง (ตรวจซ้ำ → หักเงินเฉพาะจำนวนที่ได้ → ให้ของ) / bought = จำนวนที่ได้จริง (อาจน้อยกว่าที่ขอ ไม่เคยเกินที่ว่าง)
    public static bool TryBuy(ShopCatalog catalog, ShopEntry entry, int requested, Inventory inventory, RecipeBook recipeBook,
                              out int bought, out string reason, out string limitNote)
    {
        bought = GetBuyableAmount(catalog, entry, requested, inventory, recipeBook, out reason, out limitNote);
        if (bought <= 0) return false;

        int total = catalog.GetPrice(entry) * bought;
        if (!PlayerWallet.Instance.TrySpend(total))
        {
            bought = 0;
            reason = ReasonNoMoney;
            return false;
        }

        bool ok;
        switch (entry.kind)
        {
            case ShopEntryKind.Item:
                ok = inventory.AddItem(entry.item, bought);
                break;
            case ShopEntryKind.BagUpgrade:
                ok = inventory.UpgradeBag();
                break;
            case ShopEntryKind.RecipeBook:
                ok = recipeBook.Learn(entry.recipe);
                break;
            default:
                ok = false;
                break;
        }

        if (!ok)
        {
            // ไม่ควรเกิด (ตรวจไว้แล้ว) แต่กันเงินหาย
            PlayerWallet.Instance.Add(total);
            Debug.LogWarning($"ShopPurchase: Failed to give {entry.DisplayName}. Money refunded.");
            bought = 0;
            reason = ReasonUnavailable;
            return false;
        }

        Debug.Log($"ShopPurchase: Bought {entry.DisplayName} x{bought} (asked {requested}) for {total} at {catalog.shopName}.");
        reason = string.Empty;
        return true;
    }
}
