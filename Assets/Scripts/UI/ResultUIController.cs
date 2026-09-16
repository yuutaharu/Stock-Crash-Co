using TMPro;
using UnityEngine;
using UnityEngine.UI;

// コアループ4番目「リザルト」画面。今回はスキル強化は含めず、結果表示のみ。
public class ResultUIController : MonoBehaviour
{
    [Header("UI参照")]
    public TMP_Text resultTitleText;
    public TMP_Text finalMoneyText;
    public Button playAgainButton;

    [Header("効果音")]
    public AudioClip winSound;
    public AudioClip loseSound;
    public AudioClip clickSound;

    void Awake()
    {
        if (playAgainButton != null)
        {
            playAgainButton.onClick.AddListener(HandlePlayAgain);
        }
    }

    void OnEnable()
    {
        Refresh();
    }

    private void HandlePlayAgain()
    {
        Sfx.Play(clickSound);
        if (GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.ResetForNewMatch();
        }
    }

    private void Refresh()
    {
        if (GameSessionManager.Instance == null || MarketManager.Instance == null) return;

        bool won = GameSessionManager.Instance.MatchWon;
        if (resultTitleText != null)
        {
            resultTitleText.text = won ? "目標達成！" : "未達成…";
        }
        Sfx.Play(won ? winSound : loseSound);

        if (finalMoneyText != null)
        {
            finalMoneyText.text =
                $"最終資金: ${MarketManager.Instance.playerMoney:F0} / 目標: ${GameSessionManager.Instance.targetAmount:F0}";
        }
    }
}
