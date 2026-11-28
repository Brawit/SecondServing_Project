# Recipes v2 งาน 1: ข้อมูล + ช่องหมวด + จานแยกแบบ + ราคา — แผนลงมือ

> **สถานะ 27 พ.ย.:** 1A ✅ 1B ✅ 1C ✅ (ผู้ใช้ทดสอบผ่าน) / 1D ✅ (28 พ.ย. ผู้ใช้ทดสอบผ่าน + ส่วนเพิ่มดู HANDOFF หัวข้อ 10) / 1E ✅ (28 พ.ย. ผู้ใช้ทดสอบผ่าน) Task 13 Step 1 เซฟ v8 = ข้าม (เซฟถูกทับเป็น v9 แล้ว) → **งาน 1 จบ** / **Task 7 เปลี่ยนจากแผน:** เตาในร้านเป็นแถบ 2 ชั้นให้ผู้เล่นเลือกวัตถุดิบ (ดู HANDOFF หัวข้อ 10)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** เกมใช้ข้อมูลชีต v2 ได้จริง: ช่องหมวด any_* ที่ผู้เล่นเลือกวัตถุดิบเอง, จานแยกแบบตามวัตถุดิบ (สร้างตอนรัน), ราคา/ค่าพลังตามวัตถุดิบจริง, ตัวคูณดาว × คุณภาพ (ราคาและค่าพลัง), UI รวมกลุ่มที่กางให้เลือกได้ทุกจุด, กรอบไฮไลท์ตอนวางเมาส์

**Architecture:** ตรรกะคำนวณอยู่ใน static class ล้วน (`RecipeRequirements`, `DishPricing`, `DishVariantRegistry`) ระบบเดิม (Inventory/Save/Ledger/Customer) ยังมองจานแบบเป็น ItemData ธรรมดา — เปลี่ยนเฉพาะจุดที่เคยถือ "สูตร" แทน "ไอเทม" (CarriedDish, DailyMenu serving, Customer จ่ายเงิน) และ UI ที่รวมกลุ่ม

**Tech Stack:** Unity 6.3 LTS, C#, uGUI + TextMeshPro, ScriptableObject, Claude Docs connector (อ่านชีต)

**Spec:** `Docs/recipes-v2-code-design.md` (หัวข้อ 2, 4) + `Docs/recipes-v2-design.md` (ค่าตัวเลข)

## Global Constraints

- ไฟล์ .cs: UTF-8 with BOM + CRLF / ชื่อคลาส ตัวแปร [Header] Debug.Log เป็นอังกฤษ / คอมเมนต์อธิบายเป็นไทย (ผู้ใช้)
- ส่งโค้ดเป็นไฟล์เต็ม ไม่ตัดทอน / บอกรายชื่อไฟล์ที่แก้ทุกครั้ง (ผู้ใช้ reload ใน Visual Studio)
- enum เพิ่มต่อท้ายเท่านั้น ห้ามสลับลำดับ
- ตัว import ไม่ลบ asset ใดๆ / ไม่ทับ icon, description, cookingProfile, customSteps
- ห้ามคำนวณทับราคา/ค่าพลังที่อยู่ในชีต (จานไม่มีช่องหมวดใช้ค่าชีตตรงๆ)
- เซฟ v1–v8 ต้องโหลดได้ / SaveData.CurrentVersion = 9
- ไม่สร้างไฟล์ไอเทมจานแบบใน Assets (สร้างตอนรันเท่านั้น)
- ค่าตัวเลขทั้งหมด (ตัวคูณ 2.4, 1.3, 2.0, ตาราง ★×คุณภาพ, 1.15/1.30, เพดาน) อยู่ใน `CookingSettings` ปรับได้
- ตาราง ★ × คุณภาพ: ★1 1.00/1.10/1.25 · ★2 1.10/1.275/1.50 · ★3 1.20/1.45/1.75 · ★4 1.30/1.625/2.00
- ค่าพลังตามคุณภาพ: OK 1.00 / Good 1.15 / Perfect 1.30 (H/S/M เท่านั้น ไม่คูณ FEVER)
- เพดานค่าพลังจาน: H 80 (★4 = 100) / S 250 / M 150
- ห้ามเตือนผู้ใช้ให้ commit

## Review Focus

