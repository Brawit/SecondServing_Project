# Second Serving – HANDOFF

> ไฟล์นี้สรุปสถานะโปรเจกต์ให้ Claude ในแชทใหม่อ่านต่อได้ทันที
> อัปเดตล่าสุด: 21 พฤศจิกายน 2026 (จบขั้น B ทั้งหมด: B1–B4 / ถัดไปขั้น C Equipment Slots)
> ตำแหน่งไฟล์: `D:\SecondServing_Project\Docs\HANDOFF.md` (อยู่นอก Assets ตั้งใจไม่ให้ Unity import)

---

## 1. เกม

- **ชื่อโปรเจกต์:** SecondServing_Project
- **แนว:** Cozy Simulation 2D มุมมอง top-down แบบ Stardew Valley + Dave the Diver ผสม RPG เบาๆ
- **วงจรการเล่น:** เช้าถึงบ่ายออกสำรวจเก็บวัตถุดิบ / 17:00–19:00 เปิดร้านอาหารนานาชาติได้ / นอนแล้วขึ้นวันใหม่
- **ธีม:** อาหารคือความทรงจำ ("Second Serving" = ได้ลิ้มรสความทรงจำอีกครั้ง) สูตรลับบางสูตรมาจาก side story ของ NPC
- **เป้าหมาย:** ปล่อยบน Steam (PC) และทำเดโมบนเว็บ (itch.io)
- **ทำคนเดียว:** ยังไม่มีสไปรต์จริง ใช้สี่เหลี่ยมแทน

## 2. เครื่องมือและเวอร์ชัน

- Unity 6.3 LTS (6000.3.28f1), Universal 2D (URP), Windows
- Visual Studio Community 2026 (ตั้งค่า auto-reload ไฟล์ที่เปลี่ยนจากภายนอกไว้แล้ว)
- Input System ตัวใหม่ (`Keyboard.current`), `rb.linearVelocity`
- Cinemachine 3.x (`Unity.Cinemachine`, `CinemachineCamera`, `CinemachineConfiner2D`)
- UI: uGUI (Canvas) + TextMeshPro, Canvas Scaler = Scale With Screen Size 1920x1080, Match 0.5
- ฟอนต์ TMP ยังไม่มีภาษาไทย → ข้อความในเกมเป็นภาษาอังกฤษไปก่อน ชื่อไทยเก็บใน `nameTh`
- GitHub (Private, Git LFS) ผู้ใช้ commit เองทุกครั้งที่จบหัวข้อ ไม่ต้องเตือน
- มีแพ็กเกจ `com.unity.ai.navigation` ติดตั้งอยู่ (ยังไม่ได้ใช้)

## 3. ข้อตกลงในการทำงานร่วมกัน

- ตอบเป็นภาษาไทย
- แนะนำขั้นตอนใน Unity Editor แบบละเอียดทีละขั้น ถ้าไม่แน่ใจตำแหน่งเมนูให้บอกตรงๆ
- อธิบายโครงสร้างแบบสั้นก่อนเขียนโค้ด (plan first) และถามก่อนถ้ามีทางเลือกที่เปลี่ยนการออกแบบ
- เขียนโค้ด C# ให้ครบทั้งไฟล์ ห้ามย่อหรือข้าม
- `[Header]`, ชื่อตัวแปร, ชื่อคลาส, `Debug.Log` เป็นภาษาอังกฤษ / คอมเมนต์เป็นภาษาไทย
- ถ้าผู้ใช้บอกว่าไม่ต้องส่งโค้ด ให้ส่งเฉพาะคำอธิบาย
- แยกงานเป็นโมดูลย่อย ทีละระบบ
- ถ้ายังไม่ได้ทดลองรันโค้ดจริง ให้บอกตรงๆ
- แก้ไฟล์ให้แล้ว ต้องบอกชื่อไฟล์ทุกไฟล์ที่เปลี่ยน
- ไม่ต้องเตือนเรื่อง commit
- เมื่อจบหัวข้อ: สรุป roadmap ในแชท + อัปเดตไฟล์ HANDOFF.md นี้

## 4. การเข้าถึงไฟล์ (สำคัญ)

- ขอสิทธิ์โฟลเดอร์ `D:\SecondServing_Project` ผ่านแอปเดสก์ท็อป แล้วอ่าน/เขียนไฟล์ได้โดยตรง
- ไฟล์ `.cs` เป็น **UTF-8 with BOM + CRLF**
- ⚠️ **เคยเกิดปัญหา:** เครื่องมือส่งไฟล์ (device_commit_files) ส่ง**เวอร์ชันเก่า**ของไฟล์ไป เมื่อไฟล์ใน outputs ถูกแก้หลังจากเขียนครั้งแรก
  - **วิธีป้องกัน:** แปลง encoding ให้เสร็จก่อน แล้ว**คัดลอกไปไว้ที่ path ใหม่ที่ไม่เคยใช้** แล้วค่อย commit จาก path นั้น
  - **หลัง commit ทุกครั้ง:** ให้ stage ไฟล์กลับมาเทียบด้วย `cmp` ว่าตรงกับต้นฉบับ
