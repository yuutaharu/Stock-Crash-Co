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
            tradingUI.Open(tradableCompanies);
        }
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
