using UnityEngine;

public class MouseLook : MonoBehaviour
{
    // 滑鼠靈敏度，覺得太快太慢可以改這裡
    public float mouseSensitivity = 100f;

    // 這是我們要轉動的「身體」
    public Transform playerBody;

    float xRotation = 0f; // 用來紀錄攝影機上下看了多少度

    void Start()
    {
        // 遊戲開始時，把滑鼠游標隱藏並鎖定在畫面中央
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // 1. 取得滑鼠移動量
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // 2. 處理「上下看」（旋轉攝影機）
        xRotation -= mouseY;
        // 限制抬頭低頭的角度（-90度到90度之間），不然頭會轉到背後去
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // 套用旋轉到攝影機自己身上 (X軸旋轉)
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // 3. 處理「左右看」（旋轉玩家身體）
        // 注意：左右看的時候，是轉動整個身體（Player Body），而不只是頭
        playerBody.Rotate(Vector3.up * mouseX);
    }
}