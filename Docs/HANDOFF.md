# Second Serving – HANDOFF

> ไฟล์นี้สรุปสถานะโปรเจกต์ให้ Claude ในแชทใหม่อ่านต่อได้ทันที
> อัปเดตล่าสุด: 22 พฤศจิกายน 2026 (จบขั้น 2.4b เมนูนิยมรายวัน + ลูกค้าถามหาเมนูนิยม + อารมณ์หลังกิน / ถัดไปขั้น 2.5 ทำอาหารและเสิร์ฟ)
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
- รอบขาย: จับเวลา 3 นาทีจริง (`ShopManager.serviceDurationSeconds` = 180) นาฬิกาหยุดระหว่างขาย ปิดร้านแล้วข้ามไป 22:00 เสมอ
  - หมดเวลา = ไม่รับลูกค้าใหม่ / **ขายหมดทุกเมนูก่อนหมดเวลา = หยุดรับลูกค้าทันที** (`EndServiceBySellOut`)
  - **ร้านปิดทันทีเมื่อไม่มีลูกค้าที่ยังไม่ได้อาหาร** (รวมคนที่กำลังเดินไปนั่ง) คนที่ได้อาหารแล้วกินต่อจนเสร็จแล้วเดินออกเองระหว่างโหมดสำรวจ
  - ระหว่างเปิดร้าน **ซ่อนนาฬิกาปกติ** แสดงตัวนับถอยหลัง `นาที:วินาที.เสี้ยววินาที` กลางบนจอ (เหลือง < 60 วิ, แดง+เต้น < 20 วิ) + สถานะ `Dishes left N · Waiting N` / `SOLD OUT!` / `TIME'S UP!`
  - `Waiting` นับเฉพาะลูกค้าที่**นั่งแล้วและสั่งแล้ว** (เสิร์ฟได้ทันที) คนที่ยังเดิน/คิดเมนู/ได้อาหารแล้วไม่นับ
- **ความยาว 1 วัน** (10 นาทีเกม = 7 วินาทีจริง แบบ Stardew): 06:00–17:00 ≈ 7.7 นาที, ร้าน 3 นาที, 22:00–03:00 ≈ 3.5 นาที → วันปกติ ≈ 11 นาที สูงสุด ≈ 14 นาที / ผู้ใช้เห็นด้วยว่าค่อยปรับความยาวรอบขาย + ความถี่ลูกค้าหลังจบ 2.5
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
  - (ขั้น D) เลือกเมนูโดย**กด E ที่เคาน์เตอร์ช่วง 17:00–19:00** → หน้าต่างเลือกเมนู → ปุ่ม Open Shop (ต้องมีอย่างน้อย 1 จาน) / Esc หรือ Cancel ปิด
  - ของที่จอง**ยังอยู่ในกระเป๋า** ช่วงล็อก = ตั้งแต่เปิดร้านถึงปิดร้าน (ซึ่งกระเป๋า/Hotbar ใช้ไม่ได้อยู่แล้ว) จึงไม่ได้แก้ตัวกระเป๋า
  - เสิร์ฟใช้จานที่ทำไว้แล้วก่อน แล้วค่อยหักวัตถุดิบ / สองเมนูใช้วัตถุดิบเดียวกัน = max ของแต่ละช่องหักส่วนที่ช่องอื่นจอง
  - **จำเมนูเมื่อวานเป็นค่าเริ่มต้น** (เซฟ v5) เปิดหน้าต่างแล้วจำนวนถูกลดตามของที่มี / สูตรที่ไม่รู้ถูกล้าง
  - ระหว่างเปิดหน้าต่างเมนู: นาฬิกาหยุด เดินไม่ได้ เปิดกระเป๋าไม่ได้ ใช้ Hotbar ไม่ได้
