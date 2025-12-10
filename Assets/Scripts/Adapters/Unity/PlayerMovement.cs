using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float gravity = -9.81f; // 重力

    private CharacterController controller;
    private Vector3 velocity; // 用來處理墜落速度

    void Start()
    {
        // 自動抓取身上的 Controller
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // 1. 處理移動 (WASD)
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        // 算出移動方向
        Vector3 move = transform.right * x + transform.forward * z;

        // 【關鍵改變】使用 controller.Move 來移動，它會自動擋住牆壁
        controller.Move(move * moveSpeed * Time.deltaTime);

        // 2. 處理重力 (不然你會浮在半空中)
        // 如果在地上，且速度向下，重置速度
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}