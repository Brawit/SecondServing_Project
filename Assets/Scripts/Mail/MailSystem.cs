using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// [2.5c] จดหมาย 1 ฉบับในตู้/โต๊ะ: เป็นจดหมายสูตรจากเชฟ (recipe) หรือจดหมายทั่วไป (data) อย่างใดอย่างหนึ่ง
[Serializable]
public class MailLetter
{
    public const string ChefPrefix = "chef:";
    public const string LetterPrefix = "letter:";

    public RecipeData recipe;     // จดหมายสูตรจากเชฟ
    public LetterData data;       // จดหมายทั่วไป
    public int dayReceived;
    public bool isRead;
    public int claimedMask;       // บิตที่ i = รับของแนบชิ้นที่ i แล้ว

    private LetterAttachment chefAttachment;

    // รหัสในไฟล์เซฟ เช่น "chef:th_pad_thai" / "letter:passout_neighbor"
    public string Id => recipe != null ? ChefPrefix + recipe.recipeId : data != null ? LetterPrefix + data.letterId : "";

    public ChefData Chef => recipe != null ? recipe.chef : null;

    public string Sender => data != null ? data.senderName : Chef != null ? Chef.displayName : "Someone";

    public string SenderSubtitle
    {
        get
        {
            if (data != null) return data.senderSubtitle;
            if (Chef == null) return "";
            return Chef.cuisine == Cuisine.HouseSpecial ? "House Special" : Chef.cuisine.ToString();
        }
    }

    public string Subject => data != null ? data.subject : recipe != null ? recipe.recipeName : "";
    public string Body => data != null ? data.body : recipe != null ? recipe.letterBody : "";
    public string SignOff => data != null ? data.signOff : Chef != null ? Chef.signOff : "";

    public int AttachmentCount => data != null ? data.attachments.Count : recipe != null ? 1 : 0;

    public LetterAttachment GetAttachment(int index)
    {
        if (data != null)
        {
            return index >= 0 && index < data.attachments.Count ? data.attachments[index] : null;
        }
        if (recipe != null && index == 0)
        {
            if (chefAttachment == null) chefAttachment = new LetterAttachment { kind = LetterAttachmentKind.Recipe, recipe = recipe };
            return chefAttachment;
        }
        return null;
    }

    public bool IsClaimed(int index) => (claimedMask & (1 << index)) != 0;

    public bool HasUnclaimed
    {
        get
        {
            for (int i = 0; i < AttachmentCount; i++)
            {
                if (GetAttachment(i) != null && !IsClaimed(i)) return true;
            }
            return false;
        }
    }
}

// [2.5c] ระบบจดหมาย
// - ทุกเช้า (OnDayStarted): ส่งจดหมายที่รอส่ง (QueueLetter) + จดหมายสูตรจากเชฟทุกฉบับที่ถึงเกณฑ์ เข้าตู้ (ไม่จำกัดจำนวน)
//   จดหมายเชฟ = ยังไม่เคยได้ + ยังไม่รู้สูตร + เลเวลร้านและชื่อเสียงถึงเกณฑ์ (คำนวณใหม่ทุกเช้า)
// - สลบ (OnPassedOut) = สุ่มจดหมาย PassOut 1 ฉบับ รอส่งเช้าถัดไป
// - เปิดอ่าน = อ่านแล้ว / ของแนบต้องกดรับเอง (สูตร = เรียนรู้, ไอเทม = เข้ากระเป๋า) กระเป๋าเต็ม = ยังรับไม่ได้ เก็บไว้รับทีหลังที่โต๊ะ
// - เซฟใน SaveData v7: จดหมายที่ได้, วันที่ได้, อ่านแล้ว, ของที่รับแล้ว, จดหมายที่รอส่ง
// ทำงานหลัง SaveManager (-100 คืนจดหมายจากเซฟ) และก่อน GameClock (0 เริ่มวันแรกใน Start) เพื่อไม่พลาดเช้าวันแรก
[DefaultExecutionOrder(-50)]
public class MailSystem : MonoBehaviour
{
    public static MailSystem Instance { get; private set; }

    [Header("References")]
    [Tooltip("สมุดสูตรของผู้เล่น (เว้นว่าง = หาบนตัว Player)")]
    [SerializeField] private RecipeBook recipeBook;

    [Tooltip("กระเป๋าของผู้เล่น (เว้นว่าง = หาบนตัว Player)")]
    [SerializeField] private Inventory inventory;

    [Tooltip("ฐานข้อมูลสูตร (เว้นว่าง = ใช้ของ RecipeBook)")]
    [SerializeField] private RecipeDatabase recipeDatabase;