1. **เซฟเก่า v8 ที่มีจานในกระเป๋า/Hotbar/เมนู/กล่อง** → ต้องโหลดได้ ของที่ยังมีใน v2 อยู่ครบ ของที่หายไปถูกข้ามพร้อมเตือน ไม่ crash (ทดสอบใน Task 13)
2. **จานแบบที่ถูกเซฟแล้ววัตถุดิบนั้นถูกลบจากชีต** (`jp_sushi__xxx` ที่ไม่มี xxx) → GetById คืน null + เตือน ไม่สร้างไอเทมผี (Task 5)
3. **ช่องหมวดที่ไม่มีวัตถุดิบในหมวดเลยในกระเป๋า** → ปุ่มทำอาหารกดไม่ได้ ไม่ error / ตัวเลือกล่าสุดที่หมดแล้ว → ตกไปตัวที่มีมากสุด (Task 2, 7)
4. **เมนูประจำวันที่จองวัตถุดิบหมวดเดียวกันสองสูตร** (ซูชิ + ปลาเผาใช้ any_fish ร่วม) → จำนวนที่ทำได้ไม่นับปลาตัวเดียวกันซ้ำสองสูตร (Task 2)
5. **จานในมือ (CarriedDish) ตอนร้านปิด/โหลดเซฟ** → ต้องเข้ากระเป๋าเป็นไอเทมแบบที่ถูกต้อง ไม่กลายเป็นจานแม่ (Task 6)

> **Ruling (Claude):** โปรเจกต์ไม่มี Unity Test Framework และผู้ใช้เป็นคนรัน Unity → แทน "test ที่ fail ก่อน" ด้วย: (1) คอมไพล์ผ่าน (2) เมนู Debug ที่พิมพ์ผลจริงเทียบกับค่าที่คาด (3) ขั้นเล่นทดสอบที่ผู้ใช้ทำ แต่ละ Task จบด้วยรายการตรวจที่ผู้ใช้ทำได้ใน 5 นาที

---

## ขั้น 1A — ข้อมูลเข้าเกม (Task 1–3) → ผู้ใช้ทดสอบ: import ผ่าน + เกมเล่นได้เหมือนเดิม

### Task 1: CSV จากชีต v2

**Files:**
- Modify (เขียนใหม่ทั้งไฟล์): `Assets/Data/Ingredients.csv`, `Assets/Data/Recipes.csv`

**Interfaces:**
- Produces: หัวคอลัมน์ Ingredients = `item_id, ชื่อไทย, Name (EN), Category, Source, In Store, Early, ใช้ในกี่สูตร, Sell Price, Health, Stamina, Mana, Fever, หมายเหตุ, Type, Season` / Recipes = หัวชีต `Recipes v2` A1:AB1 ตรงตัว (ค่าสูตร COUNTIF ใส่เป็นค่าตัวเลข)

- [ ] **Step 1:** อ่าน `Ingredients v2` A1:P112 และ `Recipes v2` A1:AB110 ด้วย Claude Docs connector (revision ล่าสุด)
- [ ] **Step 2:** เขียน CSV ด้วย python `csv` (QUOTE_MINIMAL) UTF-8 BOM + CRLF ช่องสูตรใช้ `value`
- [ ] **Step 3:** ตรวจ: `python -c` นับแถว
  Expected: Ingredients 112 แถวข้อมูล, Recipes 109 แถวข้อมูล, ทุก Key ที่ไม่ขึ้นต้น any_ อยู่ใน item_id

### Task 2: enum + ItemData + RecipeSlot + RecipeRequirements

**Files:**
- Modify: `Assets/Scripts/ItemData.cs`, `Assets/Scripts/Cooking/RecipeData.cs`, `Assets/Scripts/Cooking/RecipeSource.cs`
- Create: `Assets/Scripts/Cooking/RecipeRequirements.cs`, `Assets/Scripts/Systems/Season.cs`
- Modify (เปลี่ยนการเรียก `GetRequiredCounts`): `Cooking/CookingSession.cs`, `Cooking/DailyMenu.cs`, `Cooking/RecipeDatabase.cs`, `Cooking/RecipeBook.cs`, `UI/InventoryUI.cs`

