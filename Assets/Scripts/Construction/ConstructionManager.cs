using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// [2.6] ตัวจัดการก่อสร้าง (ช่าง): สร้างได้ทีละงาน แบบ Stardew / เปิดร้านได้ปกติระหว่างสร้าง
// - เริ่ม: ตรวจครบ (CanStart) → หักเงิน → หักของ → (ถ้าต้องใช้หลอด) หลอดกลับ 0 → นับวัน / ไม่ครบ = ไม่หักอะไรเลย
// - ช่างเริ่มงานวันถัดไปหลังจ้าง (ผู้ใช้ขอ): เช้าแรกหลังจ้าง = เริ่มทำ (แจ้งเตือน) / เช้าถัดๆ ไป วันที่เหลือ −1 → 0 = เสร็จ
//   เช่น งาน 1 วัน จ้างวันที่ 1 → เริ่มวันที่ 2 → เสร็จเช้าวันที่ 3
// - เซฟใน SaveData v8 (รหัสงาน + วันที่เหลือ) โหลด = คืนค่าเฉยๆ ไม่หักเงินซ้ำ
// ทำงานก่อน MailSystem (-50) ตอนเช้า เพื่อให้ร้านเลเวลขึ้นก่อนเช็กจดหมาย
[DefaultExecutionOrder(-60)]
public class ConstructionManager : MonoBehaviour
{
    public static ConstructionManager Instance { get; private set; }

    [Header("Projects")]
    [Tooltip("งานก่อสร้างทั้งหมด (Build Economy ใส่ให้)")]
    [SerializeField] private List<ConstructionProject> projects = new List<ConstructionProject>();

    [Header("References")]
    [Tooltip("กระเป๋าของผู้เล่น (เว้นว่าง = หาบนตัว Player)")]
    [SerializeField] private Inventory inventory;

    [Header("Text")]
    [Tooltip("สร้างเสร็จ ({0} = ชื่องาน)")]
    [SerializeField] private string finishedFormat = "Construction finished: {0}!";

    [Tooltip("ช่างเริ่มงาน ({0} = ชื่องาน, {1} = จำนวนวัน)")]
    [SerializeField] private string startedFormat = "The builder started working on {0}. Ready in {1} day(s).";

    [Tooltip("ร้านเลเวลขึ้น ({0} = เลเวลใหม่)")]
    [SerializeField] private string shopLevelUpFormat = "Your shop is now level {0}!";

    public IReadOnlyList<ConstructionProject> Projects => projects;
    public ConstructionProject Current { get; private set; }
    public int DaysLeft { get; private set; }
    public bool IsBuilding => Current != null;

    // จ้างแล้วแต่ช่างยังไม่เริ่ม (เริ่มเช้าพรุ่งนี้)
    public bool StartsTomorrow { get; private set; }

    // ส่งสัญญาณเมื่อเริ่ม/เสร็จ/วันที่เหลือเปลี่ยน
    public event Action OnConstructionChanged;

    private bool isSubscribed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("ConstructionManager: Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (inventory == null)
        {
            PlayerInteractor player = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
            if (player != null) inventory = player.GetComponent<Inventory>();
        }
    }

    private void Start() { Subscribe(); }
    private void OnEnable() { Subscribe(); }

