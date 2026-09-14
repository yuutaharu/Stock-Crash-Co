using TMPro;
using UnityEngine;
using UnityEngine.UI;

// トレーディングUIの1企業分の行。価格・保有状況の表示とボタン操作を担当する。
// 実際の売買処理はMarketManagerのRequest系メソッド経由で行うため、
// ホスト権威モデルに沿った通信になる（このクラス自体はネットワークを意識しない）。
public class CompanyTradeRow : MonoBehaviour
{
    [Header("UI参照")]
    public TMP_Text companyNameText;
    public TMP_Text priceText;
    public TMP_Text positionText;
    public Button buyButton;
    public Button sellButton;
    public Button shortButton;
    public Button closeShortButton;

    private Company company;
    private int tradeAmount = 1;

    public void Setup(Company targetCompany, int amount)
    {
        company = targetCompany;
        tradeAmount = amount;

        if (companyNameText != null) companyNameText.text = company.companyName;

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => MarketManager.Instance.RequestBuyStock(company, tradeAmount));
        }

        if (sellButton != null)
        {
            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(() => MarketManager.Instance.RequestSellStock(company, tradeAmount));
        }

        if (shortButton != null)
        {
            shortButton.onClick.RemoveAllListeners();
            shortButton.onClick.AddListener(() => MarketManager.Instance.RequestShortStock(company, tradeAmount));
        }

        if (closeShortButton != null)
        {
            closeShortButton.onClick.RemoveAllListeners();
            closeShortButton.onClick.AddListener(() => MarketManager.Instance.RequestCloseShort(company));
        }
    }

    void Update()
    {
        if (company == null || MarketManager.Instance == null) return;

        if (priceText != null)
        {
            priceText.text = company.isBankrupt ? "破産" : $"${company.currentPrice:F2}";
        }

        int bought = MarketManager.Instance.GetBoughtShares(company.companyId);
        int shorted = MarketManager.Instance.GetShortShares(company.companyId);

        if (positionText != null)
        {
            positionText.text = $"保有: {bought}株 / 空売り: {shorted}株";
        }

        if (buyButton != null) buyButton.interactable = !company.isBankrupt;
        if (shortButton != null) shortButton.interactable = !company.isBankrupt;
        if (sellButton != null) sellButton.interactable = bought > 0;
        if (closeShortButton != null) closeShortButton.interactable = shorted > 0;
    }
}