**Interfaces:**
- Produces:
  - `ItemCategory` + `Mushroom, Fish, Shellfish, Egg, Legume, Spice, Processed, Import, Seaweed, Cheese, AgedCheese`
  - `ItemSource` + `Garden, Ranch, Hunt, Processed, Import`
  - `enum ItemOriginType { Unknown, Forage, FruitTree, Crop, Fish, CrabPotBeach, AnimalProduct, MonsterLoot, Artisan, Import }`
  - `[Flags] enum Season { None = 0, Spring = 1, Summer = 2, Fall = 4, Winter = 8, All = 15 }` + `SeasonUtil.TryParse(string, out Season)` ("Fall, Winter" / "All")
  - `RecipeSource` + `Mastery`
  - ItemData: `bool inStore`, `bool earlyGame`, `ItemOriginType originType`, `Season seasons` / runtime (`[NonSerialized]`): `RecipeData VariantRecipe`, `ItemData VariantIngredient`, `bool IsVariant`
  - `[Serializable] struct RecipeSlot { ItemData item; bool anyOfCategory; ItemCategory category; bool Accepts(ItemData); string Label }`
  - RecipeData: `List<RecipeSlot> slots` (≤5), `RecipeData baseRecipe`, `MaxIngredients = 5`, `MaxDifficulty = 4`, `bool HasCategorySlot`, `int CategorySlotIndex` (-1 = ไม่มี), `RecipeSlot CategorySlot`, `[HideInInspector] List<ItemData> ingredients` (เก่า ไม่ใช้)
  - `static class RecipeRequirements`:
    - `List<ItemData> GetCandidates(RecipeData r, Inventory inv)` — วัตถุดิบในหมวดที่มี ≥ จำนวนที่ต้องใช้ เรียงจำนวนมากสุดก่อน แล้วตาม itemId
    - `ItemData DefaultChoice(RecipeData r, Inventory inv, ItemData lastChoice)` — lastChoice ถ้ายังพอ ไม่งั้นตัวแรกของ GetCandidates / ไม่มีช่องหมวด = null
    - `Dictionary<ItemData,int> Resolve(RecipeData r, ItemData choice)` — ช่องหมวดใช้ choice (null = ข้ามช่องนั้น)
    - `bool HasAll(RecipeData r, Inventory inv, ItemData choice)`
    - `int CountServings(RecipeData r, Inventory inv, IDictionary<ItemData,int> alreadyReserved)` — จำนวนจานที่ทำได้ ช่องหมวดนับรวมทุกตัวในหมวดลบที่จองแล้ว

- [ ] **Step 1:** เพิ่ม enum/ช่องตาม Interfaces (เก็บ `GetRequiredCounts()` ไว้เป็น `[Obsolete]` ที่เรียก `Resolve(this, null)`)
- [ ] **Step 2:** เขียน `RecipeRequirements` ตามลายเซ็น
- [ ] **Step 3:** แทนที่ 5 จุดที่เรียก `GetRequiredCounts()`:
  - CookingSession `HasIngredients/ConsumeIngredients(recipe, choice)` (choice ส่งมาจาก StartCooking — ขั้นนี้ใช้ `DefaultChoice(.., null)`)
  - DailyMenu `GetMaxServings/GetCookRoom` ใช้ `CountServings` โดยส่งยอดที่สูตรก่อนหน้าในเมนูจองไว้ (Review Focus 4)
  - RecipeDatabase `CanCook/GetMaxServings`, RecipeBook debug, InventoryUI รายละเอียดสูตร ("Any Fish" แสดงจาก `RecipeSlot.Label`)
- [ ] **Step 4:** ผู้ใช้กด Play ด้วยข้อมูลเดิม (ยังไม่ import) Expected: คอมไพล์ผ่าน / Console ไม่มี error ใหม่ (สูตรเดิมยังไม่มี slots → Task 3 ทำให้ครบ; ขั้นนี้ให้ `slots` ว่าง = อ่านจาก `ingredients` เดิมเป็นช่องตายตัวชั่วคราว)

### Task 3: ตัว import v2

**Files:**
- Modify: `Assets/Scripts/Editor/RecipeImporter.cs`

**Interfaces:**
- Consumes: Task 2 ทั้งหมด
- Produces: RecipeData.slots / baseRecipe / difficulty 1–4 / obtainSource Mastery, ItemData.inStore/earlyGame/originType/seasons

