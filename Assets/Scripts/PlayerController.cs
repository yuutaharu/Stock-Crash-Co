using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 6.0f;
    public float sprintSpeed = 10.0f;
    public KeyCode sprintKey = KeyCode.LeftShift;
    private Rigidbody rb;
    public Company targetCompany;

    [Header("ゴミ投げ(暴落工作)")]
    public GameObject trashPrefab;
    public Transform throwOrigin; // 通常はFPSカメラのtransform
    public float throwForce = 12f;
    public float throwCooldownSeconds = 0.4f;
    private float nextThrowTime = 0f;

    [Header("掃除(高騰工作)")]
    public float cleanAmount = 10f;

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
            float speed = Input.GetKey(sprintKey) ? sprintSpeed : moveSpeed;
            transform.Translate(moveDirection * speed * Time.deltaTime, Space.World);
        }
    }

    void HandleActions()
    {
        if (Input.GetKeyDown(KeyCode.E)) { ThrowTrash(); }

        if (targetCompany != null && Input.GetKeyDown(KeyCode.F))
        {
            targetCompany.RequestCleanDirt(cleanAmount);
            RemoveNearestTrashMark(targetCompany);
        }
    }

    // ゴミを前方へ投げる。狙いを付けて実際に店舗に当てないと汚れない
    // (以前のような、近づいてキーを押すだけの即時汚し工作は廃止)。
    private void ThrowTrash()
    {
        if (trashPrefab == null || throwOrigin == null) return;
        if (Time.time < nextThrowTime) return;
        nextThrowTime = Time.time + throwCooldownSeconds;

        GameObject trash = Instantiate(trashPrefab, throwOrigin.position + throwOrigin.forward * 0.6f, Random.rotation);
        Rigidbody trashRb = trash.GetComponent<Rigidbody>();
        if (trashRb != null)
        {
            trashRb.AddForce(throwOrigin.forward * throwForce, ForceMode.VelocityChange);
        }
    }

    // 掃除は近くにある張り付いたゴミも1つ取り除く(見た目と数値を一致させる)。
    private void RemoveNearestTrashMark(Company company)
    {
        TrashMarker[] marks = company.GetComponentsInChildren<TrashMarker>();
        if (marks.Length == 0) return;

        TrashMarker nearest = null;
        float bestDistance = float.MaxValue;
        foreach (TrashMarker mark in marks)
        {
            float distance = Vector3.Distance(mark.transform.position, transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = mark;
            }
        }
        if (nearest != null) Destroy(nearest.gameObject);
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