    [Tooltip("จดหมายทั่วไปทั้งหมด (Build Mail System เติมให้) ใช้โหลดเซฟและสุ่มจดหมายตอนสลบ")]
    [SerializeField] private List<LetterData> letterLibrary = new List<LetterData>();

    [Header("Text")]
    [Tooltip("แจ้งเตือนเมื่อมีจดหมายใหม่ ({0} = จำนวนฉบับ) เว้นว่าง = ไม่แจ้ง")]
    [SerializeField] private string newMailFormat = "You've got mail! ({0})";

    [Tooltip("รับไอเทมสำเร็จ ({0} = จำนวน, {1} = ชื่อ)")]
    [SerializeField] private string itemClaimedFormat = "Got {1} x{0}!";

    [Tooltip("กระเป๋าเต็ม ({0} = จำนวน, {1} = ชื่อ)")]
    [SerializeField] private string bagFullFormat = "Your bag can't hold {1} x{0}. It stays in the letter for now.";

    [Tooltip("รู้สูตรนี้อยู่แล้ว ({0} = ชื่อสูตร)")]
    [SerializeField] private string alreadyKnownFormat = "You already know {0}.";

    // จดหมายทั้งหมดตามลำดับที่ได้ (เก่า → ใหม่)
    private readonly List<MailLetter> letters = new List<MailLetter>();

    // จดหมายทั่วไปที่รอส่งเช้าถัดไป
    private readonly List<LetterData> pending = new List<LetterData>();

    private bool isSubscribed;

    public IReadOnlyList<MailLetter> Letters => letters;

    public int UnreadCount
    {
        get
        {
            int count = 0;
            foreach (MailLetter letter in letters)
            {
                if (!letter.isRead) count++;
            }
            return count;
        }
    }

    // ส่งสัญญาณเมื่อได้จดหมายใหม่ อ่าน หรือรับของแนบ
    public event Action OnMailChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("MailSystem: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        FindReferences();
    }