- **ลูกค้า (ขั้น 2.4a)**
  - หาเส้นทางด้วย **Grid A* จาก Tilemap** (4 ทิศ) ใช้ต่อกับ NPC เฟส 3 ได้
  - ที่นั่ง = **โต๊ะ + เก้าอี้หลายตัว** (prefab Table_2Seats) ตอนนี้ 1 คนต่อ 1 ที่ กลุ่มลูกค้าทำทีหลัง
  - **ลูกค้าสั่งได้เฉพาะเมนูประจำวัน** เมนูหมด = สั่งจานอื่นที่ยังเหลือ หมดทั้งร้าน = เดินออก (ผู้ใช้ไม่อยากให้ลูกค้าอยากกินเมนูที่ไม่ได้ขาย)
  - ความอดทนสุ่ม 35–50 วินาทีต่อคน (ตั้งใจให้ต่างกัน ต่อไปใช้กับลูกค้าหลายแบบได้) หมดเวลา = เดินออก
  - **เสิร์ฟชั่วคราว: กด E ที่ลูกค้า** (ขั้น 2.5 จะเปลี่ยนเป็นทำที่เตา → ถือจาน → เสิร์ฟ ผู้ใช้อยากให้มีเฟสทำอาหารจริงจัง)
  - **จ่ายเงินตอนได้อาหาร** (ไม่ใช่ตอนกินเสร็จ) / เดินออกเร็วกว่าเดินเข้า 1.6 เท่า
  - **เมนูนิยมรายวัน (ขั้น 2.4b)** `PopularDishes` บนวัตถุ DailyMenu
    - สุ่มวันละ **2 จาน** (เพิ่มได้ทีหลังจากการอัปเกรดร้าน `SetBonusCount`) แต่ละจานมีโอกาส **25%** เป็นสูตรที่ยังไม่รู้ (ไม่รวมสูตรลับ) สูตรลับที่รู้แล้วเป็นได้ / พยายามไม่ซ้ำเมื่อวาน / **ไม่เซฟ** (โหลด = วันใหม่ สุ่มใหม่)
    - สูตรที่ยังไม่รู้แสดงเป็น**คำใบ้ชั่วคราว** `??? (Japanese dessert)` / ลูกค้าถามหาเป็น `that Japanese dessert` → ผู้ใช้อยากให้**เฟส 3 เปลี่ยนเป็นคำใบ้ผ่าน NPC / จุดให้ไปหาสูตร / เนื้อเรื่องหลัก-รอง** (คิดตอนมี NPC หลักหรือช่วงขัดเกลาภาพ) แก้ที่ `GetDisplayName` / `GetRequestName` จุดเดียว
    - หน้าต่างเลือกเมนู: บรรทัด `Popular today: * …` สีทองใต้หัวข้อ + รายการสูตรที่นิยมขึ้นก่อน มี `*` สีทอง (ใช้ `*` เพราะไม่แน่ใจว่าฟอนต์ TMP มี ★)
    - เมนูนิยมที่มีขาย ถูกสั่งบ่อยกว่า **3 เท่า** / ลูกค้าที่สั่งเมนูนิยมมี `*` หน้าชื่อจานเหนือหัว
    - **ไม่ใส่เมนูนิยมเลยไม่มีบทลงโทษ** (ผู้ใช้เลือก: ใส่เมนูนิยมต้องไม่มีทางแย่กว่าไม่ใส่) ไม่มีลูกค้าเดินออกเพราะเรื่องเมนูนิยม
    - **ลูกค้าถามหาเมนูนิยมที่ร้านไม่มี** (ไม่ได้ใส่ หรือหมดแล้ว) โอกาส 25% ต่อคน: ขึ้น `Got Som Tam? [E] 40s` → **ผู้เล่นเดินไปกด E เอง** (ผู้ใช้อยากให้เป็นการโต้ตอบกับลูกค้า) → แจ้งเตือนคำพูดผู้เล่น "Sorry, no … today. Try our menu, it's tasty!" → ลูกค้า `OK, I'll try …!` เลือกจานจากเมนูวันนี้ → รอเสิร์ฟ (ความอดทนนับต่อ ไม่รีเซ็ต) / ลูกค้าที่ถามหาอยู่นับใน Waiting และร้านยังไม่ปิดจนกว่าจะตอบ
  - **อารมณ์หลังกินและความพอใจ (ขั้น 2.4b)** สุ่มตอนได้อาหาร แสดงตอนกินเสร็จ
    - ลูกค้าที่เปลี่ยนจาน: **ชอบมาก 20%** (`Loved it!!` 1.5) / **อร่อยดี 40%** (`Tasty, thanks!` 1.0) / **เฉยๆ 40%** (`It was okay.` 0.7) โอกาสชอบมาก**เพิ่มได้จากการอัปเกรดร้าน** (`SetLovedChanceBonus`) ส่วนที่เหลือแบ่งเท่ากัน
    - ได้กินเมนูนิยม = ชอบมาก 1.5 / จานปกติ = อร่อยดี 1.0 / ไม่ได้กิน = 0
    - เก็บที่ `Customer.Satisfaction` / `Mood` รวมรายวันใน CustomerManager ให้ 2.6 ใช้คิดเงิน/ชื่อเสียง
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
- **Shift = สลับเดิน/วิ่ง (toggle)** วิ่งเร็ว 1.7 เท่า หัก Stamina 1 ทุก 1 วินาทีเฉพาะตอนขยับตัว Stamina ไม่พอ = กลับเป็นเดินเอง (ปรับได้ที่ PlayerController หัวข้อ Run) ระหว่างเปิดกระเป๋า/หน้าต่างเมนู Shift ไม่สลับวิ่ง
- Scroll เมาส์: ผู้ใช้อยากได้ **ขึ้น = ไปทางซ้าย / ลง = ไปทางขวา** → ติ๊ก `Invert Scroll` ที่ PlayerActionController บน Player (ไม่ได้แก้โค้ด)
- คลิกที่ชี้อยู่บน UI จะไม่นับเป็นการใช้ของ