- [ ] **Step 1:** คอลัมน์ใหม่: `In Store`, `Early`, `Type`, `Season`, `Key 1`–`Key 5`, `Base Recipe` / `TryParseEnum` ตัด `/` ด้วย ("Crab Pot / Beach" → CrabPotBeach, "Aged Cheese" → AgedCheese, "Fruit Tree" → FruitTree) / Y/N → bool
- [ ] **Step 2:** Key ขึ้นต้น `any_` → `RecipeSlot{anyOfCategory, category}` (any_fish→Fish, any_vegetable→Vegetable, any_fruit→Fruit, any_cheese→Cheese, อื่น = เตือน) / ช่องหมวด > 1 = เตือน
- [ ] **Step 3:** รอบสองหลังสร้างสูตรครบ: Base Recipe → `baseRecipe` (ไม่เจอ = เตือน) / difficulty ตรวจ 1–`MaxDifficulty`
- [ ] **Step 4:** รายงานของเก่า: ItemData ชนิด Ingredient ที่ไม่มีใน CSV และ RecipeData ที่ไม่มีใน CSV → พิมพ์รายชื่อ (ไม่ลบ)
- [ ] **Step 5:** ผู้ใช้รัน `Tools > Second Serving > Import Recipes & Ingredients` แล้ว `ItemDatabase > Find All Items`
  Expected: สรุป Items 112 (+ จาน 109) Recipes 109 / รายชื่อของเก่า (เช่น seasonal_fruit) / ไม่มีเตือน Unknown category/source/type / `jp_sushi` มีช่อง Any Fish + Rice / `fd_th_kra_pao` 5 ช่อง baseRecipe = th_kra_pao
- [ ] **Step 6 (ผู้ใช้ทดสอบ 1A):** เล่น 1 วัน: ทำอาหารนอกร้าน (สูตรช่องหมวดใช้ตัวที่มีมากสุดอัตโนมัติ) / เปิดร้าน / ขาย / นอน / โหลดเซฟ → ทำงานเหมือนเดิม

---

## ขั้น 1B — จานแยกแบบ + ราคา (Task 4–6) → ผู้ใช้ทดสอบ: ทำซูชิแซลมอน/นิล ได้คนละไอเทม ราคาต่างกัน

### Task 4: ค่าตั้งเศรษฐกิจ + DishPricing

**Files:**
- Modify: `Assets/Scripts/Cooking/CookingSettings.cs`
- Create: `Assets/Scripts/Cooking/DishPricing.cs`, `Assets/Scripts/Cooking/CategoryStats.cs`

**Interfaces:**
- Produces:
  - CookingSettings `[Header("Economy (v2)")]`: `variantPriceFactor 2.4`, `secretPriceFactor 1.3`, `fineDiningPriceFactor 2.0`, `variantStatFactors (1.5,1.5,1.2)`, `secretStatFactor 1.2`, `dishCaps (80,250,150)`, `fineDiningHealthCap 100`, `cookedZeroValues (H5,S20)`, `float[] priceMultipliers` 12 ค่า (★1–4 × OK/Good/Perfect ตาม Global Constraints), `float[] qualityStatMultipliers {1, 1.15, 1.3}`
  - `float PriceMultiplier(int stars, DishQuality q)` / `float QualityStatMultiplier(DishQuality q)`
  - `static class CategoryStats`: `Build(ItemDatabase)` / `AveragePrice(ItemCategory)` / `AverageRestore(ItemCategory)` (Vector3 H,S,M หลังกฎ "ค่า 0 = H5 S20" และ "ติดลบ = 0")
  - `static class DishPricing`:
    - `int VariantPrice(RecipeData r, ItemData ingredient)` = round5(r.sellPrice + (ingredient.sellPrice − avg) × 2.4 × (secret 1.3 | ★4 2.0)) ไม่ต่ำกว่า 5
    - `Vector3Int VariantRestore(RecipeData r, ItemData ingredient)` + FEVER = ค่าจาน
    - `int SalePrice(ItemData dish, DishQuality q)` = floor(dish.sellPrice × PriceMultiplier(stars ของสูตรแม่, q)) / ไม่ใช่จาน = sellPrice
    - `void EatValues(ItemData item, DishQuality? q, out int h, out int s, out int m, out int fever)` (คูณคุณภาพเฉพาะจาน)