    private void Start()
    {
        Subscribe();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        if (isSubscribed && GameClock.Instance != null)
        {
            GameClock.Instance.OnDayStarted -= HandleDayStarted;
            GameClock.Instance.OnPassedOut -= HandlePassedOut;
        }
        isSubscribed = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Subscribe()
    {
        if (isSubscribed || GameClock.Instance == null)
        {
            return;
        }

        GameClock.Instance.OnDayStarted += HandleDayStarted;
        GameClock.Instance.OnPassedOut += HandlePassedOut;
        isSubscribed = true;
    }

    private void FindReferences()
    {
        if (recipeBook == null || inventory == null)
        {
            PlayerInteractor player = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
            if (player != null)
            {
                if (recipeBook == null) recipeBook = player.GetComponent<RecipeBook>();
                if (inventory == null) inventory = player.GetComponent<Inventory>();
            }
        }
        if (recipeDatabase == null && recipeBook != null)
        {
            recipeDatabase = recipeBook.Database;
        }

        if (recipeBook == null) Debug.LogWarning("MailSystem: Recipe Book not found. Recipe attachments cannot be claimed.", this);
        if (inventory == null) Debug.LogWarning("MailSystem: Inventory not found. Item attachments cannot be claimed.", this);
        if (recipeDatabase == null) Debug.LogWarning("MailSystem: Recipe Database not found. No chef letters will arrive.", this);
    }

    // ---------- ส่งจดหมาย ----------

    // ให้ระบบอื่นส่งจดหมายทั่วไป: มาถึงตู้เช้าถัดไป (เฟส 3: เทศกาล, ความสนิท ฯลฯ เรียกตรงนี้)
    public void QueueLetter(LetterData letter)
    {
        if (letter == null)
        {
            return;
        }

        pending.Add(letter);
        Debug.Log($"MailSystem: Queued letter '{letter.letterId}' for next morning.");
    }

    private void HandlePassedOut(int day)
    {
        List<LetterData> candidates = new List<LetterData>();
        foreach (LetterData letter in letterLibrary)
        {
            if (letter != null && letter.trigger == LetterTrigger.PassOut) candidates.Add(letter);
        }

        if (candidates.Count > 0)
        {
            QueueLetter(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
        }
    }

    private void HandleDayStarted(int day)
    {
        int delivered = DeliverPending();
        delivered += DeliverChefLetters(true);
        Announce(delivered);
    }

    private int DeliverPending()
    {
        int day = CurrentDay;
        int count = 0;
        foreach (LetterData data in pending)
        {
            if (data == null) continue;
            letters.Add(new MailLetter { data = data, dayReceived = day });
            Debug.Log($"MailSystem: Letter '{data.letterId}' from {data.senderName} arrived (day {day}).");
            count++;
        }
        pending.Clear();
        return count;
    }

    // จดหมายสูตรจากเชฟที่เข้าเงื่อนไขทั้งหมด (checkThresholds = false: ไม่สนเลเวล/ชื่อเสียง ใช้กับ Debug)
    private int DeliverChefLetters(bool checkThresholds)
    {
        if (recipeDatabase == null)
        {
            return 0;
        }

        ShopProgress progress = ShopProgress.Instance;
        int level = progress != null ? progress.Level : 1;
        int reputation = progress != null ? progress.Reputation : 0;

        // ลำดับ: เกณฑ์ง่ายก่อน แล้วตามลำดับในฐานข้อมูล
        List<KeyValuePair<RecipeData, int>> candidates = new List<KeyValuePair<RecipeData, int>>();
        IReadOnlyList<RecipeData> all = recipeDatabase.Recipes;
        for (int i = 0; i < all.Count; i++)
        {
            RecipeData recipe = all[i];
            if (recipe == null || !recipe.IsChefMail || HasChefLetterFor(recipe)) continue;
            if (recipeBook != null && recipeBook.Knows(recipe)) continue; // รู้สูตรแล้วจากทางอื่น ไม่ต้องส่ง
            if (checkThresholds && (level < recipe.mailShopLevel || reputation < recipe.mailReputation)) continue;
            candidates.Add(new KeyValuePair<RecipeData, int>(recipe, i));
        }

        candidates.Sort((a, b) =>
        {
            int byLevel = a.Key.mailShopLevel.CompareTo(b.Key.mailShopLevel);
            if (byLevel != 0) return byLevel;
            int byReputation = a.Key.mailReputation.CompareTo(b.Key.mailReputation);
            return byReputation != 0 ? byReputation : a.Value.CompareTo(b.Value);
        });

        int day = CurrentDay;
        foreach (KeyValuePair<RecipeData, int> pair in candidates)
        {
            letters.Add(new MailLetter { recipe = pair.Key, dayReceived = day });
            Debug.Log($"MailSystem: Letter from {pair.Key.chef.displayName} arrived ({pair.Key.recipeName}, day {day}).");
        }
        return candidates.Count;
    }

    private void Announce(int delivered)
    {
        if (delivered <= 0)
        {
            return;
        }

        if (!string.IsNullOrEmpty(newMailFormat))
        {
            NotificationUI.Post(string.Format(newMailFormat, delivered));
        }
        OnMailChanged?.Invoke();
    }

    private int CurrentDay => GameClock.Instance != null ? GameClock.Instance.Day : 0;

    public bool HasChefLetterFor(RecipeData recipe)
    {
        if (recipe == null) return false;
        foreach (MailLetter letter in letters)
        {
            if (letter.recipe == recipe) return true;
        }
        return false;
    }

    // ฉบับยังไม่อ่านที่เก่าสุด (ตู้จดหมายหยิบฉบับนี้) ไม่มี = null
    public MailLetter NextUnread()
    {
        foreach (MailLetter letter in letters)
        {
            if (!letter.isRead) return letter;
        }
        return null;
    }

    // ---------- อ่าน / รับของ ----------

    public void MarkRead(MailLetter letter)
    {
        if (letter == null || letter.isRead)
        {
            return;
        }

        letter.isRead = true;
        OnMailChanged?.Invoke();
    }

    // กดรับของแนบชิ้นที่ index คืนค่า true ถ้ารับสำเร็จ (รับแล้ว/รู้สูตรแล้ว = นับว่ารับแล้ว)
    public bool TryClaim(MailLetter letter, int index)
    {
        if (letter == null || index < 0 || index >= 31 || letter.IsClaimed(index))
        {
            return false;
        }

        LetterAttachment attachment = letter.GetAttachment(index);
        if (attachment == null)
        {
            return false;
        }

        switch (attachment.kind)
        {
            case LetterAttachmentKind.Recipe:
                if (attachment.recipe == null || recipeBook == null) return false;
                if (recipeBook.Knows(attachment.recipe))
                {
                    NotificationUI.Post(string.Format(alreadyKnownFormat, attachment.recipe.recipeName));
                }
                else
                {
                    recipeBook.Learn(attachment.recipe); // แจ้งเตือน New recipe ให้เอง
                }
                break;

            case LetterAttachmentKind.Item:
                if (attachment.item == null || inventory == null) return false;
                int amount = Mathf.Max(1, attachment.amount);
                if (!inventory.AddItem(attachment.item, amount))
                {
                    NotificationUI.Post(string.Format(bagFullFormat, amount, attachment.item.itemName));
                    return false;
                }
                NotificationUI.Post(string.Format(itemClaimedFormat, amount, attachment.item.itemName));
                break;

            default:
                return false;
        }

        letter.claimedMask |= 1 << index;
        letter.isRead = true;
        OnMailChanged?.Invoke();
        return true;
    }

    // ---------- เซฟ / โหลด ----------

    public void CaptureState(SaveData data)
    {
        data.mailIds.Clear();
        data.mailDays.Clear();
        data.mailRead.Clear();
        data.mailClaimed.Clear();
        data.mailPendingIds.Clear();

        foreach (MailLetter letter in letters)
        {
            string id = letter.Id;
            if (string.IsNullOrEmpty(id)) continue;
            data.mailIds.Add(id);
            data.mailDays.Add(letter.dayReceived);
            data.mailRead.Add(letter.isRead);
            data.mailClaimed.Add(letter.claimedMask);
        }

        foreach (LetterData letter in pending)
        {
            if (letter != null) data.mailPendingIds.Add(letter.letterId);
        }
    }

    public void RestoreState(SaveData data)
    {
        letters.Clear();
        pending.Clear();

        if (recipeDatabase == null)
        {
            FindReferences();
        }

        if (data.mailIds != null)
        {
            for (int i = 0; i < data.mailIds.Count; i++)
            {
                MailLetter letter = ResolveLetter(data.mailIds[i]);
                if (letter == null)
                {
                    Debug.LogWarning($"MailSystem: Saved letter '{data.mailIds[i]}' not found. Skipped.");
                    continue;
                }
                if (letter.recipe != null && HasChefLetterFor(letter.recipe)) continue;

                letter.dayReceived = data.mailDays != null && i < data.mailDays.Count ? data.mailDays[i] : 0;
                letter.isRead = data.mailRead != null && i < data.mailRead.Count && data.mailRead[i];
                letter.claimedMask = data.mailClaimed != null && i < data.mailClaimed.Count ? data.mailClaimed[i] : 0;
                letters.Add(letter);
            }
        }

        if (data.mailPendingIds != null)
        {
            foreach (string id in data.mailPendingIds)
            {
                LetterData letter = FindLetterData(id);
                if (letter != null) pending.Add(letter);
                else Debug.LogWarning($"MailSystem: Pending letter '{id}' not found. Skipped.");
            }
        }

        OnMailChanged?.Invoke();
    }

    private MailLetter ResolveLetter(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (id.StartsWith(MailLetter.ChefPrefix))
        {
            RecipeData recipe = recipeDatabase != null ? recipeDatabase.GetById(id.Substring(MailLetter.ChefPrefix.Length)) : null;
            return recipe != null ? new MailLetter { recipe = recipe } : null;
        }
        if (id.StartsWith(MailLetter.LetterPrefix))
        {
            LetterData data = FindLetterData(id.Substring(MailLetter.LetterPrefix.Length));
            return data != null ? new MailLetter { data = data } : null;
        }
        return null;
    }

    private LetterData FindLetterData(string letterId)
    {
        foreach (LetterData letter in letterLibrary)
        {
            if (letter != null && letter.letterId == letterId) return letter;
        }
        return null;
    }

    // ---------- Debug (คลิกขวาที่หัวคอมโพเนนต์ระหว่าง Play) ----------

    [ContextMenu("Debug: Check Mail Now")]
    private void DebugCheckMailNow()
    {
        int count = DeliverPending() + DeliverChefLetters(true);
        Announce(count);
        Debug.Log($"MailSystem: Debug check delivered {count} letter(s).");
    }

    [ContextMenu("Debug: Deliver All Chef Letters")]
    private void DebugDeliverAll()
    {
        int count = DeliverChefLetters(false);
        Announce(count);
        Debug.Log($"MailSystem: Debug delivered {count} chef letter(s) (ignoring level/reputation, skipping known recipes).");
    }

    [ContextMenu("Debug: Send Pass-Out Letter Now")]
    private void DebugPassOutLetter()
    {
        HandlePassedOut(CurrentDay);
        Announce(DeliverPending());
    }

    [ContextMenu("Debug: Print Mail Status")]
    private void DebugPrint()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"MailSystem: {letters.Count} letter(s), {UnreadCount} unread, {pending.Count} pending, {letterLibrary.Count} in library.");
        foreach (MailLetter letter in letters)
        {
            sb.Append($"\n  Day {letter.dayReceived} · {letter.Sender} · {letter.Subject} · {(letter.isRead ? "read" : "UNREAD")}{(letter.HasUnclaimed ? " · gift waiting" : "")}");
        }
        Debug.Log(sb.ToString());
    }
}
