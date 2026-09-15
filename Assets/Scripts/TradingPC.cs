using UnityEngine;

public class TradingPC : MonoBehaviour
{
    [Header("参照")]
    public Company[] tradableCompanies;
    public TradingUIController tradingUI;

    private bool isPlayerNearby = false;

    void Start()
    {
        if (tradingUI != null) tradingUI.Close();
    }

    void Update()
    {
        bool isPlayingPhase = GameSessionManager.Instance == null ||
            GameSessionManager.Instance.CurrentPhase == GameSessionManager.GamePhase.Playing;
        if (!isPlayingPhase)
        {
            if (tradingUI != null) tradingUI.Close();
            return;
        }

        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            TogglePCScreen();
        }
    }

    void TogglePCScreen()
    {
        if (tradingUI == null) return;

        if (tradingUI.gameObject.activeSelf)
        {
            tradingUI.Close();
        }
        else
        {
            tradingUI.Open(GetTradableCompanies());
        }
    }

    // ブリーフィングで担当企業を選んでいる場合はその企業の株だけ取引できるようにする。
    // GameSessionManager未使用(単体テスト等)の場合はtradableCompanies全体を使う。
    private Company[] GetTradableCompanies()
    {
        if (GameSessionManager.Instance != null && GameSessionManager.Instance.selectedCompany != null)
        {
            return new Company[] { GameSessionManager.Instance.selectedCompany };
        }
        return tradableCompanies;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            if (tradingUI != null) tradingUI.Close();
        }
    }
}