- [ ] **Step 1:** เพิ่มค่าและเขียนคลาสตามลายเซ็น
- [ ] **Step 2:** เมนู `Tools > Second Serving > Debug > Print Dish Price Table` พิมพ์ ทุกสูตรที่มีช่องหมวด × ทุกวัตถุดิบในหมวด: ราคาแบบ, H/S/M, และราคาขาย OK/Good/Perfect
  Expected: `jp_sushi (Tilapia)` < 295 < `jp_sushi (Salmon)` / ปลาที่ราคาเท่าค่าเฉลี่ย ≈ 295 / `us_mac_cheese (Cheese)` ≈ 610 (ชีสธรรมดาถูกกว่าค่าเฉลี่ยหมวด) / ★1 Perfect = ×1.25

### Task 5: DishVariantRegistry + ItemDatabase

**Files:**
- Create: `Assets/Scripts/Cooking/DishVariantRegistry.cs`
- Modify: `Assets/Scripts/ItemDatabase.cs`, `Assets/Scripts/Cooking/RecipeDatabase.cs`

**Interfaces:**
- Produces:
  - `const string Separator = "__"`
  - `ItemData GetOrCreate(RecipeData r, ItemData ingredient)` — ไม่มีช่องหมวด = `r.dishItem` / สร้าง `ScriptableObject.CreateInstance<ItemData>()` (hideFlags DontSave) itemId `r.recipeId + "__" + ingredient.itemId`, itemName `"{r.recipeName} ({ingredient.itemName})"`, icon/category/type ของจานแม่, sellPrice + restore จาก DishPricing, VariantRecipe/VariantIngredient
  - `bool TryResolve(string itemId, out ItemData)` — แยกด้วย `__` → หา recipe + ingredient → GetOrCreate / หาไม่เจอ = false + เตือนครั้งเดียว (Review Focus 2)
  - `RecipeData RecipeOf(ItemData dish)` (จานแม่ = หาจาก dishItem, แบบ = VariantRecipe)
  - `List<ItemData> DishItemsIn(RecipeData r, Inventory inv)` — จานแม่ + แบบทุกตัวที่มีในกระเป๋า
  - ItemDatabase.GetById: ไม่เจอ → `DishVariantRegistry.TryResolve`
  - RecipeDatabase: `RecipeData FindByDish(ItemData)`

- [ ] **Step 1:** เขียนตามลายเซ็น (Registry ต้องรู้ RecipeDatabase + ItemDatabase: ใช้ตัวที่ SaveManager/ระบบหาได้ หรือ `Resources`/อ้างอิงใน CookingSettings — เลือกตอนเขียน แล้วบันทึก Ruling)
- [ ] **Step 2:** Debug เมนู `Print Variant Registry` (จำนวนแบบที่สร้างแล้ว)
  Expected: เปิดเกมใหม่ = 0 / หลังทำซูชิแซลมอน 1 ครั้ง = 1 (`jp_sushi__salmon`)

### Task 6: ทำอาหาร → จานแบบ / จานในมือ / เสิร์ฟ / ขาย / กิน

**Files:**
- Modify: `Cooking/CookingSession.cs`, `Player/CarriedDish.cs`, `Cooking/DailyMenu.cs`, `Shop/Customer.cs`, `Shop/CustomerManager.cs` (ถ้าลายเซ็น ReportServed/ReleasePending ต้องรับไอเทม), `Shop/Commerce/SalesLedger.cs`, `Player/PlayerStats.cs`, `Systems/Save/SaveManager.cs` + `SaveData.cs` (จานในมือ ถ้ามีการเซฟ)