- อ่านฉากได้จาก `Assets/Scenes/SampleScene.unity` (YAML) ใช้ตรวจโครงสร้าง UI และช่องที่ยังไม่ได้ผูกได้ ต้องให้ผู้ใช้กด Ctrl+S ก่อน
- ถ้าต้องดูหน้าจอจริงหรือ Console ใช้ computer use ได้ ต้องขอสิทธิ์ก่อน
- ฝั่ง Claude ไม่มี Unity / ตัวคอมไพล์ C# → โค้ดทุกไฟล์ยังไม่ได้คอมไพล์จนกว่าผู้ใช้จะเปิด Unity ต้องบอกตรงๆ ทุกครั้ง
- ไฟล์ HANDOFF.md นี้เป็น UTF-8 (ไม่มี BOM) + CRLF

## 5. ข้อมูลสูตรอาหารและวัตถุดิบ

- **ตารางต้นฉบับ (Claude Sheets):** https://claude.ai/artifact/4PmyMFeRHbk51BCE1eq4eJ
  - ชีต `อ่านก่อน` / `Recipes` (87 สูตร) / `Ingredients` (83 ชนิด)
  - Recipes: `#`, ชื่อเมนู, Name (EN), Country, Course, Secret, Key 1–4 (item_id), สถานะ, Vertical Slice, recipe_id, วัตถุดิบเต็ม, หมายเหตุ, Sell Price, Unlock, Rumor, Story Notes
  - Ingredients: item_id, ชื่อไทย, Name (EN), Category, Source (Forage/Sea/Market/Artisan/Pantry), ใช้ในกี่สูตร (สูตร COUNTIF), รวมมาจาก, Sell Price
  - **[ใหม่] คอลัมน์ค่าที่ฟื้นเมื่อกิน `Health / Stamina / Mana / Fever`** ท้ายทั้งสองชีต (Recipes = T:W, Ingredients = I:L) เป็น**ค่าสุ่มตั้งต้น** ผู้ใช้จะไปปรับเอง
    - วัตถุดิบที่ทุกค่าเป็น 0 = กินดิบไม่ได้ (แป้ง เนื้อดิบ ข้าวสาร เห็ด ฯลฯ) / อาหารฟื้น Mana บางจาน / FEVER ในอาหารปกติมีไม่กี่จาน (เมนูเผ็ด) / สูตรลับทุกจานเพิ่ม FEVER
  - Sell Price ยังว่างทุกแถว
- **CSV ในโปรเจกต์:** `Assets/Data/Recipes.csv`, `Assets/Data/Ingredients.csv` (มีคอลัมน์ Health/Stamina/Mana/Fever แล้ว / ยังไม่มี Unlock / Rumor / Story Notes)
  - วิธีสร้าง CSV ใหม่: อ่านตารางด้วย Claude Docs connector แล้ว**ต่อคอลัมน์ท้ายแถวเดิมหรือเขียนใหม่ทั้งไฟล์** เป็น UTF-8 with BOM + CRLF หัวคอลัมน์ต้องตรงกับตาราง
- **ตัว import:** เมนู `Tools > Second Serving > Import Recipes & Ingredients` (`Scripts/Editor/RecipeImporter.cs`)
  - หาคอลัมน์จาก**ชื่อหัวคอลัมน์** (ห้ามเปลี่ยนชื่อหัว)
  - อ่าน Health/Stamina/Mana/Fever ใส่ `restoreHealth/Stamina/Mana/Fever` ของวัตถุดิบ และของไอเทมจานอาหาร (จากชีต Recipes) ถ้าไม่มีคอลัมน์จะไม่แตะค่าเดิม
  - สร้าง/อัปเดต ItemData ตาม itemId (ไฟล์ใหม่อยู่ที่ `Assets/Items/`) ไม่เขียนทับ icon และ description
  - สร้าง RecipeData ใน `Assets/Recipes/` + `RecipeDatabase.asset`
  - สร้างไอเทมจานอาหาร (ItemType.Dish, itemId = recipe_id) ใน `Assets/Items/Dishes/`
  - ItemDatabase (`Assets/ItemDatabase.asset`) มี 170 ไอเทม (วัตถุดิบ 83 + จาน 87)
- **ถ้าผู้ใช้แก้ตาราง:** Claude สร้าง CSV ใหม่จากตาราง (อ่านด้วย Claude Docs connector) แล้วให้ผู้ใช้รัน import ซ้ำ