**ค่าสถานะ**
- **Health 100:** ไม่ฟื้นเอง ฟื้นจากอาหารหรือการนอน (Health หมดยังไม่มีบทลงโทษ)
- **Stamina 100:** ใช้กับเครื่องมือและการต่อสู้ ฟื้นเองหลังไม่ได้ใช้ 1.5 วินาที (ยืนเฉยหรือเดินก็ฟื้น) หมดแล้ว = ใช้เครื่องมือ/โจมตีไม่ได้จนกว่าจะฟื้น (ไม่สลบ ไม่เดินช้า)
- **Mana 50:** **ไม่ฟื้นเอง** ได้จาก อาหาร/โพชั่น และตอน Attack / Heavy Attack โดนมอนสเตอร์ และ Block การโจมตีได้ ฟื้นช้าๆ ได้เฉพาะจาก "แหล่งฟื้น Mana" = อาหารบอส, ของสวมใส่, ความชำนาญ (proficiency) ของแต่ละอาวุธแบบ Stardew (`SetManaRegenSource`)
- **FEVER 200:** หลอดท่าไม้ตาย ได้จาก Attack +1 / Heavy +2 / Block +1 และอาหารบางอย่าง (สูตรลับทุกจาน) **เก็บข้ามวันได้** ส่วน Skill ให้ FEVER เท่าไรค่อยตัดสินตอนมีแอนิเมชัน (มี hitbox หลายแบบ)
- ตื่นนอน (ทั้งนอนปกติและสลบ): Health / Stamina / Mana เต็ม FEVER คงเดิม (บทลงโทษสลบทำทีหลัง)
- ค่าสูงสุดเพิ่มถาวรได้ (อาหารบอส) ผ่าน `SetBonusMax`
- ต้นทุนท่าตอนนี้เป็นค่าชั่วคราวใน `PlayerActionController`: Attack 5 ST / Heavy 15 ST / Block 3 ST / Skill 20 MP / เครื่องมือ 4 ST
- **Equipment Slots 8 ช่อง** (ขั้น C):
  - ช่องเพิ่มค่าสถานะ 4 ช่อง: Shoes, Ring ×2, Necklace (ยังไม่มีผล เฟส 3 จะใช้ `SetBonusMax` / `SetManaRegenSource`)
  - ช่องแฟชัน 4 ช่อง (แค่หน้าตา): Head, Top, Bottom, Back (กระเป๋า ปีก ผ้าคลุม) → หมวกและเสื้อเป็นแฟชัน ไม่เพิ่มค่าสถานะ
  - แผงอยู่ในหน้าต่างกระเป๋ามุมขวาบน แสดงเฉพาะแท็บ Equipment / Accessory (แผงรายละเอียดเลื่อนลงไปอยู่ใต้)
  - ใส่/ถอดด้วยการลากอย่างเดียว: ลากจากกระเป๋า = สวม, ลากช่องไปช่อง = สลับ (แหวน 1 ↔ 2), ลากออก = ถอด
  - สวม = **ย้ายของ 1 ชิ้นออกจากกระเป๋า** / ถอด = กลับเข้ากระเป๋า (กระเป๋าเต็มเพดาน = ถอดไม่ได้) / สวมทับ = ของเดิมกลับเข้ากระเป๋า
  - ItemData มี `equipType` (None/Shoes/Ring/Necklace/Head/Top/Bottom/Back) แนะนำ: รองเท้าและแฟชัน = ItemType.Equipment, แหวน/สร้อย = Accessory
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
Player/PlayerEquipment.cs   enum EquipSlot (Shoes, Ring1, Ring2, Necklace, Head, Top, Bottom, Back), GetSlot, CountEquipped,
                            IsStatSlot, GetAcceptedType, CanEquipInSlot, TryEquip / TryUnequip / TrySwap, CaptureSlotIds / SetAllSlots, OnChanged,
                            Debug: Give All Equippable Items / Print Equipment / Unequip All
Player/PlayerActionController.cs  รับปุ่มเลือกช่องและใช้ของตามตารางควบคุม, IsBlocking, cooldown, events OnAttack/OnHeavyAttack/
                            OnBlockStarted/OnBlockEnded/OnSkill/OnUltimate/OnToolUsed/OnItemEaten (ให้ระบบต่อสู้เฟส 3 มาฟัง)
Cooking/RecipeData.cs     recipeId, recipeName, nameTh, icon, cuisine, course, isSecret, inDemo, dishItem,
                          ingredients (≤4), fullIngredientsText, sellPrice, GetRequiredCounts()
Cooking/RecipeDatabase.cs GetById, CanCook, GetMaxServings, GetCookable, RefreshFromProject
Cooking/DailyMenu.cs      singleton (วัตถุ DailyMenu ในฉาก): MenuEntry (Recipe, Planned, Remaining, Sold), SlotCount = base 3 + SetBonusSlots,
                          TryAddRecipe, ClearEntry, SetPlanned/ChangePlanned, GetMaxServings, GetAvailableServings, GetDishesInBag,
                          GetReservedCount, Revalidate, TryConfirmAndOpenShop, IsActive, CanServe(recipe), TryTakeServing(recipe) ← ขั้น 2.5 เรียกตอนเสิร์ฟ,
                          GetTotalSold, ฟัง OnShopClosed, CaptureMenu/RestoreMenu, events OnMenuChanged / OnServingTaken,
                          Debug: Print Menu / Serve One Of Each / Add Menu Slot