**Interfaces:**
- Consumes: Task 2, 4, 5
- Produces:
  - `CookingSession.StartCooking(RecipeData r, CookMode m, ItemData choice = null)` — null = DefaultChoice / จบแล้วได้ `DishVariantRegistry.GetOrCreate(r, choice)`
  - `CarriedDish`: `ItemData Item`, `DishQuality Quality`, `RecipeData Recipe => RecipeOf(Item)`, `Hold(ItemData dish, DishQuality q)` (Stash/ร้านปิด → `inventory.AddItem(Item, 1, Quality)` — Review Focus 5)
  - DailyMenu: `GetDishesInBag(r)` = รวม DishItemsIn / `TryTakeServing(RecipeData r, ItemData dish, DishQuality? q, out ItemData servedItem, out DishQuality served)` (dish null = แบบใดก็ได้ที่ดีสุด) / `GetReservedCount(item)` รองรับแบบ
  - Customer: เก็บ `ServedItem`, จ่าย `DishPricing.SalePrice(ServedItem, served)` + ทิปคิดจากราคานี้
  - SalesLedger: `UnitSellPrice` และ `QualityMultiplier` → เรียก DishPricing (คง `QualityMultiplier` ไว้แบบ `[Obsolete]`)
  - PlayerStats: `TryEat(ItemData item, Inventory inv, DishQuality? quality = null)` (null = ต่ำสุดก่อน เหมือน RemoveItem) ใช้ `DishPricing.EatValues`

- [ ] **Step 1–3:** แก้ตาม Interfaces ทีละไฟล์ (CookingSession → CarriedDish → DailyMenu → Customer/Manager → SalesLedger → PlayerStats)
- [ ] **Step 4 (ผู้ใช้ทดสอบ 1B):**
  - Debug Give Item `salmon` + `tilapia` + `rice` → ทำ Sushi สองครั้ง (ยังไม่มีตัวเลือก: ได้ตัวที่มีมากสุด) Expected: กระเป๋ามี "Sushi (Salmon)" และ "Sushi (Tilapia)" แยก / Print Inventory แสดง itemId `jp_sushi__salmon`
  - ขายที่ร้านของชำ Expected: ราคาตาม Print Dish Price Table × ตัวคูณคุณภาพ
  - ใส่เมนู + เปิดร้าน + เสิร์ฟจากมือและจากกระเป๋า Expected: เงินที่ได้ = ราคาแบบ × ตัวคูณ + ทิป
  - กินจาน Good Expected: ค่าพลัง × 1.15
  - นอน → โหลดเซฟ Expected: Sushi (Salmon) ยังอยู่

---

## ขั้น 1C — ผู้เล่นเลือกวัตถุดิบ (Task 7) → ผู้ใช้ทดสอบ: เลือกปลาเองได้

### Task 7: ตัวเลือกวัตถุดิบ + ตัวที่เลือกล่าสุด + เซฟ v9 (ส่วนที่ 1)

**Files:**
- Modify: `UI/CookingMenuUI.cs`, `UI/CookingRecipeItemUI.cs`, `Cooking/RecipeBook.cs`, `Shop/Stove.cs`, `Systems/Save/SaveData.cs`, `Systems/Save/SaveManager.cs`
- Modify (ถ้าจำเป็นต่อการจัดหน้าต่าง): `Editor/CookingSetupBuilder.cs`

**Interfaces:**
- Produces:
  - RecipeBook: `ItemData GetLastChoice(RecipeData)`, `void SetLastChoice(RecipeData, ItemData)`, `CaptureChoices(List<string> recipeIds, List<string> itemIds)`, `RestoreChoices(...)`
  - SaveData v9: `lastChoiceRecipeIds`, `lastChoiceItemIds`
  - CookingRecipeItemUI: แถวสูตรที่มีช่องหมวด แสดง `‹ Salmon ×3 ›` คลิกลูกศร = เปลี่ยน (วนตาม GetCandidates) / ค่าเริ่มต้น = DefaultChoice(lastChoice) / กดทำ = `StartCooking(r, Free, choice)` + SetLastChoice
  - Stove (ร้านเปิด): `StartCooking(r, InShop, DefaultChoice(r, inv, GetLastChoice(r)))` ไม่ถาม
- [ ] **Step 1–2:** แก้ตาม Interfaces / เซฟ v8 โหลด = ไม่มีตัวเลือกล่าสุด
- [ ] **Step 3 (ผู้ใช้ทดสอบ 1C):** มีปลา 3 ชนิด → หน้าต่างทำอาหารเลือกปลาได้ / ค่าเริ่มต้นตัวที่มีมากสุด / ทำแล้วปิดเปิดใหม่ = จำตัวล่าสุด / ปลาตัวนั้นหมด = ตกไปตัวที่มีมากสุด / ไม่มีปลาเลย = กดทำไม่ได้ (Review Focus 3) / เตาในร้านใช้ตัวล่าสุด / นอน+โหลด = ยังจำ

