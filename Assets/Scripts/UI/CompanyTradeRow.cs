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
    public Slider amountSlider; // 売買する株数をバーで選ぶ(0〜今可能な最大数)
    public TMP_Text amountText;
    public Button buyButton;
    public Button sellButton;

    [Header("効果音")]
    public AudioClip clickSound;

    private Company company;

    public void Setup(Company targetCompany)
    {
        company = targetCompany;

        if (companyNameText != null) companyNameText.text = company.companyName;

        if (amountSlider != null)
        {
            amountSlider.wholeNumbers = true;
            amountSlider.minValue = 0;
            amountSlider.maxValue = 1;
            amountSlider.value = 0;
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() =>
            {
                int amount = CurrentAmount();
                if (amount <= 0) return;

                Sfx.Play(clickSound);
                MarketManager.Instance.RequestBuyStock(company, amount);
            });
        }

        if (sellButton != null)
        {
            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(() =>
            {
                int amount = CurrentAmount();
                if (amount <= 0) return;

                Sfx.Play(clickSound);
                MarketManager.Instance.RequestSellStock(company, amount);
            });
        }
    }

    private int CurrentAmount() => amountSlider != null ? Mathf.RoundToInt(amountSlider.value) : 0;

    void Update()
    {
        if (company == null || MarketManager.Instance == null) return;

        if (priceText != null)
        {
            priceText.text = company.isBankrupt ? "破産" : $"${company.currentPrice:F2}";
        }

        int held = MarketManager.Instance.GetBoughtShares(company.companyId);
        if (positionText != null)
        {
            positionText.text = $"保有: {held}株";
        }

        int maxAffordable = company.currentPrice > 0f
            ? Mathf.FloorToInt(MarketManager.Instance.playerMoney / company.currentPrice)
            : 0;

        // バーの上限は「今買える数」と「今売れる数(保有数)」の大きい方。
        // 資金/株価/保有が変動するたびに追従させつつ、選択中の株数はその範囲に収める。
        int sliderMax = Mathf.Max(1, maxAffordable, held);
        if (amountSlider != null)
        {
            if (!Mathf.Approximately(amountSlider.maxValue, sliderMax)) amountSlider.maxValue = sliderMax;
            amountSlider.value = Mathf.Clamp(amountSlider.value, 0, sliderMax);
        }

        int amount = CurrentAmount();
        if (amountText != null) amountText.text = $"{amount}株";

        if (buyButton != null) buyButton.interactable = !company.isBankrupt && amount > 0 && amount <= maxAffordable;
        if (sellButton != null) sellButton.interactable = amount > 0 && amount <= held;
    }
}
