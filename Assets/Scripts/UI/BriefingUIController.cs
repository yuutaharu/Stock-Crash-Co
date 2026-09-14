using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// コアループ1番目「ブリーフィング」画面。目標金額と対象企業を表示する。
// 「開始」操作はホストのみ可能（全員の進行状態を揃える必要があるため）で、
// ホストでないプレイヤーには待機中の表示を出す。
public class BriefingUIController : MonoBehaviour
{
    [Header("UI参照")]
    public TMP_Text targetAmountText;
    public TMP_Text targetCompaniesText;
    public TMP_Text timeLimitText;
    public Button startButton;
    public GameObject waitingForHostLabel;

    void OnEnable()
    {
        RefreshContent();

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(() => GameSessionManager.Instance.StartMatch());
        }
    }

    void Update()
    {
        // ホストかどうかは途中(ホスト離脱等)で変わる可能性があるため毎フレーム見た目を更新する
        bool isHost = NetworkManager.Instance == null || NetworkManager.Instance.IsHost;
        if (startButton != null) startButton.gameObject.SetActive(isHost);
        if (waitingForHostLabel != null) waitingForHostLabel.SetActive(!isHost);
    }

    private void RefreshContent()
    {
        if (GameSessionManager.Instance == null) return;

        if (targetAmountText != null)
        {
            targetAmountText.text = $"目標金額: ${GameSessionManager.Instance.targetAmount:F0}";
        }

        if (targetCompaniesText != null)
        {
            var names = new StringBuilder();
            foreach (Company company in GameSessionManager.Instance.targetCompanies)
            {
                if (company == null) continue;
                if (names.Length > 0) names.Append(" / ");
                names.Append(company.companyName);
            }
            targetCompaniesText.text = $"対象企業: {names}";
        }

        if (timeLimitText != null)
        {
            int minutes = Mathf.FloorToInt(GameSessionManager.Instance.matchDurationSeconds / 60f);
            int seconds = Mathf.FloorToInt(GameSessionManager.Instance.matchDurationSeconds % 60f);
            timeLimitText.text = $"制限時間: {minutes:00}:{seconds:00}";
        }
    }
}
