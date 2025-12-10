using UnityEngine;

public class SmartWallAdapter : MonoBehaviour
{
    [Header("目標門 (要適應誰)")]
    public Transform doorTarget; 

    [Header("牆壁組件")]
    public Transform leftPanel;
    public Transform rightPanel;
    public Transform topPanel;

    [Header("設定")]
    public float totalWidth = 4f;  // 房間格子的總寬度
    public float totalHeight = 4f; // 房間總高度
    public float wallDepth = 0.5f; // 牆壁厚度

    void Start()
    {
        AdjustWalls();
    }

    void AdjustWalls()
    {
        if (doorTarget == null) return;

        // 1. 取得門的實際尺寸 (利用 Collider 或 Mesh 邊界)
        // 這裡我們假設門系統有一個 Collider 在最外層
        Bounds doorBounds = new Bounds(doorTarget.position, Vector3.zero);
        
        // 嘗試抓取門所有的 Renderer 來算總寬度
        Renderer[] renderers = doorTarget.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            doorBounds.Encapsulate(r.bounds);
        }

        // 算出門的寬度和高度
        float doorWidth = doorBounds.size.x;
        float doorHeight = doorBounds.size.y;

        // 2. 計算縫隙
        // 剩餘寬度 = 總寬度 - 門寬
        float remainingWidth = totalWidth - doorWidth;
        float sidePanelWidth = remainingWidth / 2f;

        // 3. 設定左牆
        // 大小：寬度是縫隙的一半，高度是滿的
        leftPanel.localScale = new Vector3(sidePanelWidth, totalHeight, wallDepth);
        // 位置：從中心點 (0) 往左移 -> (門寬的一半 + 左牆寬的一半)
        float xOffset = (doorWidth / 2f) + (sidePanelWidth / 2f);
        leftPanel.localPosition = new Vector3(-xOffset, totalHeight / 2f, 0);

        // 4. 設定右牆 (跟左牆一樣，只是 X 是正的)
        rightPanel.localScale = new Vector3(sidePanelWidth, totalHeight, wallDepth);
        rightPanel.localPosition = new Vector3(xOffset, totalHeight / 2f, 0);

        // 5. 設定頂牆 (門樑)
        // 如果門比牆矮，才需要頂牆
        if (doorHeight < totalHeight)
        {
            float topPanelHeight = totalHeight - doorHeight;
            
            // 寬度要涵蓋門的上方 (或者是 totalWidth 也可以，看設計風格，這裡做填補門上方)
            topPanel.localScale = new Vector3(doorWidth, topPanelHeight, wallDepth);
            
            // 位置：在門的頭頂上方
            // Y座標 = 門高 + (補牆高/2)
            // 注意：因為我們的牆座標原點是底邊，所以要仔細算
            float topY = doorHeight + (topPanelHeight / 2f);
            topPanel.localPosition = new Vector3(0, topY, 0);
            
            topPanel.gameObject.SetActive(true);
        }
        else
        {
            topPanel.gameObject.SetActive(false);
        }
    }
}