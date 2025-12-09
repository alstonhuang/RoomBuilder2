using UnityEngine;

public class DoorController : MonoBehaviour
{
    public bool isLocked = true;
    
    // 門打開的角度 / 關閉的角度
    private float openAngle = 90f; 
    private float closeAngle = 0f;
    
    private bool isOpen = false;

    void Update()
    {
        // 平滑旋轉邏輯
        float targetAngleY = isOpen ? openAngle : closeAngle;
        Quaternion targetRotation = Quaternion.Euler(0, targetAngleY, 0);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * 5f);
    }

    // 這個函式是用來被 Interactable 呼叫的
    public void TryOpen()
    {
        // 如果門是鎖著的
        if (isLocked)
        {
            // 1. 找到玩家 (大腦)
            PlayerInteraction player = FindAnyObjectByType<PlayerInteraction>();
            
            // 2. 檢查玩家是否存在，而且「有沒有鑰匙 (HasKey)」
            if (player != null && player.HasKey())
            {
                // 有鑰匙！解鎖並開門
                UnlockDoor();
                isOpen = true; 
                Debug.Log("使用了鑰匙，門開了！");
            }
            else
            {
                // 沒鑰匙，或者找不到玩家
                Debug.Log("門鎖住了！需要鑰匙。");
            }
        }
        else
        {
            // 如果本來就沒鎖，直接開關
            isOpen = !isOpen; 
        }
    }

    public void UnlockDoor()
    {
        isLocked = false;
        Debug.Log("門已解鎖！");
    }
}