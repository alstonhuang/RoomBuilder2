using UnityEngine;

public class WallLayout : MonoBehaviour
{
    [Header("Auto Layout")]
    [Tooltip("Runs Layout() automatically in Edit Mode. Disabled by default to avoid modifying scene objects unexpectedly.")]
    public bool autoLayoutInEditMode = false;

    [Tooltip("Runs Layout() automatically in Play Mode. Disabled by default (runtime builders should control placement).")]
    public bool autoLayoutInPlayMode = false;

#if UNITY_EDITOR
    [Tooltip("When enabled, auto layout in Edit Mode only runs while editing a prefab in Prefab Mode (not in regular scenes).")]
    public bool onlyInPrefabModeInEdit = true;
#endif

    [Header("房間規則")]
    public float totalWidth = 4f;
    public float totalHeight = 4f;
    public float wallDepth = 0.5f;

    [Header("門的設定")]
    public float doorWidth = 1.6f;
    public float doorHeight = 2.5f;

    [Header("DOM 元素")]
    public Transform doorObject;
    public Transform leftPanel;
    public Transform rightPanel;
    public Transform topPanel;

    [Header("微調")]
    public bool autoAlignFloor = true;
    [Range(0f, 0.1f)] public float overlap = 0.02f; // 新增：重疊量，消除縫隙

    void Update()
    {
        // Never mutate scene objects automatically in Edit Mode.
        if (!Application.isPlaying) return;

        if (!autoLayoutInPlayMode) return;
        Layout();
    }

    [ContextMenu("Layout Now")]
    public void LayoutNow()
    {
        Layout();
    }

    [ContextMenu("自動偵測門的大小")]
    public void AutoDetectDoorSize()
    {
        if (doorObject == null) return;
        Bounds bounds = new Bounds(doorObject.position, Vector3.zero);
        Renderer[] renderers = doorObject.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers) {
            if (!r.name.Contains("Outline") && !(r is ParticleSystemRenderer)) 
                bounds.Encapsulate(r.bounds);
        }
        doorWidth = bounds.size.x;
        doorHeight = bounds.size.y;
    }

    void Layout()
    {
        if (doorObject == null || leftPanel == null || rightPanel == null || topPanel == null) return;

        // 1. 貼地邏輯
        if (autoAlignFloor) {
            doorObject.localPosition = Vector3.zero;
            Bounds bounds = new Bounds(doorObject.position, Vector3.zero);
            bool hasBounds = false;
            foreach (Renderer r in doorObject.GetComponentsInChildren<Renderer>()) {
                 if (!r.name.Contains("Outline")) {
                    if(!hasBounds) { bounds = r.bounds; hasBounds = true; }
                    else bounds.Encapsulate(r.bounds);
                 }
            }
            float lift = transform.position.y - bounds.min.y;
            if (Mathf.Abs(lift) > 0.001f) doorObject.position += Vector3.up * lift;
        } else {
            doorObject.localPosition = Vector3.zero;
        }

        // 2. 左右牆 (增加 overlap 消除縫隙)
        float sideWidth = ((totalWidth - doorWidth) / 2f) + overlap; // 加寬一點點
        if (sideWidth < 0) sideWidth = 0;

        Vector3 sideScale = new Vector3(sideWidth, totalHeight, wallDepth);
        leftPanel.localScale = sideScale;
        rightPanel.localScale = sideScale;

        // 位置計算 (讓它們往中間擠一點點)
        float xOffset = (totalWidth / 2f) - (sideWidth / 2f);
        float yCenter = totalHeight / 2f;

        leftPanel.localPosition = new Vector3(-xOffset, yCenter, 0);
        rightPanel.localPosition = new Vector3(xOffset, yCenter, 0);

        // 3. 頂牆 (強制貼齊天花板)
        float topHeight = totalHeight - doorHeight;
        
        if (topHeight <= 0) {
            topPanel.localScale = Vector3.zero;
        } else {
            // 寬度加寬，確保覆蓋門縫
            topPanel.localScale = new Vector3(doorWidth + (overlap * 2), topHeight + overlap, wallDepth);
            
            // 關鍵修改：位置是從「最頂端」往下算，而不是從門往上算
            // 這樣保證上方絕對切齊 totalHeight
            float topY = totalHeight - (topHeight / 2f); // 頂牆中心點
            topPanel.localPosition = new Vector3(0, topY, 0);
        }
    }

}