    private void OnDisable()
    {
        if (isSubscribed && GameClock.Instance != null)
        {
            GameClock.Instance.OnDayStarted -= HandleDayStarted;
        }
        isSubscribed = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Subscribe()
    {
        if (isSubscribed || GameClock.Instance == null) return;
        GameClock.Instance.OnDayStarted += HandleDayStarted;
        isSubscribed = true;
    }

    // ---------- หางาน ----------

    // งานอัปเกรดร้านขั้นถัดไป (ตามเลเวลร้านตอนนี้) ไม่มี = null (เลเวลสูงสุดแล้ว)
    public ConstructionProject NextShopUpgrade()
    {
        int level = ShopProgress.Instance != null ? ShopProgress.Instance.Level : 1;
        foreach (ConstructionProject project in projects)
        {
            if (project != null && project.effect == ConstructionEffect.ShopLevelUp && project.requiredShopLevel == level)
            {
                return project;
            }
        }
        return null;
    }

    public ConstructionProject FindById(string projectId)
    {
        foreach (ConstructionProject project in projects)
        {
            if (project != null && project.projectId == projectId) return project;
        }
        return null;
    }

    // ---------- เริ่มงาน ----------

    // ตรวจทุกเงื่อนไข (ยังไม่หักอะไร) reason = ข้อความบอกผู้เล่นว่าขาดอะไร
    public bool CanStart(ConstructionProject project, out string reason)
    {
        if (project == null) { reason = "Nothing to build."; return false; }
        if (IsBuilding) { reason = $"The builder is busy with {Current.displayName} ({DaysLeft} day(s) left)."; return false; }

        ShopProgress progress = ShopProgress.Instance;
        if (project.requiredShopLevel > 0)
        {
            int level = progress != null ? progress.Level : 1;
            if (level != project.requiredShopLevel) { reason = $"Needs shop level {project.requiredShopLevel}."; return false; }
        }
        if (project.requiresFullReputation && (progress == null || !progress.IsBarFull))
        {
            int rep = progress != null ? progress.Reputation : 0;
            int bar = progress != null ? progress.BarSize : 0;
            reason = $"Reputation not full yet ({rep:N0} / {bar:N0}).";
            return false;
        }

        PlayerWallet wallet = PlayerWallet.Instance;
        if (project.money > 0 && (wallet == null || !wallet.CanAfford(project.money)))
        {
            int have = wallet != null ? wallet.Money : 0;
            reason = $"Need {PlayerWallet.Format(project.money - have)} more.";
            return false;
        }

        foreach (MaterialCost cost in project.materials)
        {
            if (cost == null || cost.item == null) continue;
            int have = inventory != null ? inventory.GetCount(cost.item) : 0;
            if (have < cost.amount)
            {
                reason = $"Need {cost.item.itemName} x{cost.amount - have} more.";
                return false;
            }
        }

        reason = "";
        return true;
    }

    // จ้างช่าง: ตรวจซ้ำ → หักเงิน → หักของ → หลอด → เริ่มนับวัน คืนค่า true ถ้าเริ่มได้
    public bool TryStart(ConstructionProject project)
    {
        if (!CanStart(project, out string reason))
        {
            Debug.Log($"ConstructionManager: Cannot start {(project != null ? project.displayName : "null")}: {reason}");
            return false;
        }

        if (!PlayerWallet.Instance.TrySpend(project.money))
        {
            return false; // ไม่ควรเกิด (ตรวจแล้ว) แต่กันไว้: ยังไม่ได้หักอะไร
        }

        foreach (MaterialCost cost in project.materials)
        {
            if (cost == null || cost.item == null || inventory == null) continue;
            inventory.RemoveItem(cost.item, cost.amount);
        }

        if (project.requiresFullReputation && ShopProgress.Instance != null)
        {
            ShopProgress.Instance.SpendBar();
        }

        Current = project;
        DaysLeft = Mathf.Max(1, project.days);
        StartsTomorrow = true;
        Debug.Log($"ConstructionManager: Hired the builder for {project.displayName} ({DaysLeft} day(s), starts tomorrow).");
        OnConstructionChanged?.Invoke();
        return true;
    }

    // ---------- นับวัน / เสร็จ ----------

    private void HandleDayStarted(int day)
    {
        if (!IsBuilding) return;

        // เช้าแรกหลังจ้าง: ช่างเริ่มงาน ยังไม่นับวัน
        if (StartsTomorrow)
        {
            StartsTomorrow = false;
            NotificationUI.Post(string.Format(startedFormat, Current.displayName, DaysLeft));
            OnConstructionChanged?.Invoke();
            return;
        }

        DaysLeft--;
        if (DaysLeft <= 0)
        {
            Complete();
        }
        else
        {
            OnConstructionChanged?.Invoke();
        }
    }

    private void Complete()
    {
        ConstructionProject project = Current;
        Current = null;
        DaysLeft = 0;
        StartsTomorrow = false;

        ApplyEffect(project);
        Debug.Log($"ConstructionManager: Finished {project.displayName}.");
        OnConstructionChanged?.Invoke();
    }

    private void ApplyEffect(ConstructionProject project)
    {
        switch (project.effect)
        {
            case ConstructionEffect.ShopLevelUp:
                if (ShopProgress.Instance != null)
                {
                    ShopProgress.Instance.LevelUp();
                    NotificationUI.Post(string.Format(shopLevelUpFormat, ShopProgress.Instance.Level));
                }
                break;
            default:
                NotificationUI.Post(string.Format(finishedFormat, project.displayName));
                break;
        }
    }

    // ---------- เซฟ / โหลด ----------

    public void CaptureState(SaveData data)
    {
        data.constructionId = Current != null ? Current.projectId : "";
        data.constructionDaysLeft = Current != null ? DaysLeft : 0;
        data.constructionStartsTomorrow = Current != null && StartsTomorrow;
    }

    // คืนค่าเฉยๆ ไม่หักเงิน/ของ/หลอดซ้ำ
    public void RestoreState(SaveData data)
    {
        Current = null;
        DaysLeft = 0;
        StartsTomorrow = false;

        if (!string.IsNullOrEmpty(data.constructionId))
        {
            ConstructionProject project = FindById(data.constructionId);
            if (project != null)
            {
                Current = project;
                DaysLeft = Mathf.Max(1, data.constructionDaysLeft);
                StartsTomorrow = data.constructionStartsTomorrow;
            }
            else
            {
                Debug.LogWarning($"ConstructionManager: Saved project '{data.constructionId}' not found.");
            }
        }

        OnConstructionChanged?.Invoke();
    }

    // ---------- Debug (คลิกขวาที่หัวคอมโพเนนต์ระหว่าง Play) ----------

    [ContextMenu("Debug: Finish Construction Now")]
    private void DebugFinishNow()
    {
        if (IsBuilding) Complete();
        else Debug.Log("ConstructionManager: Nothing is being built.");
    }

    [ContextMenu("Debug: Print Status")]
    private void DebugPrint()
    {
        StringBuilder sb = new StringBuilder("ConstructionManager: ");
        sb.Append(IsBuilding ? $"Building {Current.displayName}, {DaysLeft} day(s) left{(StartsTomorrow ? " (starts tomorrow)" : "")}." : "Idle.");
        ConstructionProject next = NextShopUpgrade();
        sb.Append(next != null ? $" Next shop upgrade: {next.displayName}." : " Shop is max level.");
        sb.Append($" {projects.Count} project(s).");
        Debug.Log(sb.ToString());
    }
}