---

## ขั้น 1D — UI รวมกลุ่มที่กางเลือกได้ (Task 8–11) → ผู้ใช้ทดสอบ: เลือกแถวเองได้ทุกจุด

### Task 8: ServePicker เลือก แบบ × คุณภาพ

**Files:** Modify `Shop/ServePicker.cs`, `Shop/Customer.cs` (HandleServeInteract / TryServe)

**Interfaces:**
- Produces: ตัวเลือก = `List<(ItemData dish, DishQuality q)>` จาก `DishItemsIn(Order)` × `GetAvailableQualities` เรียง ดีสุดก่อน แล้วตามราคา / ข้อความ "Salmon · Perfect" (จานแม่ไม่มีแบบ = "Perfect" เหมือนเดิม) / ตัวเลือกเดียว = เสิร์ฟทันที / `Customer.TryServe(ItemData dish, DishQuality? q)`
- [ ] **Step 1:** แก้ตาม Interfaces
- [ ] **Step 2 (ผู้ใช้):** ลูกค้าสั่งซูชิ มีแซลมอน Perfect + นิล OK → แถบขึ้น 2 ตัวเลือก เลือกนิลได้ / มีแบบเดียว = เสิร์ฟทันที

### Task 9: กระเป๋า แท็บ Dishes รวมกลุ่ม + กล่องตอนวางเมาส์ + แผงรายละเอียดแบบแถว

**Files:**
- Modify: `UI/InventoryUI.cs`, `UI/InventorySlotUI.cs`, `Editor/InventoryLayoutBuilder.cs` (เพิ่มที่วางแถวในแผงรายละเอียด ไม่จัด layout เดิมใหม่)
- Create: `UI/DishVariantRowUI.cs`

**Interfaces:**
- Produces:
  - แท็บ Dishes: 1 ช่องต่อสูตร (`dishItem` แม่เป็นตัวแทน) จำนวน = รวมทุกแบบทุกคุณภาพ / สถานะ Owned เมื่อรวม > 0 / ซ่อนสูตรลับเหมือนเดิม
  - DishQualityTooltip: บรรทัดต่อแบบ `Salmon  Perfect 1 · Good 2`
  - แผงรายละเอียด: `DishVariantRowUI` ต่อ (แบบ, คุณภาพ): ไอคอน, `"Salmon · Perfect ×1"`, ปุ่ม **Eat** (`PlayerStats.TryEat(dish, inv, q)`), ลากได้ (`IBeginDragHandler…` ส่ง payload `(ItemData, DishQuality)` ให้ HotbarSlotUI)
- [ ] **Step 1–2:** แก้ตาม Interfaces (รัน `Build Inventory Window Layout` เฉพาะถ้าต้องเพิ่มโหนด — ⚠️ ถามผู้ใช้ก่อนถ้าเขาขัดเกลา layout ไว้แล้ว)
- [ ] **Step 3 (ผู้ใช้):** ซูชิ 4 แบบ×คุณภาพ = ช่องเดียว ×รวม / วางเมาส์เห็นแยก / คลิกเห็นแถว / Eat แถว OK แล้วแถว Perfect ยังอยู่

### Task 10: Hotbar จำแบบ + คุณภาพ / สีเทาเมื่อหมด / เซฟ v9 (ส่วนที่ 2)

**Files:** Modify `Player/Hotbar.cs`, `UI/HotbarSlotUI.cs`, `UI/HotbarUI.cs`, `Player/PlayerActionController.cs` (กิน), `Systems/Save/SaveData.cs`, `Systems/Save/SaveManager.cs`

**Interfaces:**
- Produces: Hotbar `DishQuality? GetQuality(int index)`, `SetSlot(int index, ItemData item, DishQuality? q)` (ของเดิมเรียก `SetSlot(i, item)` = q null), `CaptureSlotQualities()` → SaveData v9 `hotbarQualities` (-1 = ไม่ระบุ) / ช่องที่จำนวน 0 = ไอคอนสีเทา ไม่ล้างช่อง / กินจากช่อง = `TryEat(item, inv, q)` / ลากจานแม่จากช่องกระเป๋า = q null (กินต่ำสุดก่อน)
- [ ] **Step 1–2:** แก้ตาม Interfaces / เซฟ v8 = คุณภาพ null
- [ ] **Step 3 (ผู้ใช้):** ลากแถว "Tilapia · OK" ลง Hotbar → กดกิน = กินนิล OK / หมด = ช่องเทา / ทำนิล OK ใหม่ = กลับมา / นอน+โหลด = ยังจำคุณภาพ

