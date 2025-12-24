using UnityEngine;

public class KeyController : MonoBehaviour
{
    public bool debugSnap = false;

    void Start()
    {
        SnapToGround();
    }

    private void SnapToGround()
    {
        var col = GetComponentInChildren<Collider>();
        var ren = GetComponentInChildren<Renderer>();
        float extentsY = 0.1f;
        if (col != null) extentsY = col.bounds.extents.y;
        else if (ren != null) extentsY = ren.bounds.extents.y;

        Vector3 start = transform.position + Vector3.up * 5f;
        // Only hit ground layer (8) and default (0) by default; adjust as needed.
        int layerMask = (1 << 0) | (1 << 8);
        var hits = Physics.RaycastAll(start, Vector3.down, 50f, layerMask, QueryTriggerInteraction.Ignore);
        if (hits.Length > 0)
        {
            float minY = float.MaxValue;
            Vector3 bestPoint = transform.position;
            foreach (var h in hits)
            {
                if (h.point.y < minY)
                {
                    minY = h.point.y;
                    bestPoint = h.point;
                }
            }
            // 將底部直接貼在命中點上方微小偏移，避免浮空
            transform.position = bestPoint + Vector3.up * 0.01f;
            if (debugSnap)
            {
                Debug.Log($"[KeyController] {name} snapped to {transform.position} using lowest hit {bestPoint}, extentsY={extentsY}, col={col != null}, ren={ren != null}");
            }
        }
        else
        {
            if (debugSnap)
            {
                Debug.LogWarning($"[KeyController] {name} failed to find ground from {start}");
            }
        }
    }

    public void PickUp()
    {
        // 1. 找到玩家
        PlayerInteraction player = FindAnyObjectByType<PlayerInteraction>();
        
        if (player != null)
        {
            // 2. 加鑰匙
            player.AddKey();
            
            // 3. 毀滅自己
            Destroy(gameObject); 
        }
    }
}
