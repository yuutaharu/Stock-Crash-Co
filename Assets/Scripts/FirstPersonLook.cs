using UnityEngine;

// 一人称視点のマウスルック。Playerの子カメラに付ける想定。
// 左右(Yaw)は親(プレイヤー本体)を回し、上下(Pitch)はこのカメラ自身だけを回す。
public class FirstPersonLook : MonoBehaviour
{
    public float mouseSensitivity = 2.5f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    private float pitch = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        mouseSensitivity = PlayerPrefs.GetFloat("Settings_MouseSensitivity", mouseSensitivity);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }

        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        if (transform.parent != null)
        {
            transform.parent.Rotate(Vector3.up * mouseX);
        }

        pitch = Mathf.Clamp(pitch - mouseY, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
