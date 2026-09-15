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

    [Header("企業選択")]
    // targetCompanies(GameSessionManager側)と同じ並び順で用意しておく
    public Button[] companyButtons;
    public TMP_Text selectedCompanyText;

    private bool hooked = false;

    void OnEnable()
    {
        hooked = false;
        TryInitialize();

        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(() => GameSessionManager.Instance.StartMatch());
        }
    }

    void Update()
    {
        // GameSessionManagerはスクリプト実行順序次第でOnEnable時点ではまだ存在しないことがあるため、
        // (他のUIスクリプトと同様に)ここで初期化できるまで毎フレーム試みる。
        if (!hooked) TryInitialize();

        // ホストかどうかは途中(ホスト離脱等)で変わる可能性があるため毎フレーム見た目を更新する
        bool isHost = NetworkManager.Instance == null || NetworkManager.Instance.IsHost;
        bool hasSelection = GameSessionManager.Instance != null && GameSessionManager.Instance.selectedCompany != null;

        if (startButton != null)
        {
            startButton.gameObject.SetActive(isHost);
            startButton.interactable = hasSelection;
        }
        if (waitingForHostLabel != null) waitingForHostLabel.SetActive(!isHost);

        if (selectedCompanyText != null)
        {
            selectedCompanyText.text = hasSelection
                ? $"担当企業: {GameSessionManager.Instance.selectedCompany.companyName}"
                : "担当企業を選んでください";
        }
    }

    private void TryInitialize()
    {
        if (GameSessionManager.Instance == null) return;

        RefreshContent();
        WireCompanyButtons();
        hooked = true;
    }

    private void WireCompanyButtons()
    {
        if (companyButtons == null || GameSessionManager.Instance == null) return;

        Company[] companies = GameSessionManager.Instance.targetCompanies;
        for (int i = 0; i < companyButtons.Length; i++)
        {
            if (companyButtons[i] == null) continue;
            if (companies == null || i >= companies.Length || companies[i] == null) continue;

            Company company = companies[i];
            companyButtons[i].onClick.RemoveAllListeners();
            companyButtons[i].onClick.AddListener(() => GameSessionManager.Instance.SelectCompany(company));
        }
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