## 6. การตัดสินใจด้านออกแบบ

**ร้านและวัน**
- ร้านอยู่ฉากเดียวกับแผนที่ เข้าด้วยประตูวาร์ป (สลับขอบกล้อง) มีห้องนอนหลังร้านที่เข้าทางประตูหลังได้
- เปิดร้านได้ช่วง 17:00–19:00 (มีแจ้งเตือน) ถ้าถึง 19:00 ยังไม่เปิด วันนั้นเปิดไม่ได้อีก
- รอบขาย: จับเวลา 3 นาทีจริง หมดเวลาแล้วไม่รับลูกค้าใหม่ แต่ร้านยังเปิดจนลูกค้าในร้านหมด ปิดร้านแล้วข้ามไป 22:00 เสมอ นาฬิกาหยุดระหว่างขาย
- นอนได้ทุกเวลา ยกเว้นตอนเปิดร้าน (กด E ที่เตียงสองครั้ง) → หน้าสรุป → เซฟ → 06:00
- ถึง 03:00 ยังไม่นอนจะสลบแล้วไปตื่นที่โรงพยาบาล (ยังไม่มีบทลงโทษ)
- เปิดกระเป๋าไม่ได้ระหว่างเปิดร้านและระหว่างหน้าสรุปวัน

**สูตรอาหาร**
- สูตรในเกมใช้วัตถุดิบหลัก 1–4 อย่าง ส่วนเครื่องปรุงพื้นฐานอยู่ใน "ตู้กับข้าว" มีไม่จำกัด ไม่เป็นไอเทม
- แป้งสาลีเป็นไอเทม (Market) ใช้เป็นวัตถุดิบหลักเมื่อเป็นตัวเนื้ออาหาร
- ของเฉพาะทาง (นม ชีส ของหมัก ของประจำชาติ) = Artisan
- สูตรเริ่มต้น (Starter / Vertical Slice / In Demo) 8 สูตร 6 ประเทศ: ส้มตำ, ไก่ผัดกงเป่า, บรูสเกตตา, โอโคโนมิยากิ, ทามาเลส, ข้าวเหนียวมะม่วง, เลมอนเนด, อากัวเฟรสกา
- สูตรลับ 12 สูตร แบ่งเป็น **Rumor** (ได้ยินข่าวลือแล้วไปลองผสมที่เตาเอง) และ **Story** (side story ของ NPC + ฉากความทรงจำ "รสชาติแห่งความทรงจำ")
- รหัสสูตรลับ (recipe_id = itemId ของจาน): th_crab_omelette, th_bua_loy_egg, th_pandan_coconut, jp_omurice, jp_matcha_dorayaki, cn_xiao_long_bao, cn_mango_pomelo_sago, us_chicken_waffles, us_pecan_pie, mx_birria_tacos, house_rainbow_parfait, house_grandma_tea
- อาหารต้นเกมต้องมีค่าจนจบเกม: ราคาคิดจากต้นทุนวัตถุดิบ, ลูกค้าอยากกินเมนูเฉพาะ, โบนัสความหลากหลาย/เบื่อเมนูซ้ำ, ความชำนาญของสูตร, NPC/เทศกาลขอสูตรง่าย

**ร้านค้าและรายได้**
- ร้านค้ามี 5 บทบาท: ซื้อเวลา (ขายของที่หาเองได้แต่แพงกว่า), ขายเครื่องมือสำหรับทำของเอง, พ่อค้าเร่ขายของต่างประเทศ, ของนอกฤดู, รับซื้อของจากผู้เล่น
- รายได้มาได้หลายทาง มูลค่าเป็นขั้น: ของดิบ < Artisan < อาหาร
- ไอเดียสิ่งประดิษฐ์ช่วงปลายเกม: เรือนกระจก, สัญญานำเข้า, ห้องหมักบ่ม, บ่อเลี้ยงปลา
- เนื้อสัตว์ได้จากร้านขายเนื้อ และจากการล่า/ต่อสู้กับมอนสเตอร์

