using UnityEngine;

public class KeyController : MonoBehaviour
{
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