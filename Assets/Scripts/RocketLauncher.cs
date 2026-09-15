using UnityEngine;

// 終盤兵器。目標金額を達成すると、相手企業(GameSessionManager.rivalCompany)側の
// このランチャーだけが出現し、Eキーを長押しすることで対象企業を即破産させる。
public class RocketLauncher : MonoBehaviour
{
    [Header("対象")]
    public Company targetCompany;

    [Header("設定")]
    public float holdSecondsToFire = 2.0f;
    public KeyCode fireKey = KeyCode.E;

    [Header("演出")]
    public ParticleSystem fireEffect;

    private bool isPlayerNearby = false;
    private float holdTimer = 0f;
    private bool hasAppeared = false;

    private MeshRenderer meshRenderer;
    private Collider[] colliders;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        colliders = GetComponents<Collider>();
        SetAppearance(false);
    }

    void Update()
    {
        bool shouldAppear = GameSessionManager.Instance != null &&
            GameSessionManager.Instance.MoneyGoalReached &&
            GameSessionManager.Instance.rivalCompany == targetCompany;

        if (shouldAppear != hasAppeared)
        {
            SetAppearance(shouldAppear);
        }

        bool isPlayingPhase = GameSessionManager.Instance == null ||
            GameSessionManager.Instance.CurrentPhase == GameSessionManager.GamePhase.Playing;

        if (!hasAppeared || !isPlayingPhase)
        {
            holdTimer = 0f;
            return;
        }

        if (isPlayerNearby && Input.GetKey(fireKey))
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= holdSecondsToFire)
            {
                Fire();
                holdTimer = 0f;
            }
        }
        else
        {
            holdTimer = 0f;
        }
    }

    // 0〜1。発射までの溜め具合。ゲージ等の演出を出したい場合に外部から参照する。
    public float ChargeProgress01 => holdSecondsToFire > 0f ? Mathf.Clamp01(holdTimer / holdSecondsToFire) : 0f;

    // 現在Eキーを押し続けてチャージ中かどうか。HUD側がゲージの表示/非表示を判断するのに使う。
    public bool IsCharging => hasAppeared && isPlayerNearby && holdTimer > 0f;

    private void SetAppearance(bool visible)
    {
        hasAppeared = visible;
        if (meshRenderer != null) meshRenderer.enabled = visible;
        if (colliders != null)
        {
            foreach (Collider c in colliders) c.enabled = visible;
        }
    }

    private void Fire()
    {
        if (targetCompany == null || targetCompany.isBankrupt) return;

        if (fireEffect != null) fireEffect.Play();
        targetCompany.RequestRocketStrike();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) isPlayerNearby = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            holdTimer = 0f;
        }
    }
}