**กระเป๋าและอุปกรณ์**
- กระเป๋าวิเศษ: ของไม่เน่า ไม่เสีย คงสภาพเดิม ไม่มีช่องให้จัด แสดงแบบ Collectibles (ยังไม่เคยเจอ = เงาดำ)
- 5 แท็บ: Ingredients / Dishes / Recipes (สมุดสูตร) / Equipment (อาวุธ เครื่องมือ ชุด) / Accessory
- สูตรลับที่ยังไม่รู้: ซ่อนทั้งในแท็บ Recipes และจานของสูตรนั้นในแท็บ Dishes (ไม่แสดงแม้แต่เงาดำ) จานจะแสดงเมื่อรู้สูตร **หรือมี/เคยได้จานนั้นมาแล้ว** (เช่น NPC ให้)
- เพดานต่อชนิดตามระดับกระเป๋า: 30 → 99 → 300 → 999 (ระดับ 4 ได้จากบอสหรือเควสต์)
- จานอาหารเป็นไอเทม ไม่เน่า ทำนอกเวลาเปิดร้านได้ ใช้ให้ NPC / กิน / ขาย
- **ระบบผสม (เมนูประจำวัน):** เลือกได้ 3 เมนู (อัปเกรดเพิ่มได้) กด +/- ตั้งจำนวนจาน นับจากจานที่ทำไว้แล้วในกระเป๋า + วัตถุดิบที่จองไว้ วัตถุดิบหักตอนทำจริง ขายไม่หมดก็คืนเข้ากระเป๋า
- **Hotbar 10 ช่อง** ด้านล่างจอ: เลือกด้วยลูกกลิ้งเมาส์ / ปุ่ม 1–9, 0 / คลิกที่ช่อง ลากของจากกระเป๋ามาใส่ได้ (เป็นทางลัด ไม่ได้ย้ายของออกจากกระเป๋า) ไอเทมชนิดเดียวกันอยู่ได้ทีละช่อง
  - ใส่ได้เฉพาะ อาวุธ / เครื่องมือ / ของที่กินได้ (`Hotbar.CanAssign`)
  - ลากช่องไปช่อง = สลับ / ลากออกนอกแถบ = เอาทางลัดออก
  - ระหว่างเปิดร้าน: แถบจาง ใช้ของและลากไม่ได้ (เลือกช่องได้) / ระหว่างเปิดกระเป๋าและหน้าสรุปวัน: ไม่รับปุ่มใช้ของ

**การควบคุม (ปุ่มเดียวกันทำงานตามของในช่องที่เลือก)**

| ปุ่ม | ถืออาหาร | ถือเครื่องมือ | ถืออาวุธ |
|---|---|---|---|
| คลิกซ้าย | – | ใช้เครื่องมือ | Attack |
| คลิกขวา | กิน | – | Heavy Attack |
| F (ค้าง) | – | – | Block |
| R | – | – | Skill |
| ค้าง F แล้วกด R | – | – | Ultimate (ใช้ FEVER เต็มหลอด) |

- E = ใช้งานของในฉาก (เดิม) / I = กระเป๋า / ปุ่ม Debug เร่งเวลาย้ายจาก F เป็น **F7** แล้ว
- คลิกที่ชี้อยู่บน UI จะไม่นับเป็นการใช้ของ

**ค่าสถานะ**
- **Health 100:** ไม่ฟื้นเอง ฟื้นจากอาหารหรือการนอน (Health หมดยังไม่มีบทลงโทษ)
- **Stamina 100:** ใช้กับเครื่องมือและการต่อสู้ ฟื้นเองหลังไม่ได้ใช้ 1.5 วินาที (ยืนเฉยหรือเดินก็ฟื้น) หมดแล้ว = ใช้เครื่องมือ/โจมตีไม่ได้จนกว่าจะฟื้น (ไม่สลบ ไม่เดินช้า)
- **Mana 50:** **ไม่ฟื้นเอง** ได้จาก อาหาร/โพชั่น และตอน Attack / Heavy Attack โดนมอนสเตอร์ และ Block การโจมตีได้ ฟื้นช้าๆ ได้เฉพาะจาก "แหล่งฟื้น Mana" = อาหารบอส, ของสวมใส่, ความชำนาญ (proficiency) ของแต่ละอาวุธแบบ Stardew (`SetManaRegenSource`)
- **FEVER 200:** หลอดท่าไม้ตาย ได้จาก Attack +1 / Heavy +2 / Block +1 และอาหารบางอย่าง (สูตรลับทุกจาน) **เก็บข้ามวันได้** ส่วน Skill ให้ FEVER เท่าไรค่อยตัดสินตอนมีแอนิเมชัน (มี hitbox หลายแบบ)
- ตื่นนอน (ทั้งนอนปกติและสลบ): Health / Stamina / Mana เต็ม FEVER คงเดิม (บทลงโทษสลบทำทีหลัง)
- ค่าสูงสุดเพิ่มถาวรได้ (อาหารบอส) ผ่าน `SetBonusMax`
- ต้นทุนท่าตอนนี้เป็นค่าชั่วคราวใน `PlayerActionController`: Attack 5 ST / Heavy 15 ST / Block 3 ST / Skill 20 MP / เครื่องมือ 4 ST
- Equipment Slots บนตัวละคร: หมวก เสื้อ รองเท้า แหวน 2 ช่อง
- ระบบต่อสู้ (เฟส 3): มอนสเตอร์ในป่าและถ้ำ บอสประจำพื้นที่ อาวุธ ดาบ / คทา / ธนู รางวัลบอสคือของวิเศษ อาวุธ หรืออาหารเพิ่มค่าสถานะถาวร

