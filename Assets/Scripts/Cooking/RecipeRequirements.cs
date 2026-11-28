using System.Collections.Generic;

// [v2] คำนวณวัตถุดิบที่สูตรต้องใช้ รวมช่องหมวด (any_fish, any_cheese ฯลฯ)
// - สูตร v2 มีช่องหมวดได้ไม่เกิน 1 ช่อง ผู้เล่นเลือกวัตถุดิบในหมวดเอง (choice)
// - ไม่มีช่องหมวด = choice ไม่มีผล
public static class RecipeRequirements
{
    // วัตถุดิบที่ต้องหักจริง (choice = วัตถุดิบที่ใส่ช่องหมวด null = ข้ามช่องหมวด)
    public static Dictionary<ItemData, int> Resolve(RecipeData recipe, ItemData choice)
    {
        Dictionary<ItemData, int> result = new Dictionary<ItemData, int>();
        if (recipe == null)
        {
            return result;
        }

        foreach (RecipeSlot slot in recipe.Slots)
        {
            ItemData item = slot.anyOfCategory ? (slot.Accepts(choice) ? choice : null) : slot.item;
            if (item == null)
            {
                continue;
            }

            result.TryGetValue(item, out int current);
            result[item] = current + 1;
        }

        return result;
    }

    // จำนวนชิ้นที่สูตรใช้ของวัตถุดิบนี้ในช่องตายตัว (ไม่นับช่องหมวด)
    private static int FixedNeed(RecipeData recipe, ItemData item)
    {
        int need = 0;
        foreach (RecipeSlot slot in recipe.Slots)
        {
            if (!slot.anyOfCategory && slot.item == item) need++;
        }
        return need;
    }

    // วัตถุดิบในหมวดที่มีพอทำได้อย่างน้อย 1 จาน เรียงจำนวนมากสุดก่อน แล้วตาม itemId
    public static List<ItemData> GetCandidates(RecipeData recipe, Inventory inventory)
    {
        List<ItemData> result = new List<ItemData>();
        if (recipe == null || inventory == null || !recipe.HasCategorySlot)
        {
            return result;
        }

        RecipeSlot slot = recipe.CategorySlot;
        foreach (KeyValuePair<ItemData, int> pair in inventory.Items)
        {
            ItemData item = pair.Key;
            if (!slot.Accepts(item))
            {
                continue;
            }

            // ถ้าวัตถุดิบนี้อยู่ในช่องตายตัวด้วย ต้องมีพอทั้งสองช่อง
            if (pair.Value >= FixedNeed(recipe, item) + 1)
            {
                result.Add(item);
            }
        }

        result.Sort((a, b) =>
        {
            int byCount = inventory.GetCount(b).CompareTo(inventory.GetCount(a));
            return byCount != 0 ? byCount : string.CompareOrdinal(a.itemId, b.itemId);
        });
        return result;
    }

    // ตัวเลือกตั้งต้น: ตัวที่เลือกล่าสุดถ้ายังพอ / ไม่งั้นตัวที่มีมากสุด / ไม่มีช่องหมวดหรือไม่มีของ = null
    public static ItemData DefaultChoice(RecipeData recipe, Inventory inventory, ItemData lastChoice)
    {
        List<ItemData> candidates = GetCandidates(recipe, inventory);
        if (candidates.Count == 0)
        {
            return null;
        }

        if (lastChoice != null && candidates.Contains(lastChoice))
        {
            return lastChoice;
        }

        return candidates[0];
    }

    // มีวัตถุดิบครบสำหรับ 1 จานไหม (สูตรที่มีช่องหมวดต้องมี choice)
    public static bool HasAll(RecipeData recipe, Inventory inventory, ItemData choice)
    {
        if (recipe == null || inventory == null)
        {
            return false;
        }

        if (recipe.HasCategorySlot && !recipe.CategorySlot.Accepts(choice))
        {
            return false;
        }

        foreach (KeyValuePair<ItemData, int> pair in Resolve(recipe, choice))
        {
            if (inventory.GetCount(pair.Key) < pair.Value)
            {
                return false;
            }
        }

        return true;
    }

    // จำนวนจานที่ทำได้จากของในกระเป๋า ลบของที่จองไว้แล้ว (reserved) ช่องหมวดนับรวมทุกตัวในหมวด
    // reserved ไม่ null = บวกของที่สูตรนี้ใช้ (servings จาน) เข้าไปด้วย ให้สูตรถัดไปในเมนูไม่นับซ้ำ
    public static int CountServings(RecipeData recipe, Inventory inventory, Dictionary<ItemData, int> reserved, int limit = int.MaxValue)
    {
        if (recipe == null || inventory == null)
        {
            return 0;
        }

        IReadOnlyList<RecipeSlot> slots = recipe.Slots;
        if (slots.Count == 0)
        {
            return 0;
        }

        // ของที่เหลือหลังหักที่จองไว้
        int Available(ItemData item)
        {
            int have = inventory.GetCount(item);
            if (reserved != null && reserved.TryGetValue(item, out int used)) have -= used;
            return have < 0 ? 0 : have;
        }

        Dictionary<ItemData, int> fixedNeed = Resolve(recipe, null);
        int servings = limit;
        foreach (KeyValuePair<ItemData, int> pair in fixedNeed)
        {
            int can = Available(pair.Key) / pair.Value;
            if (can < servings) servings = can;
        }

        // ช่องหมวด: รวมทุกตัวในหมวด (หักส่วนที่ช่องตายตัวใช้ไปแล้ว)
        if (recipe.HasCategorySlot)
        {
            RecipeSlot slot = recipe.CategorySlot;
            int pool = 0;
            foreach (KeyValuePair<ItemData, int> pair in inventory.Items)
            {
                if (!slot.Accepts(pair.Key)) continue;
                int left = Available(pair.Key);
                if (fixedNeed.TryGetValue(pair.Key, out int fixedUse)) left -= fixedUse * (servings == int.MaxValue ? 0 : servings);
                if (left > 0) pool += left;
            }
            if (pool < servings) servings = pool;
        }

        if (servings == int.MaxValue || servings < 0)
        {
            servings = 0;
        }

        // จองของที่ใช้ไว้ให้สูตรถัดไป
        if (reserved != null && servings > 0)
        {
            foreach (KeyValuePair<ItemData, int> pair in fixedNeed)
            {
                reserved.TryGetValue(pair.Key, out int used);
                reserved[pair.Key] = used + pair.Value * servings;
            }

            if (recipe.HasCategorySlot)
            {
                // หักจากตัวที่มีมากสุดก่อน (ประมาณการ ตอนทำจริงผู้เล่นเลือกเอง)
                int remaining = servings;
                RecipeSlot slot = recipe.CategorySlot;
                List<ItemData> pool = new List<ItemData>();
                foreach (ItemData item in inventory.Items.Keys)
                {
                    if (slot.Accepts(item)) pool.Add(item);
                }
                pool.Sort((a, b) => Available(b).CompareTo(Available(a)));
                foreach (ItemData item in pool)
                {
                    if (remaining <= 0) break;
                    int take = System.Math.Min(remaining, Available(item));
                    if (take <= 0) continue;
                    reserved.TryGetValue(item, out int used);
                    reserved[item] = used + take;
                    remaining -= take;
                }
            }
        }

        return servings;
    }
}
