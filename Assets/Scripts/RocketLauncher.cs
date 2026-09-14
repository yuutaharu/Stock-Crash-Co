using UnityEngine;

// 終盤兵器：対象企業に向けてロケットを発射し、株価を即座に0にする（破産）。
// 発射条件（空売りポジションが必要かどうか）の最終判定はCompany.TriggerBankruptcy()側
// （ホスト上）で行われる。ここでのCanFire()はプレイヤーへの見た目のフィードバック用。
public class RocketLauncher : MonoBehaviour
{
    [Header("対象")]
    public Company targetCompany;

    [Header("設定")]
    public float cooldownSeconds = 60f;
    public KeyCode fireKey = KeyCode.R;

    private bool isPlayerNearby = false;
    private float cooldownRemaining = 0f;

    void Update()
    {
        if (cooldownRemaining > 0f)
        {
            cooldownRemaining -= Time.deltaTime;
        }

        bool isPlayingPhase = GameSessionManager.Instance == null ||
            GameSessionManager.Instance.CurrentPhase == GameSessionManager.GamePhase.Playing;
        if (!isPlayingPhase) return;

        if (isPlayerNearby && Input.GetKeyDown(fireKey))
        {
            TryFire();
        }
    }

    public bool CanFire()
    {
        if (targetCompany == null) return false;
        if (targetCompany.isBankrupt) return false;
        if (cooldownRemaining > 0f) return false;

        if (targetCompany.requireShortPositionToFire)
        {
            if (MarketManager.Instance == null) return false;
            if (MarketManager.Instance.GetShortShares(targetCompany.companyId) <= 0) return false;
        }

        return true;
    }

    public void TryFire()
    {
        if (!CanFire())
        {
            Debug.Log("[RocketLauncher] 発射条件を満たしていません（空売りポジション、クールダウン、対象未設定のいずれか）。");
            return;
        }

        targetCompany.RequestRocketStrike();
        cooldownRemaining = cooldownSeconds;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) isPlayerNearby = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) isPlayerNearby = false;
    }
}