## 7. สคริปต์ที่มีอยู่ (Assets/Scripts)

```
Inventory.cs          กระเป๋าวิเศษ: Dictionary<ItemData,int>, เพดานต่อชนิดตามระดับ (stackCapsByLevel), discovered,
                      AddItem (เพิ่มทั้งหมดหรือไม่เพิ่มเลย), RemoveItem, GetCount, IsDiscovered, UpgradeBag,
                      SetItems / SetDiscovered / SetBagLevel (โหลดเซฟ), OnChanged, OnItemDiscovered
ItemData.cs           itemId, itemName (EN), nameTh, icon, description, itemType (Ingredient/Dish/Tool/Weapon/Equipment/Accessory),
                      category, source, sellPrice, restoreHealth/Stamina/Mana/Fever, IsEdible  (enum เพิ่มต่อท้ายเท่านั้น)
                      หมายเหตุ: IsEdible เช็กเฉพาะ Ingredient/Dish ถ้าเพิ่ม ItemType โพชั่นต้องแก้ตรงนี้ด้วย
ItemDatabase.cs       GetById, RefreshFromProject (Editor)
ForageItem.cs         IInteractable, Collect(Inventory)
ForageSpawnZone.cs    โซนเกิดของ
PlayerController.cs   เดิน 8 ทิศ, CanMove, SetCanMove
Player/PlayerStats.cs       Health/Stamina/Mana/Fever, Restore, TrySpend, TryUseFever, TakeDamage, RegisterCombatHit(CombatHitType),
                            TryEat(item, inventory), SetManaRegenSource, SetBonusMax, SetFever, RefillForNewDay (ฟัง OnDayEndFinished),
                            events OnStatsChanged/OnHealthDepleted/OnFeverFull, เมนู Debug หลายตัว (คลิกขวาหัวคอมโพเนนต์)
Player/Hotbar.cs            10 ช่อง (ItemData), SelectedIndex, CanAssign, SetSlot (ย้ายถ้าซ้ำ), ClearSlot, SwapSlots, Select/SelectOffset,
                            CaptureSlotIds / SetAllSlots (ใช้ตอนเซฟ/โหลด), Debug: Give Test Items + Fill Hotbar,
                            Debug: Give Item By Id (กรอกช่อง Debug Item Id / Debug Amount ใน Inspector ก่อน)
Player/PlayerActionController.cs  รับปุ่มเลือกช่องและใช้ของตามตารางควบคุม, IsBlocking, cooldown, events OnAttack/OnHeavyAttack/
                            OnBlockStarted/OnBlockEnded/OnSkill/OnUltimate/OnToolUsed/OnItemEaten (ให้ระบบต่อสู้เฟส 3 มาฟัง)
Cooking/RecipeData.cs     recipeId, recipeName, nameTh, icon, cuisine, course, isSecret, inDemo, dishItem,
                          ingredients (≤4), fullIngredientsText, sellPrice, GetRequiredCounts()
Cooking/RecipeDatabase.cs GetById, CanCook, GetMaxServings, GetCookable, RefreshFromProject
Cooking/RecipeBook.cs     สูตรที่รู้ (บน Player): เริ่มด้วยสูตร inDemo, Knows, Learn, SetKnown, Debug: Learn All Recipes
Interaction/IInteractable.cs, PlayerInteractor.cs (กด E ใช้ของที่ใกล้สุดชิ้นเดียว), PlayerTeleporter.cs (วาร์ป + สลับ Confiner),
            Door.cs, ShopCounter.cs, Bed.cs (กด E สองครั้ง)
Systems/GameClock.cs      singleton, วันไม่จบเอง, passOutHour 27 (03:00), ช่วง Morning/Afternoon/Evening/Night,
                          EndDay(), AdvanceTo(), OnPassedOut, Debug: T เวลา, P หยุด, F ค้างเร่ง, N ข้ามช่วง
Systems/ShopManager.cs    ShopState (BeforeOpening/CanOpen/Serving/ClosedForDay), TryOpenShop, Register/UnregisterCustomer,
                          OnShopOpened/OnServiceTimeUp/OnShopClosed, F8 ปิดร้านทันที
Systems/DayEndManager.cs  นอน/สลบ → สรุป → EndDay (เซฟ) → ตื่นที่เตียง/โรงพยาบาล
Systems/ForageSpawner.cs  ของงอกรายวัน, CaptureState/RestoreState
Systems/Save/SaveData.cs     version 3: day, inventory, groundItems, discoveredItems, knownRecipes, bagLevel,
                             [v3] hotbarItems (itemId 10 ช่อง, ว่าง = ""), fever, bonusMaxHealth/Stamina/Mana
Systems/Save/SaveManager.cs  เซฟตอน OnDayEnded, โหลดตอนเปิดเกม, รองรับเซฟ v1/v2 (Hotbar ว่าง, FEVER 0, โบนัส 0), F9 ลบเซฟ
                             ผูก playerHotbar / playerStats (ว่างไว้จะหาบนตัว Player เอง) โหลดแล้วตั้งโบนัส → FEVER → RefillForNewDay
                             ไม่เซฟค่าปัจจุบัน HP/ST/MP (โหลด = วันใหม่ เต็มเสมอ) / ไอเทม Hotbar ต้องอยู่ใน ItemDatabase
UI/ClockUI.cs, NotificationUI.cs (NotificationUI.Post), DaySummaryUI.cs
UI/InventoryUI.cs      กระเป๋า 5 แท็บ: I เปิด/ปิด, Esc ปิด, Q/E เปลี่ยนแท็บ, คลิกช่องดูรายละเอียด,
                       hideUnknownSecretRecipes (ซ่อนสูตรลับที่ยังไม่รู้ + จานของสูตรนั้นในแท็บ Dishes ผ่าน IsHiddenSecretDish)
UI/InventorySlotUI.cs  SlotState (Owned/DiscoveredEmpty/Undiscovered), ตัวเลขมุมขวาบน, SelectionFrame, OnClicked,
                       ลากไป Hotbar ได้ (ช่องที่ลากไม่ได้ ส่งการลากต่อให้ Scroll View)
UI/StatsUI.cs          แถบ HP/ST/MP/FEVER (ยืด/หดด้วย Anchor Max X ไม่ใช้ Image Filled), FEVER เต็มแล้วกะพริบ
UI/HotbarUI.cs         แถบ 10 ช่อง + ชื่อไอเทมที่เลือก, จางตอนเปิดร้าน (CanvasGroup)
UI/HotbarSlotUI.cs     ไอคอน/จำนวน/เลขปุ่ม/กรอบเลือก, คลิกเลือก, OnDrop (จากกระเป๋า = ใส่, จาก Hotbar = สลับ), ลากออก = เอาออก
UI/DragIconUI.cs       ไอคอนลอยตามเมาส์ตอนลาก สร้างตัวเองบน Canvas หลัก (ไม่ต้องวางในฉาก)
Editor/RecipeImporter.cs           Tools > Second Serving > Import Recipes & Ingredients
Editor/InventoryLayoutBuilder.cs   Tools > Second Serving > Build Inventory Window Layout (จัดหน้าต่างกระเป๋าอัตโนมัติ)
Editor/HudLayoutBuilder.cs         Tools > Second Serving > Build Stats HUD (มุมซ้ายบน) / Build Hotbar HUD (กลางล่าง)
                                   ใส่คอมโพเนนต์ที่ขาดให้ Player และผูกช่องให้ กดซ้ำได้
```