### Task 11: แผงขาย + กล่องขาย แถว แบบ × คุณภาพ

**Files:** Modify `UI/SellPanelUI.cs`, `UI/SellItemSlotUI.cs`, `UI/ShippingBoxUI.cs`, `UI/BoxEntryRowUI.cs`, `Shop/Commerce/SalesLedger.cs` (ถ้า BoxEntry ต้องรองรับแบบ — แบบเป็น ItemData อยู่แล้ว น่าจะไม่ต้อง)

**Interfaces:**
- Produces: แท็บ Dishes ของแผงขาย = แถวต่อ (ไอเทมแบบ, คุณภาพ) ชื่อ `"Sushi (Salmon) · Perfect"` ราคา `DishPricing.SalePrice` / คลิก = ขาย/ใส่กล่องแถวนั้น
- [ ] **Step 1:** แก้ตาม Interfaces
- [ ] **Step 2 (ผู้ใช้):** ใส่ "Sushi (Tilapia) · OK" ลงกล่อง แซลมอนยังอยู่ในกระเป๋า / นอน = เงินกล่องตามราคาแถวนั้น

---

## ขั้น 1E — กรอบไฮไลท์ (Task 12) + ปิดงาน (Task 13)

### Task 12: กรอบไฮไลท์ตอนวางเมาส์

**Files:**
- Create: `UI/HoverHighlight.cs`
- Modify: `UI/InventorySlotUI.cs`, `UI/DishVariantRowUI.cs`, `UI/HotbarSlotUI.cs`, `UI/SellItemSlotUI.cs`, `UI/BoxEntryRowUI.cs`, `UI/CookingRecipeItemUI.cs`

**Interfaces:**
- Produces: `HoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler` — สร้าง Image กรอบลูกเองถ้าไม่มี (สี/ความหนาใน Inspector, ค่าเริ่ม ขาวนวล alpha 0.9 หนา 3px) / `bool Interactable` (ผู้เรียกตั้ง: ช่องว่าง/Undiscovered = false) / แยกจาก SelectionFrame
- [ ] **Step 1:** เขียนคอมโพเนนต์ + ใส่ทุกจุด (เพิ่มด้วยโค้ดตอน Awake ถ้ายังไม่มี ไม่ต้องแก้ prefab)
- [ ] **Step 2 (ผู้ใช้):** วางเมาส์บนช่องวัตถุดิบ/จาน/อุปกรณ์ที่มีของ = กรอบขึ้น / ช่องว่าง = ไม่ขึ้น / แถวแบบ×คุณภาพ = ขึ้น / Hotbar แผงขาย กล่อง หน้าต่างทำอาหาร = ขึ้นแบบเดียวกัน

### Task 13: ทดสอบรวม + HANDOFF

**Files:** Modify `Docs/HANDOFF.md` (หัวข้อ 5 ข้อมูล v2, 7 สคริปต์ใหม่, 9 Roadmap, 10 ขั้นถัดไป), `Docs/remaining-work.md` (ติ๊กงาน 1)

- [ ] **Step 1 (ผู้ใช้ — Review Focus 1):** โหลดเซฟ v8 เดิมที่มีจานในกระเป๋า Hotbar เมนู กล่อง Expected: โหลดได้ / ของที่ยังมีอยู่ครบ / Console มีแค่เตือน Unknown itemId ของเก่า
- [ ] **Step 2:** อัปเดตเอกสาร + บอกรายชื่อไฟล์ที่แก้ทั้งหมด

---

## Self-review (Claude)
- ครอบคลุม spec หัวข้อ 4.1–4.9: 4.1→T1,T3 · 4.2–4.4→T2 · 4.5→T2,T7 · 4.6→T5 · 4.7→T4,T6 · 4.8→T8–T12 · 4.9→T7,T10 (servedCounts/pendingMastery เป็นงาน 3)
- งาน 2 (Stamina) 3 (Mastery) 4 (เศรษฐกิจ) จะมีแผนแยกหลังงาน 1 ผ่าน
