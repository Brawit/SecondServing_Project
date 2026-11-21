using UnityEngine;
using UnityEngine.UI;

// ไอคอนที่ลอยตามเมาส์ระหว่างลากของ (จากกระเป๋าหรือ Hotbar)
// สร้างตัวเองอัตโนมัติบน Canvas หลักตอนเริ่มลากครั้งแรก ไม่ต้องวางในฉาก
// ไอคอนนี้ไม่รับคลิก (ไม่บังช่องปลายทางที่จะวาง)
public class DragIconUI : MonoBehaviour
{
    private const float IconSize = 64f;
    private const float IconAlpha = 0.85f;

    private static DragIconUI instance;

    private Canvas canvas;
    private RectTransform rectTransform;
    private Image image;

    // ตอนนี้กำลังลากอยู่หรือไม่
    public static bool IsDragging => instance != null && instance.gameObject.activeSelf;

    // เริ่มแสดงไอคอน (source = UI ที่เริ่มลาก ใช้หา Canvas)
    public static void Begin(Component source, ItemData item, Vector2 screenPosition)
    {
        if (source == null || item == null)
        {
            return;
        }

        Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
        if (sourceCanvas == null)
        {
            Debug.LogWarning("DragIconUI: Source is not under a Canvas.");
            return;
        }

        Canvas root = sourceCanvas.rootCanvas;
        if (instance == null || instance.canvas != root)
        {
            Create(root);
        }

        instance.ShowItem(item);
        instance.transform.SetAsLastSibling(); // วาดทับทุกอย่าง
        instance.gameObject.SetActive(true);
        Move(screenPosition);
    }

    // ย้ายไอคอนไปตามตำแหน่งเมาส์
    public static void Move(Vector2 screenPosition)
    {
        if (instance == null || !instance.gameObject.activeSelf)
        {
            return;
        }

        RectTransform canvasRect = instance.canvas.transform as RectTransform;
        Camera cam = instance.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : instance.canvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, cam, out Vector2 local))
        {
            instance.rectTransform.localPosition = local;
        }
    }

    // ซ่อนไอคอน
    public static void End()
    {
        if (instance != null)
        {
            instance.gameObject.SetActive(false);
        }
    }

    private static void Create(Canvas root)
    {
        if (instance != null)
        {
            Destroy(instance.gameObject);
        }

        GameObject go = new GameObject("DragIcon", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(DragIconUI));
        go.transform.SetParent(root.transform, false);
        go.layer = root.gameObject.layer;

        // ไม่รับ raycast เพื่อให้ช่องที่อยู่ใต้เมาส์รับการวาง (OnDrop) ได้
        CanvasGroup group = go.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        instance = go.GetComponent<DragIconUI>();
        instance.canvas = root;
        instance.rectTransform = go.GetComponent<RectTransform>();
        instance.rectTransform.sizeDelta = new Vector2(IconSize, IconSize);
        instance.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        instance.image = go.GetComponent<Image>();
        instance.image.raycastTarget = false;

        go.SetActive(false);
    }

    private void ShowItem(ItemData item)
    {
        image.sprite = item.icon;
        image.preserveAspect = item.icon != null;

        // ยังไม่มีรูป ใช้สี่เหลี่ยมสีเทาแทน
        Color color = item.icon != null ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
        color.a = IconAlpha;
        image.color = color;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