`SaveSync.jslib` (FS.syncfs สำหรับเว็บ) ยังไม่เจอใน Scripts/Systems/Save ต้องเช็กตอนทำเดโมเว็บว่าย้ายไป `Assets/Plugins/WebGL` แล้วหรือยัง

## 8. การตั้งค่าในฉาก (SampleScene)

- **Player:** Box Collider 2D, Rigidbody2D, Inventory, RecipeBook, PlayerController, PlayerInteractor, PlayerTeleporter, PlayerStats, Hotbar, PlayerActionController
- **กล้อง:** Main Camera (Cinemachine Brain) + PlayerFollowCam (Position Composer, Ortho 5, Confiner 2D)
- **Grid:** Ground, Obstacles, CameraBoundsMap, CameraBounds_Shop
- **ร้าน:** ประตู 4 บาน (Door), ShopCounter, Bed และจุดวาร์ป ShopEntry_Front/Back, Outside_Front/Back, BedWakePoint, HospitalWakePoint
- **ระบบ:** GameClock, ShopManager, DayEndManager, ForageSpawner, SaveManager (ItemDatabase, Inventory, ForageSpawner, RecipeDatabase, RecipeBook, Hotbar, PlayerStats)
- **Canvas:** ClockUI, NotificationUI, DaySummaryUI, InventoryUI → InventoryWindow (TitleText, ProgressText, BagLevelText, TabBar ที่มีปุ่ม 5 แท็บ, SlotScrollView/Viewport/Content, DetailPanel, SlotGrid เดิมถูกปิดไว้),
  **StatsHUD** (StatsUI, แถว HealthRow/StaminaRow/ManaRow/FeverRow), **HotbarHUD** (HotbarUI, Slot_1–Slot_10, SelectedNameText)
