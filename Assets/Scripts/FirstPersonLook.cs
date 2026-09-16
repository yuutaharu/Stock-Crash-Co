using UnityEngine;

// 一人称視点のマウスルック。Playerの子カメラに付ける想定。
// 左右(Yaw)は親(プレイヤー本体)を回し、上下(Pitch)はこのカメラ自身だけを回す。
//
// カーソルのロック/表示はここで一括管理する。「プレイ中(GamePhase.Playing)かつ、
// タイトル画面も設定パネルも取引画面も表示されていない」時だけロックし、それ以外
// (タイトル画面/ブリーフィング/リザルト/設定パネル/取引画面 表示中)は常にカーソルを出しておく。
// こうしないと、それらの画面が出た瞬間にカーソルが隠れてボタンを押せなくなるため。
public class FirstPersonLook : MonoBehaviour
{
    public float mouseSensitivity = 2.5f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    private float pitch = 0f;

    void Start()
    {
        mouseSensitivity = PlayerPrefs.GetFloat("Settings_MouseSensitivity", mouseSensitivity);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        bool isPlaying = GameSessionManager.Instance != null &&
            GameSessionManager.Instance.CurrentPhase == GameSessionManager.GamePhase.Playing;
        bool shouldLock = isPlaying && !SettingsUIController.IsOpen && !TitleScreenController.IsShowing && !TradingUIController.IsOpen;

        bool isLocked = Cursor.lockState == CursorLockMode.Locked;
        if (shouldLock != isLocked)
        {
            Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !shouldLock;
        }

        if (!shouldLock) return;

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
