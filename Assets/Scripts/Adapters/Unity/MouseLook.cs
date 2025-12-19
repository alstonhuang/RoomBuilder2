using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public float mouseSensitivity = 900f;
    public Transform playerBody;

    float xRotation = 0f;

    [Header("Testing")]
    [Tooltip("When enabled, MouseLook ignores Input axes and uses Override Look Input instead (useful for automated tests).")]
    public bool useOverrideInput;

    [Tooltip("Override input delta (x=Mouse X axis, y=Mouse Y axis).")]
    public Vector2 overrideLookInput;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        Vector2 look = useOverrideInput
            ? overrideLookInput
            : new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        // Compatibility with legacy projects:
        // - If sensitivity is a large number (typical 50-200), treat it as "degrees per second" and scale by deltaTime.
        // - If sensitivity is a small number (typical 0.5-10), treat it as "degrees per frame" and don't scale by deltaTime.
        float scale = mouseSensitivity > 10f ? Time.deltaTime : 1f;
        float mouseX = look.x * mouseSensitivity * scale;
        float mouseY = look.y * mouseSensitivity * scale;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * mouseX);
        }
    }
}