- หน้าต่างกระเป๋า 1500x860 กลางจอ (ขอบล่างอยู่ที่ y=110) Hotbar สูง 72 ห่างขอบล่าง 16 จึงไม่ทับกัน
- **ของทดสอบ:** ผู้ใช้สร้าง `TestSword` (Weapon) และ `TestHoe` (Tool) ไว้ใน `Assets/Items`
- **กล้อง:** Ortho Size 5 (จอประมาณ 17.8 x 10 หน่วย) Position Composer Damping 0.5, Confiner 2D Damping 0.5 / Slowing Distance 5
  - เคยมีปัญหากล้องกระโดดในร้าน เพราะ `CameraBounds_Shop` เป็น 2 ห้องเชื่อมด้วยช่องแคบกว่าจอ (Confiner แยกเป็น 2 เกาะ) ผู้ใช้รู้วิธีแก้และจัดการเอง
  - `CameraBoundsMap` กว้าง 17 หน่วย แคบกว่าจอ กล้องข้างนอกจึงเลื่อนซ้ายขวาไม่ได้ ตอนขยายแผนที่ต้องกว้างกว่า 18 หน่วย
- **Prefab:** `Assets/Prefabs/InventorySlot` (มี SelectionFrame), `Assets/Prefabs/Mushroom_Pickup`

## 9. Roadmap

ประมาณการคิดที่ 1 วันทำงาน ≈ 3–4 ชั่วโมง รวมทั้งโปรเจกต์ประมาณ **14–21 เดือน** (เฟส 2 เหลือประมาณ 8–11 สัปดาห์)

**✅ เสร็จแล้ว**
- เฟส 1: เดิน, เก็บของ, กระเป๋า, นาฬิกา, กล้อง/Tilemap, ของงอกรายวัน, เซฟ/โหลด
- 2.1 สลับโหมดสำรวจ/เปิดร้าน
- 2.2 สูตรอาหาร 87 สูตร + ตัว import
- 2.7 (ครึ่งแรก) นอน / สลบ / หน้าสรุป / เซฟ
- **ขั้น A:** กระเป๋า Collectibles 5 แท็บ, ItemType, จานอาหาร, สมุดสูตร, เซฟ v2, เครื่องมือจัด UI
- **ขั้น B1:** PlayerStats (Health/Stamina/Mana/FEVER) + StatsUI + ค่าที่ฟื้นเมื่อกินในตาราง/ตัว import + ปุ่มเร่งเวลา F → F7
- **ขั้น B2:** Hotbar 10 ช่อง + HotbarUI + เลือกช่อง + กิน + รับปุ่ม Attack/Heavy/Block/Skill/Ultimate (ยังไม่มีผลจริง)
- **ขั้น B3:** ลากของจากกระเป๋าลง Hotbar, สลับช่อง, ลากออก
- **ขั้น B4:** เซฟ v3 (ช่อง Hotbar, FEVER, ค่าสูงสุดถาวร) + ซ่อนจานของสูตรลับที่ยังไม่รู้ในแท็บ Dishes + Debug ให้ไอเทมตาม itemId

**⬜ เฟส 2 (เหลือประมาณ 8–11 สัปดาห์)**

| ขั้น | งาน | เวลา |
|---|---|---|
| **C** ← ถัดไป | Equipment Slots (UI และข้อมูล ยังไม่มีผลกับค่าสถานะ) | 2–3 วัน |
| D = 2.3 | เมนูประจำวัน (DailyMenu + MenuSelectionUI) ตามระบบผสม | 3–4 วัน |
| 2.4 | ลูกค้า AI (เลือกระบบหาเส้นทางก่อน) + ลูกค้าอยากกินเมนูเฉพาะ | 8–12 วัน |
| 2.5 | ทำอาหารและเสิร์ฟ + เตาในครัวที่ใช้ได้ทุกเวลา + โหมดลองผสม (ค้นพบสูตร Rumor) | 9–13 วัน |
| 2.6 | เงิน ชื่อเสียง ราคาจากต้นทุน ความชำนาญ โบนัสความหลากหลาย | 3–4 วัน |
| 2.6b | ร้านค้าพื้นฐาน (ซื้อ/ขาย) + อัปเกรดกระเป๋า | 4–6 วัน |
| 2.6c | ของป่าสำหรับสูตรเริ่มต้น 12 ชนิด (prefab + โซนงอก) | 2–3 วัน |
| 2.7 | สรุปรายวัน (ส่วนที่เหลือ) | 1–2 วัน |
| – | เล่นทดสอบทั้งวงจร | 4–5 วัน |

🎯 จบเฟส 2 = Vertical Slice เล่นครบวงจรด้วย 8 สูตรเริ่มต้น