Cooking/RecipeBook.cs     สูตรที่รู้ (บน Player): เริ่มด้วยสูตร inDemo, Knows, Learn, SetKnown, Debug: Learn All Recipes
Cooking/PopularDishes.cs  [2.4b] singleton (บนวัตถุ DailyMenu): Today, IsPopular, IsKnown, GetDisplayName (คำใบ้ ??? ในหน้าต่างเมนู),
                          GetRequestName (คำที่ลูกค้าถามหา), GetSummaryText, Marker, SetBonusCount, Reroll, สุ่มตอน OnDayStarted + สุ่มเองตอนถูกเรียกครั้งแรกของวัน,
                          event OnPopularChanged, Debug: Reroll Popular Dishes / Print Popular Dishes
Navigation/GridPathfinder.cs  singleton (บน CustomerManager): A* บนช่อง Grid, walkableTilemaps (Ground), blockingTilemaps (Obstables),
                              Collider ทึบ = สิ่งกีดขวาง ยกเว้น Trigger / Rigidbody Dynamic / Collider ของ Tilemap (ข้าม CameraBounds_*),
                              FindPath, IsWalkable (cache), ClearCache, TryFindNearestWalkable, วาดเส้นทางล่าสุดเป็นเส้นฟ้าใน Scene view
Shop/Seat.cs              ที่นั่ง 1 ที่: TryReserve / Release / ForceRelease, SitPosition, Gizmo เขียว = ว่าง แดง = มีคน
Shop/Table.cs             โต๊ะ (Collider ทึบ) รวม Seat ที่เป็นลูก, FreeSeatCount
Shop/Customer.cs          IInteractable: WalkingToSeat → Ordering → (Asking) → WaitingForFood → Eating → Leaving, NeedsService, TryServe (E = เสิร์ฟ + จ่ายเงิน),
                          [2.4b] Asking = ถามหาเมนูนิยม → E = RecommendTodaysMenu (เปลี่ยนจาน), OrderKind (Normal/Popular/Substitute/Asking),
                          Mood (Loved/Good/Meh) + Satisfaction ตั้งตอนเสิร์ฟ, AskedFor, ข้อความทั้งหมดแก้ได้ใน prefab (หัวข้อ Text / Mood text)
                          LeaveBecauseShopClosed, RemoveImmediately, ข้อความ TextMeshPro เหนือหัว (ชื่อจาน + วินาทีที่เหลือ)
Shop/CustomerManager.cs   singleton: ปล่อยลูกค้าจาก entryPoint (ShopEntryFront) ทุก 8–14 วิ เมื่อมีที่นั่งว่าง + FreeServings > 0,
                          TryPickOrder (ถามหาเมนูนิยมที่ไม่มี โอกาส askForPopularChance / หรือสุ่มจากจานที่เหลือ เมนูนิยม ×popularOrderWeight),
                          TryPickSubstitute (ตอนผู้เล่นแนะนำเมนู), DecideMood / GetSatisfaction(mood), SetLovedChanceBonus, LovedChance, ReleasePending,
                          สถิติ [2.4b] PopularServedToday / AskedForPopularToday / SubstituteServedToday / LovedToday / GoodToday / MehToday /
                          SatisfactionTotalToday / AverageSatisfactionToday (ลูกค้าที่ถามหายังจองจานไว้ใน notYetOrdered จนกว่าจะเปลี่ยนจาน),
                          waitingForService → ShopManager.Register/UnregisterCustomer (ปล่อยตอนได้อาหาร), SeatedWaitingCount,
                          ขายหมด → shop.EndServiceBySellOut(), ServedToday / LeftUnhappyToday / EarnedToday,
                          events OnCustomerSpawned / Served / Paid / Left, Debug: Spawn Customer Now / Serve All Waiting / Print Status
Interaction/IInteractable.cs, PlayerInteractor.cs (กด E ใช้ของที่ใกล้สุดชิ้นเดียว), PlayerTeleporter.cs (วาร์ป + สลับ Confiner),
            Door.cs, ShopCounter.cs (ช่วง CanOpen เปิด MenuSelectionUI / ไม่มีหน้าต่าง = เปิดร้านทันที), Bed.cs (กด E สองครั้ง)
Systems/GameClock.cs      singleton, วันไม่จบเอง, passOutHour 27 (03:00), ช่วง Morning/Afternoon/Evening/Night,
                          EndDay(), AdvanceTo(), OnPassedOut, Debug: T เวลา, P หยุด, F ค้างเร่ง, N ข้ามช่วง
Systems/ShopManager.cs    ShopState (BeforeOpening/CanOpen/Serving/ClosedForDay), TryOpenShop, Register/UnregisterCustomer,
                          OnShopOpened/OnServiceTimeUp/OnShopClosed, F8 ปิดร้านทันที
Systems/DayEndManager.cs  นอน/สลบ → สรุป → EndDay (เซฟ) → ตื่นที่เตียง/โรงพยาบาล
Systems/ForageSpawner.cs  ของงอกรายวัน, CaptureState/RestoreState
Systems/Save/SaveData.cs     version 5: day, inventory, groundItems, discoveredItems, knownRecipes, bagLevel,
                             [v3] hotbarItems (itemId 10 ช่อง, ว่าง = ""), fever, bonusMaxHealth/Stamina/Mana
                             [v4] equippedItems (8 ช่องตามลำดับ EquipSlot, ของที่สวมไม่อยู่ใน inventory)
                             [v5] menuRecipes / menuServings (เมนูที่ตั้งไว้ล่าสุดทีละช่อง)
