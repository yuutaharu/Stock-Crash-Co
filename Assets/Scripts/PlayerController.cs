using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 6.0f;
    private Rigidbody rb;
    public Company targetCompany;

    [Header("ネットワーク")]
    public int playerSlot = 0;
    public float transformBroadcastInterval = 0.1f;
    private float transformTimer = 0f;

    // 自分が操作するプレイヤーかどうか。他プレイヤーのアバターにはfalseを設定する
    // （プレイヤーのスポーン処理側で、ローカルSteamIDと一致するかを見て設定する想定）。
    private bool isLocalPlayer = true;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.freezeRotation = true;
    }

    void Update()
    {
        if (!isLocalPlayer) return; // リモートプレイヤーはRemotePlayerRegistry経由の同期のみで動く
        if (!IsPlayingPhase()) return; // ブリーフィング/リザルト中は操作を受け付けない

        HandleMovement();
        HandleActions();
        BroadcastTransformIfDue();
    }

    private bool IsPlayingPhase() =>
        GameSessionManager.Instance == null ||
        GameSessionManager.Instance.CurrentPhase == GameSessionManager.GamePhase.Playing;

    public void SetIsLocalPlayer(bool value) { isLocalPlayer = value; }

    void HandleMovement()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        // 一人称視点のため、移動方向はプレイヤー自身の向き(マウスで回転済み)基準にする。
        // 見た目の向きを移動方向に合わせる従来方式(見下ろし視点向け)は使わない。
        Vector3 forward = transform.forward; forward.y = 0f; forward.Normalize();
        Vector3 right = transform.right; right.y = 0f; right.Normalize();
        Vector3 moveDirection = Vector3.ClampMagnitude(forward * moveZ + right * moveX, 1f);

        if (moveDirection.magnitude > 0.1f)
        {
            transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);
        }
    }

    void HandleActions()
    {
        if (targetCompany == null) return;

        if (Input.GetKeyDown(KeyCode.E)) { targetCompany.RequestAddDirt(10f); }
        if (Input.GetKeyDown(KeyCode.F)) { targetCompany.RequestCleanDirt(10f); }
    }

    void BroadcastTransformIfDue()
    {
        if (NetworkManager.Instance == null) return;

        transformTimer += Time.deltaTime;
        if (transformTimer < transformBroadcastInterval) return;
        transformTimer = 0f;

        byte[] msg = NetMessages.PackTransform(playerSlot, transform.position, transform.eulerAngles.y);
        NetworkManager.Instance.SendToAll(msg, reliable: false);
    }

    private void OnTriggerEnter(Collider other)
    {
        Company company = other.GetComponent<Company>();
        if (company != null) targetCompany = company;
    }

    private void OnTriggerExit(Collider other)
    {
        Company company = other.GetComponent<Company>();
        if (company != null && company == targetCompany) targetCompany = null;
    }
}