**⬜ เฟส 3 (ประมาณ 8–11 เดือน)**
NPC และบทสนทนา, ความสนิทและรสนิยมอาหาร, ข่าวลือและ side story สูตรลับ (ใช้คอลัมน์ Rumor / Story Notes), ฤดูกาลและอากาศ, เควสต์และเทศกาล, แบ่งแผนที่เป็นโซน, พ่อค้าเร่, ร้านค้าเต็มรูปแบบ, ปลูกพืช, เลี้ยงสัตว์, เครื่องแปรรูป, โซนทะเลและตกปลา, **ระบบต่อสู้ + มอนสเตอร์ + บอส + ถ้ำ (8–12 สัปดาห์)** (ต่อจาก PlayerActionController: hitbox, ดาเมจ, เรียก `RegisterCombatHit`, ต้นทุนท่าย้ายไปอยู่ที่ข้อมูลอาวุธ, FEVER จาก Skill), **ความชำนาญ (proficiency) แต่ละอาวุธแบบ Stardew** (ให้แหล่งฟื้น Mana), โพชั่น, ของสวมใส่ที่มีผลกับค่าสถานะ, บทลงโทษตอนสลบ/Health หมด, สิ่งประดิษฐ์ช่วงปลายเกม

**⬜ เฟส 4 (ประมาณ 4–7 เดือน)**
ภาพ (ไอคอนประมาณ 170 ชิ้น), เสียง, เมนูหลัก, ปรับสมดุล, ฟอนต์ไทย, เช็กภาพสั่น (Interpolate), เดโมเว็บ (SaveSync.jslib), หน้า Steam (ควรขึ้นช่วงกลางเฟส 3), ปล่อยเกม

## 10. ขั้นถัดไป: ขั้น C – Equipment Slots (ยังไม่เริ่ม)

ขั้น B1–B4 ผู้ใช้ทดสอบใน Unity ผ่านแล้ว (B4: เซฟ/โหลด Hotbar + FEVER ผ่าน, ซ่อนจานสูตรลับในแท็บ Dishes ผ่าน)

**ขั้น C ต้องทำ (UI และข้อมูลเท่านั้น ยังไม่มีผลกับค่าสถานะ)**
- ช่องสวมใส่บนตัวละคร: หมวก / เสื้อ / รองเท้า / แหวน 2 ช่อง (ตามข้อ 6)
- ต้องตัดสินใจกับผู้ใช้ก่อนเขียนโค้ด (plan first) เช่น
  - แยกประเภทช่องอย่างไร (ItemType ตอนนี้มีแค่ Equipment / Accessory ต้องมี field บอกว่าเป็นหมวก/เสื้อ/รองเท้า/แหวนไหม)
  - ใส่/ถอดด้วยวิธีไหน (ลากจากกระเป๋าแบบ Hotbar หรือคลิกในแผงรายละเอียด) และ UI อยู่ตรงไหน (ในหน้าต่างกระเป๋า หรือหน้าต่างแยก)
  - ของที่สวมอยู่ยังนับอยู่ในกระเป๋าไหม (Hotbar เป็นทางลัด ไม่ย้ายของออก)
- เซฟ: ต้องเพิ่มช่องที่สวมใส่ใน SaveData → **version 4** (เซฟ v1–v3 โหลดแล้วไม่ได้สวมอะไร)
- ตอนผลกับค่าสถานะจริง (เฟส 3) ใช้ `PlayerStats.SetBonusMax` / `SetManaRegenSource` ได้
- จบขั้น C แล้ว: สรุป roadmap ในแชท + อัปเดต HANDOFF.md แล้วไปขั้น D (เมนูประจำวัน)

**วิธีทดสอบที่ใช้อยู่**
- คลิกขวาที่หัวคอมโพเนนต์ (PlayerStats / Hotbar) ระหว่าง Play เพื่อใช้เมนู Debug
- อยากได้ไอเทมใดๆ: ที่คอมโพเนนต์ Hotbar กรอก `Debug Item Id` (+ `Debug Amount`) แล้วคลิกขวาหัว > `Debug: Give Item By Id` (หาจาก ItemData ทุกตัวในโปรเจกต์)
- ของที่กินดิบไม่ได้ใช้ทดสอบได้ เช่น เห็ด (ทุกค่าเป็น 0)
- `Give Test Items + Fill Hotbar` อาจแจกจานสูตรลับมาด้วย จานนั้นจะถูกนับว่าเคยได้แล้ว (ไม่ถูกซ่อน) ทดสอบใหม่จากศูนย์ให้กด F9 ลบเซฟ
- ไอเทมใหม่ (เช่น TestSword) ต้องกด Find All Items ที่ ItemDatabase ก่อน ไม่งั้นโหลด Hotbar กลับไม่ได้