Systems/Save/SaveManager.cs  เซฟตอน OnDayEnded, โหลดตอนเปิดเกม, รองรับเซฟ v1–v4 (Hotbar ว่าง, FEVER 0, โบนัส 0, ไม่ได้สวมอะไร, เมนูว่าง), F9 ลบเซฟ
                             ผูก playerHotbar / playerStats / playerEquipment / dailyMenu (ว่างไว้จะหาเอง) โหลดแล้วตั้งโบนัส → FEVER → RefillForNewDay
                             ไม่เซฟค่าปัจจุบัน HP/ST/MP (โหลด = วันใหม่ เต็มเสมอ) / ไอเทม Hotbar ต้องอยู่ใน ItemDatabase
UI/ClockUI.cs, NotificationUI.cs (NotificationUI.Post), DaySummaryUI.cs
UI/InventoryUI.cs      กระเป๋า 5 แท็บ: I เปิด/ปิด, Esc ปิด, Q/E เปลี่ยนแท็บ, คลิกช่องดูรายละเอียด,
                       hideUnknownSecretRecipes (ซ่อนสูตรลับที่ยังไม่รู้ + จานของสูตรนั้นในแท็บ Dishes ผ่าน IsHiddenSecretDish)
UI/InventorySlotUI.cs  SlotState (Owned/DiscoveredEmpty/Undiscovered), ตัวเลขมุมขวาบน, SelectionFrame, OnClicked,
                       ลากไป Hotbar ได้ (ช่องที่ลากไม่ได้ ส่งการลากต่อให้ Scroll View)
UI/StatsUI.cs          แถบ HP/ST/MP/FEVER (ยืด/หดด้วย Anchor Max X ไม่ใช้ Image Filled), FEVER เต็มแล้วกะพริบ
UI/HotbarUI.cs         แถบ 10 ช่อง + ชื่อไอเทมที่เลือก, จางตอนเปิดร้าน (CanvasGroup)
UI/HotbarSlotUI.cs     ไอคอน/จำนวน/เลขปุ่ม/กรอบเลือก, คลิกเลือก, OnDrop (จากกระเป๋า = ใส่, จาก Hotbar = สลับ), ลากออก = เอาออก
UI/EquipmentPanelUI.cs แผงช่องสวมใส่ 8 ช่อง (แถวบน Stats แถวล่าง Fashion), OnSlotClicked, SetSelectedSlot
UI/EquipmentSlotUI.cs  ช่องสวมใส่ 1 ช่อง: OnDrop (จากกระเป๋า = สวม, จากช่องอื่น = สลับ), ลากออก = ถอด, คลิก = ดูรายละเอียด
UI/MenuSelectionUI.cs  หน้าต่างเลือกเมนู (static Instance / IsAnyOpen): Open, Close, ปุ่ม Open Shop / Cancel, Esc, Shift = ±5, [2.4b] popularText + เรียงเมนูนิยมขึ้นก่อน
UI/MenuSlotRowUI.cs    แถวช่องเมนู (ชื่อ, ready+cook, −/+, X) สร้างจาก MenuSlotTemplate
UI/MenuRecipeItemUI.cs รายการสูตรฝั่งขวา (คลิก = ใส่ช่องว่าง) สร้างจาก RecipeItemTemplate, [2.4b] เมนูนิยม = `*` + ชื่อสีทอง + "Popular today!"
UI/ServiceTimerUI.cs   ซ่อน ClockPanel / แสดง ServiceTimerPanel ระหว่างเปิดร้าน (TimerText ใช้ <mspace>, StatusText)
UI/DragIconUI.cs       ไอคอนลอยตามเมาส์ตอนลาก สร้างตัวเองบน Canvas หลัก (ไม่ต้องวางในฉาก)
Editor/RecipeImporter.cs           Tools > Second Serving > Import Recipes & Ingredients
Editor/InventoryLayoutBuilder.cs   Tools > Second Serving > Build Inventory Window Layout (จัดหน้าต่างกระเป๋าอัตโนมัติ)
Editor/HudLayoutBuilder.cs         Tools > Second Serving > Build Stats HUD (มุมซ้ายบน) / Build Hotbar HUD (กลางล่าง)
                                   ใส่คอมโพเนนต์ที่ขาดให้ Player และผูกช่องให้ กดซ้ำได้
Editor/CustomerSetupBuilder.cs     Tools > Second Serving > Build Customer System (prefab Customer / Table_2Seats / WhiteSquare + CustomerManager + GridPathfinder)
Editor/ServiceTimerLayoutBuilder.cs Tools > Second Serving > Build Service Timer
Editor/MenuLayoutBuilder.cs        Tools > Second Serving > Build Daily Menu Window (สร้าง DailyMenu + PopularDishes + MenuSelectionUI/MenuWindow + PopularText + ผูก SaveManager)
Editor/EquipmentLayoutBuilder.cs   Tools > Second Serving > Build Equipment Panel (สร้างแผง + PlayerEquipment + ผูก InventoryUI/SaveManager)
                                   / Create Test Equipment Items (ของทดสอบ 8 ชิ้นใน Assets/Items/TestEquipment + อัปเดต ItemDatabase)
