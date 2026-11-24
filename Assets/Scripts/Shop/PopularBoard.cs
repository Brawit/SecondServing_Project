using System.Text;
using TMPro;
using UnityEngine;

// [2.5c] กระดานเมนูนิยมประจำวันหน้าร้าน (กด E = แสดง/ซ่อน) ดูได้ทุกเวลา
// - แสดงเมนูนิยมวันนี้ สูตรที่ยังไม่รู้เป็นคำใบ้พร้อมทางได้สูตร เช่น "??? (Thai main) · a chef may write"
// - ผู้เล่นอยู่ใกล้ = "[E] Popular today" / เดินห่างเกิน closeDistance = ซ่อนเอง
// ต้องมี Collider2D บนวัตถุเดียวกัน
// สร้าง prefab ได้จาก Tools > Second Serving > Build Mail System (Assets/Prefabs/PopularBoard.prefab)
public class PopularBoard : MonoBehaviour, IInteractable
{
    [Header("References")]
    [Tooltip("ข้อความเหนือกระดาน (TextMeshPro แบบ 3D)")]
    [SerializeField] private TMP_Text boardText;

    [Header("Prompt")]
    [Tooltip("ข้อความชวนกด E เมื่อผู้เล่นอยู่ใกล้ (เว้นว่าง = ไม่แสดง)")]
    [SerializeField] private string promptText = "[E] Popular today";

    [Tooltip("ระยะที่แสดงข้อความชวนกด E")]
    [Min(0.5f)] [SerializeField] private float promptDistance = 1.6f;

    [Tooltip("เดินห่างจากกระดานเกินระยะนี้ = ซ่อนรายการ")]
    [Min(0.5f)] [SerializeField] private float closeDistance = 2.2f;

    [Header("Text")]
    [Tooltip("หัวข้อของรายการ")]
    [SerializeField] private string title = "<b>Popular today</b>";

    [Tooltip("ไม่มีเมนูนิยมวันนี้")]
    [SerializeField] private string emptyMessage = "Nothing special today.";

    private Transform player;
    private bool isShowing;
    private bool promptShown;
    private bool isSubscribed;

    private void Awake()
    {
        if (GetComponent<Collider2D>() == null)
        {
            Debug.LogWarning($"PopularBoard '{name}': Needs a Collider2D to be found by the player.", this);
        }
        if (boardText == null)
        {
            Debug.LogWarning($"PopularBoard '{name}': Board Text is not assigned.", this);
        }
    }

    private void Start()
    {
        PlayerInteractor interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor != null) player = interactor.transform;
        Subscribe();
        RefreshText();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        if (isSubscribed && PopularDishes.Instance != null)
        {
            PopularDishes.Instance.OnPopularChanged -= RefreshText;
        }
        isSubscribed = false;
    }

    private void Subscribe()
    {
        if (isSubscribed || PopularDishes.Instance == null)
        {
            return;
        }

        PopularDishes.Instance.OnPopularChanged += RefreshText;
        isSubscribed = true;
    }

    // ---------- IInteractable ----------

    public bool CanInteract(PlayerInteractor interactor)
    {
        return !CookingSession.IsBusy;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (interactor != null) player = interactor.transform;
        isShowing = !isShowing;
        Subscribe();
        RefreshText();
    }

    // ---------- ข้อความ ----------

    private void Update()
    {
        float distance = player != null ? Vector2.Distance(player.position, transform.position) : float.MaxValue;

        if (isShowing && distance > closeDistance)
        {
            isShowing = false;
            RefreshText();
        }

        bool show = !string.IsNullOrEmpty(promptText) && !isShowing && !CookingSession.IsBusy && distance <= promptDistance;
        if (show != promptShown)
        {
            promptShown = show;
            RefreshText();
        }
    }

    private void RefreshText()
    {
        if (boardText == null)
        {
            return;
        }

        if (!isShowing)
        {
            boardText.text = promptShown ? promptText : "";
            return;
        }

        StringBuilder sb = new StringBuilder(title);
        PopularDishes popular = PopularDishes.Instance;
        int count = 0;
        if (popular != null)
        {
            foreach (RecipeData recipe in popular.Today)
            {
                if (recipe == null) continue;
                sb.Append($"\n<noparse>{popular.Marker}</noparse> {popular.GetDisplayName(recipe)}");
                count++;
            }
        }
        if (count == 0)
        {
            sb.Append($"\n{emptyMessage}");
        }
        boardText.text = sb.ToString();
    }
}
