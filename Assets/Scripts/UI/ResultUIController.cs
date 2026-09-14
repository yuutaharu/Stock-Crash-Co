using TMPro;
using UnityEngine;

// コアループ4番目「リザルト」画面。今回はスキル強化は含めず、結果表示のみ。
public class ResultUIController : MonoBehaviour
{
    [Header("UI参照")]
    public TMP_Text resultTitleText;
    public TMP_Text finalMoneyText;

    void OnEnable()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (GameSessionManager.Instance == null || MarketManager.Instance == null) return;

        if (resultTitleText != null)
        {
            resultTitleText.text = GameSessionManager.Instance.MatchWon ? "目標達成！" : "未達成…";
        }

        if (finalMoneyText != null)
        {
            finalMoneyText.text =
                $"最終資金: ${MarketManager.Instance.playerMoney:F0} / 目標: ${GameSessionManager.Instance.targetAmount:F0}";
        }
    }
}
