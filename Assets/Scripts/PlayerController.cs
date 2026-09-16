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

    // ゴミもモップも最初から持っているのではなく、マップに置かれたPickupItemを拾って初めて使えるようにする。
    // 持てるのは常に1つだけ(缶とモップを同時には持てない)。別の物を拾うと今持っている物と入れ替わる。
    [Header("持ち物(PickupItemを拾うと手に入る、同時に1つだけ)")]
    public GameObject trashViewModel;
    public GameObject mopViewModel;
    private enum HeldItem { None, Trash, Mop }
    private HeldItem heldItem = HeldItem.None;

    [Header("ネットワーク")]
    public int playerSlot = 0;
    public float transformBroadcastInterval = 0.1f;
    private float transformTimer = 0f;

    // 自分が操作するプレイヤーかどうか。他プレイヤーのアバターにはfalseを設定する
    // （プレイヤーのスポーン処理側で、ローカルSteamIDと一致するかを見て設定する想定）。
    private bool isLocalPlayer = true;

    private bool phaseHooked = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.freezeRotation = true;
        UpdateViewModels();
    }

    void Update()
    {
        // GameSessionManagerはスクリプト実行順序次第でStart時点ではまだ存在しないことがあるため、
        // ここで初回だけ遅延購読する
        if (!phaseHooked && GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.OnPhaseChanged += HandlePhaseChanged;
            phaseHooked = true;
        }

        if (!isLocalPlayer) return; // リモートプレイヤーはRemotePlayerRegistry経由の同期のみで動く
        if (!IsPlayingPhase()) return; // ブリーフィング/リザルト中は操作を受け付けない

        HandleMovement();
        HandleActions();
        BroadcastTransformIfDue();
    }

    void OnDestroy()
    {
        if (phaseHooked && GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        }
    }

    // もう一度あそぶ(周回)時に持ち物を空にし、マップ上の拾えるアイテムも全て出し直す。
    private void HandlePhaseChanged(GameSessionManager.GamePhase phase)
    {
        if (phase != GameSessionManager.GamePhase.Briefing) return;

        heldItem = HeldItem.None;
        UpdateViewModels();
        PickupItem.ResetAll();
    }

    private bool IsPlayingPhase() =>
        GameSessionManager.Instance == null ||
        GameSessionManager.Instance.CurrentPhase == GameSessionManager.GamePhase.Playing;

    public void SetIsLocalPlayer(bool value) { isLocalPlayer = value; }

    // PickupItemから呼ばれる。既に何かを持っている場合は今持っている物と入れ替える
    // (同じ種類を既に持っている場合だけ、意味が無いので拾わずfalseを返す)。
    public bool TryPickup(PickupItem.ItemType type)
    {
        HeldItem newItem = type == PickupItem.ItemType.Trash ? HeldItem.Trash : HeldItem.Mop;
        if (heldItem == newItem) return false;

        heldItem = newItem;
        UpdateViewModels();
        return true;
    }

    private void UpdateViewModels()
    {
        if (trashViewModel != null) trashViewModel.SetActive(heldItem == HeldItem.Trash);
        if (mopViewModel != null) mopViewModel.SetActive(heldItem == HeldItem.Mop);
    }

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

    // Eキー1つで「持っている物」に応じたアクションを行う
    // (ゴミを持っていれば投げる、モップを持っていて店舗の近くにいれば掃除する)。
    void HandleActions()
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;

        if (heldItem == HeldItem.Trash)
        {
            ThrowTrash();
        }
        else if (heldItem == HeldItem.Mop && targetCompany != null)
        {
            targetCompany.RequestCleanDirt(cleanAmount);
            RemoveNearestTrashMark(targetCompany);
        }
    }

    // ゴミを前方へ投げる。狙いを付けて実際に店舗に当てないと汚れない
    // (以前のような、近づいてキーを押すだけの即時汚し工作は廃止)。
    // 投げると手持ちのゴミは無くなるので、また地面のPickupItemを拾いに行く必要がある。
    private void ThrowTrash()
    {
        if (trashPrefab == null || throwOrigin == null) return;
        if (Time.time < nextThrowTime) return;
        nextThrowTime = Time.time + throwCooldownSeconds;

        heldItem = HeldItem.None;
        UpdateViewModels();

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
