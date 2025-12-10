using UnityEngine;
using UnityEngine.UI;

public class PlayerInteraction : MonoBehaviour
{
    public float interactionDistance = 3f;
    public int keyCount = 0;
    public Image crosshairImage;
    public Color defaultColor = Color.white;
    public Color focusColor = Color.red;

    // 紀錄我們「上一幀」看著的東西
    private Interactable currentInteractable;

    void Update()
    {
        CheckHover();

        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
    }

    void CheckHover()
    {
        RaycastHit hit;
        
        // 1. 發射雷射
        if (Physics.Raycast(transform.position, transform.forward, out hit, interactionDistance))
        {
            Interactable newInteractable = hit.transform.GetComponentInParent<Interactable>();

            // 2. 判斷邏輯
            if (newInteractable != null)
            {
                // A. 如果現在看著這東西，跟上一幀不一樣 (代表視線剛移過來)
                if (newInteractable != currentInteractable)
                {
                    // 如果原本有看著別的東西，先叫它熄燈
                    if (currentInteractable != null) 
                        currentInteractable.OnLoseFocus();

                    // 紀錄新的東西，並叫它亮燈
                    currentInteractable = newInteractable;
                    currentInteractable.OnFocus();
                }

                // 準心變紅（若有設定）
                SetCrosshairColor(focusColor);
                return; // 結束，不要執行下面的熄燈邏輯
            }
        }

        // 3. 如果什麼都沒打到，或者打到的不是 Interactable
        if (currentInteractable != null)
        {
            // 叫舊的東西熄燈
            currentInteractable.OnLoseFocus();
            currentInteractable = null; // 清空紀錄
        }

        // 準心變白（若有設定）
        SetCrosshairColor(defaultColor);
    }

    void TryInteract()
    {
        // 這裡直接使用我們已經記住的 currentInteractable，不用再射一次雷射了，比較省效能
        if (currentInteractable != null)
        {
            currentInteractable.OnInteract();
        }
    }

    private void SetCrosshairColor(Color color)
    {
        if (crosshairImage != null)
        {
            crosshairImage.color = color;
        }
    }

    public void AddKey()
    {
        keyCount++;
        Debug.Log("撿到鑰匙了！目前鑰匙數量: " + keyCount);
    }

    public bool HasKey()
    {
        if (keyCount > 0)
        {
            keyCount--; 
            return true;
        }
        return false;
    }
}