```

`SaveSync.jslib` (FS.syncfs สำหรับเว็บ) ยังไม่เจอใน Scripts/Systems/Save ต้องเช็กตอนทำเดโมเว็บว่าย้ายไป `Assets/Plugins/WebGL` แล้วหรือยัง

## 8. การตั้งค่าในฉาก (SampleScene)

- **Player:** Box Collider 2D, Rigidbody2D, Inventory, RecipeBook, PlayerController, PlayerInteractor, PlayerTeleporter, PlayerStats, Hotbar, PlayerActionController, PlayerEquipment
- **กล้อง:** Main Camera (Cinemachine Brain) + PlayerFollowCam (Position Composer, Ortho 5, Confiner 2D)
- **Grid:** Ground, Obstacles, CameraBoundsMap, CameraBounds_Shop
- **ร้าน:** ประตู 4 บาน (Door), ShopCounter, Bed และจุดวาร์ป ShopEntry_Front/Back, Outside_Front/Back, BedWakePoint, HospitalWakePoint
- **ระบบ:** GameClock, ShopManager, DailyMenu (+ **PopularDishes**), **CustomerManager** (+ GridPathfinder, ลูก Customers), DayEndManager, ForageSpawner, SaveManager (ItemDatabase, Inventory, ForageSpawner, RecipeDatabase, RecipeBook, Hotbar, PlayerStats, PlayerEquipment, DailyMenu)
- **Canvas:** ClockUI, NotificationUI, DaySummaryUI, InventoryUI → InventoryWindow (TitleText, ProgressText, BagLevelText, TabBar ที่มีปุ่ม 5 แท็บ, SlotScrollView/Viewport/Content, DetailPanel, **EquipmentPanel** (EquipSlot_Shoes … EquipSlot_Back), SlotGrid เดิมถูกปิดไว้),
  **StatsHUD** (StatsUI, แถว HealthRow/StaminaRow/ManaRow/FeverRow), **HotbarHUD** (HotbarUI, Slot_1–Slot_10, SelectedNameText),
  **ServiceTimerUI** → ServiceTimerPanel (TimerText, StatusText) กลางบนจอ,
  **MenuSelectionUI** → MenuWindow 1200x800 (TitleText, HintText, **PopularText**, SlotList + MenuSlotTemplate, RecipeScrollView + RecipeItemTemplate, TotalText, CancelButton, OpenShopButton) อยู่บนสุดของ Canvas
- หน้าต่างกระเป๋า 1500x860 กลางจอ (ขอบล่างอยู่ที่ y=110) Hotbar สูง 72 ห่างขอบล่าง 16 จึงไม่ทับกัน
- **ของทดสอบ:** ผู้ใช้สร้าง `TestSword` (Weapon) และ `TestHoe` (Tool) ไว้ใน `Assets/Items` / ของสวมใส่ทดสอบ 8 ชิ้นใน `Assets/Items/TestEquipment` (test_boots, test_ring_ruby, test_ring_jade, test_necklace, test_hat, test_top, test_bottom, test_wings)
- **กล้อง:** Ortho Size 5 (จอประมาณ 17.8 x 10 หน่วย) Position Composer Damping 0.5, Confiner 2D Damping 0.5 / Slowing Distance 5
  - เคยมีปัญหากล้องกระโดดในร้าน เพราะ `CameraBounds_Shop` เป็น 2 ห้องเชื่อมด้วยช่องแคบกว่าจอ (Confiner แยกเป็น 2 เกาะ) ผู้ใช้รู้วิธีแก้และจัดการเอง
  - `CameraBoundsMap` กว้าง 17 หน่วย แคบกว่าจอ กล้องข้างนอกจึงเลื่อนซ้ายขวาไม่ได้ ตอนขยายแผนที่ต้องกว้างกว่า 18 หน่วย
- **Prefab:** `Assets/Prefabs/InventorySlot` (มี SelectionFrame), `Assets/Prefabs/Mushroom_Pickup`, `Assets/Prefabs/Customer` (Body sprite, Bubble TextMeshPro, Trigger + Kinematic), `Assets/Prefabs/Table_2Seats` (Collider ทึบ + Seat_Left/Seat_Right ไม่มี Collider), `Assets/Prefabs/Placeholder/WhiteSquare.png` (4x4, PPU 4)
- **ร้าน:** ผู้ใช้วางโต๊ะ Table_2Seats ในร้านเองแล้ว / Player มี Rigidbody2D แบบ Dynamic (ตัวหาเส้นทางจึงไม่นับผู้เล่นเป็นสิ่งกีดขวาง)
- Tilemap สิ่งกีดขวางในฉากชื่อ **`Obstables`** (สะกดผิด) / `CameraBoundsMap` และ `CameraBounds_Shop` มี TilemapCollider2D ทึบ (ใช้กับ Confiner)

## 9. Roadmap

UI ทั้งหมดตอนนี้เป็นแบบใช้งานได้ ผู้ใช้จะ polish หน้าตาทีหลัง (เฟส 4)

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
- **ขั้น 2.4a:** ลูกค้า AI (Grid A*, โต๊ะ/เก้าอี้, เดิน-นั่ง-สั่ง-รอ-เสิร์ฟด้วย E-กิน-ออก) + ตัวจับเวลาร้าน + ขายหมดปิดร้านก่อน + ปิดร้านทันทีหลังเสิร์ฟคนสุดท้าย + Waiting นับคนนั่ง + วิ่ง (Shift) ผู้ใช้ทดสอบแล้วใช้ได้
- **ขั้น 2.4b:** เมนูนิยมรายวัน (PopularDishes) + แสดงในหน้าต่างเมนู + น้ำหนักการสั่ง ×3 + ลูกค้าถามหาเมนูนิยมที่ไม่มี (กด E แนะนำเมนูวันนี้) + อารมณ์หลังกิน (ชอบมาก/อร่อยดี/เฉยๆ) + ความพอใจรายวัน ผู้ใช้ทดสอบแล้ว "โอเคสำหรับทดสอบ"
- **ขั้น D = 2.3:** เมนูประจำวัน (DailyMenu + หน้าต่างเลือกเมนูที่เคาน์เตอร์ + จอง/เสิร์ฟ/ปิดร้าน + เซฟ v5) ผู้ใช้ทดสอบแล้วใช้ได้ (UI จะ polish ทีหลัง)
- **ขั้น C:** Equipment Slots 8 ช่อง (Stats 4 + Fashion 4) ในหน้าต่างกระเป๋า ลากใส่/ถอด/สลับ + เซฟ v4 (ผู้ใช้ทดสอบเบื้องต้นแล้ว ใช้งานได้)

**⬜ เฟส 2 (เหลือประมาณ 6–8 สัปดาห์)**

| ขั้น | งาน | เวลา |
|---|---|---|
| **2.5** ← ถัดไป | ทำอาหารและเสิร์ฟ (เตา → ถือจาน → เสิร์ฟ แทน E ชั่วคราว) + เตาในครัวที่ใช้ได้ทุกเวลา + โหมดลองผสม (ค้นพบสูตร Rumor) | 9–13 วัน |
| – | ปรับความยาวรอบขาย + ความถี่ลูกค้า หลังมีการทำอาหาร | 0.5 วัน |
| 2.6 | เงิน (ต่อจาก CustomerManager.OnCustomerPaid / EarnedToday) ชื่อเสียง (LeftUnhappyToday, Satisfaction/Mood รายวัน) ราคาจากต้นทุน ความชำนาญ โบนัสความหลากหลาย + จุดอัปเกรดร้านที่เรียก `PopularDishes.SetBonusCount` / `CustomerManager.SetLovedChanceBonus` | 3–4 วัน |
| 2.6b | ร้านค้าพื้นฐาน (ซื้อ/ขาย) + อัปเกรดกระเป๋า | 4–6 วัน |
| 2.6c | ของป่าสำหรับสูตรเริ่มต้น 12 ชนิด (prefab + โซนงอก) | 2–3 วัน |
| 2.7 | สรุปรายวัน (ส่วนที่เหลือ: ยอดขาย/ลูกค้า/เงิน จาก DailyMenu + CustomerManager) | 1–2 วัน |
| – | เล่นทดสอบทั้งวงจร | 4–5 วัน |

🎯 จบเฟส 2 = Vertical Slice เล่นครบวงจรด้วย 8 สูตรเริ่มต้น

**⬜ เฟส 3 (ประมาณ 8–11 เดือน)**
NPC และบทสนทนา, ความสนิทและรสนิยมอาหาร, ข่าวลือและ side story สูตรลับ (ใช้คอลัมน์ Rumor / Story Notes), ฤดูกาลและอากาศ, เควสต์และเทศกาล, แบ่งแผนที่เป็นโซน, พ่อค้าเร่, ร้านค้าเต็มรูปแบบ, ปลูกพืช, เลี้ยงสัตว์, เครื่องแปรรูป, โซนทะเลและตกปลา, **ระบบต่อสู้ + มอนสเตอร์ + บอส + ถ้ำ (8–12 สัปดาห์)** (ต่อจาก PlayerActionController: hitbox, ดาเมจ, เรียก `RegisterCombatHit`, ต้นทุนท่าย้ายไปอยู่ที่ข้อมูลอาวุธ, FEVER จาก Skill), **ความชำนาญ (proficiency) แต่ละอาวุธแบบ Stardew** (ให้แหล่งฟื้น Mana), โพชั่น, ของสวมใส่ที่มีผลกับค่าสถานะ, บทลงโทษตอนสลบ/Health หมด, ลูกค้าเป็นกลุ่ม + ลูกค้าหลายแบบ (ความอดทน/รสนิยมต่างกัน), NPC ใช้ GridPathfinder เดินทั่วเมือง, สิ่งประดิษฐ์ช่วงปลายเกม

**⬜ เฟส 4 (ประมาณ 4–7 เดือน)**
ภาพ (ไอคอนประมาณ 170 ชิ้น), polish UI ทั้งหมด (หน้าต่างเมนู ฯลฯ), ตัวบอกสถานะวิ่งบนจอ, เสียง, เมนูหลัก, ปรับสมดุล, ฟอนต์ไทย, เช็กภาพสั่น (Interpolate), เดโมเว็บ (SaveSync.jslib), หน้า Steam (ควรขึ้นช่วงกลางเฟส 3), ปล่อยเกม

## 10. ขั้นถัดไป: ขั้น 2.5 ทำอาหารและเสิร์ฟ

ขั้น 2.4b ผู้ใช้ทดสอบแล้ว "โอเคสำหรับทดสอบ" (ค่าตัวเลขต่างๆ ค่อยปรับตอนเล่นทดสอบทั้งวงจร)

**สิ่งที่ตัดสินใจไว้แล้ว** (ข้อ 6)
- เปลี่ยนการเสิร์ฟชั่วคราว (กด E ที่ลูกค้า) เป็น: ทำที่เตา → ถือจาน → เดินไปเสิร์ฟ (ผู้ใช้อยากให้มีเฟสทำอาหารจริงจัง แบบเกมทำอาหาร)
- เตาในครัวใช้ได้ทุกเวลา (ทำจานนอกเวลาเปิดร้านได้ จานเป็นไอเทม ไม่เน่า ใช้ให้ NPC / กิน / ขาย)
- โหมดลองผสมที่เตา = ทางค้นพบสูตร Rumor (สูตรลับประเภทข่าวลือ)
- จุดที่ต้องต่อ: `DailyMenu.TryTakeServing(recipe)` (หักจานที่ทำไว้ก่อน แล้วค่อยหักวัตถุดิบที่จอง), `Customer.TryServe()`
- ลูกค้าที่ถามหาเมนูนิยม (Asking) ยังใช้ E แนะนำเมนูเหมือนเดิม ต้องตัดสินว่าการเสิร์ฟแบบถือจานใช้ปุ่มเดียวกันไหม

**ต้องถามผู้ใช้ก่อนเขียนโค้ด** (ตัวอย่าง)
- ทำอาหารเป็นมินิเกมแบบไหน (กดตามจังหวะ / ขั้นตอนหั่น-ผัด-จัดจาน / แค่รอเวลา) และยากขึ้นตามสูตรไหม
- ทำทีละจานหรือหลายจานพร้อมกัน มีเตากี่เตา ถือจานได้กี่จาน
- ระหว่างเปิดร้าน วัตถุดิบที่จองไว้ถูกหักตอนเริ่มทำหรือตอนทำเสร็จ / ทำพลาดเสียวัตถุดิบไหม
- คุณภาพจาน (ทำดี/ไม่ดี) มีผลกับความพอใจหรืออารมณ์หลังกินไหม
- ลองผสม: เลือกวัตถุดิบเองได้กี่อย่าง ผสมผิดได้อะไร (ของเสีย / ไม่เสียอะไร) ใบ้ยังไง
- แบ่ง 2.5 เป็นขั้นย่อย (2.5a เตา + ถือจาน + เสิร์ฟ / 2.5b มินิเกม / 2.5c ลองผสม) แล้วทำทีละขั้น
- จบ 2.5 แล้ว: ปรับความยาวรอบขาย + ความถี่ลูกค้า, สรุป roadmap ในแชท + อัปเดต HANDOFF.md

**วิธีทดสอบที่ใช้อยู่**
- คลิกขวาที่หัวคอมโพเนนต์ (PlayerStats / Hotbar / PlayerEquipment / DailyMenu / CustomerManager) ระหว่าง Play เพื่อใช้เมนู Debug
- อยากได้ไอเทมใดๆ: ที่คอมโพเนนต์ Hotbar กรอก `Debug Item Id` (+ `Debug Amount`) แล้วคลิกขวาหัว > `Debug: Give Item By Id`
- สูตรทั้งหมด: คลิกขวาหัว RecipeBook > `Debug: Learn All Recipes`
- เร่งเวลา F7 (ค้าง) / ปิดร้านทันที F8 (ลูกค้าที่ยังรอจะเดินออก) / ลบเซฟ F9
- ไอเทมใหม่ต้องกด Find All Items ที่ ItemDatabase ก่อน ไม่งั้นโหลดเซฟกลับไม่ได้
- ค่าใน Inspector ที่แก้ระหว่าง Play จะหายตอนหยุด Play
- เมนูนิยม: คลิกขวาหัว PopularDishes > `Debug: Reroll Popular Dishes` / `Debug: Print Popular Dishes` / ให้เจอลูกค้าถามหาเร็วๆ ตั้ง CustomerManager → `Ask For Popular Chance` = 1 ชั่วคราว
- ค่าที่ปรับบ่อย: ShopManager → Service Duration Seconds, CustomerManager → Min/Max Spawn Interval / Popular Order Weight / Ask For Popular Chance / Base Loved Chance / Satisfaction, prefab Customer → Min/Max Patience / Eat Duration / Leave Speed Multiplier, Player → PlayerController → Run
