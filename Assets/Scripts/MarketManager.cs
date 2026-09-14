using System.Collections.Generic;
using UnityEngine;

// チーム共有の資金・ポジションを管理する。「目標金額の達成を目指す」という
// コアループ上の目標が個人ではなくチーム単位のため、資金はチームで1つの共有プールとして扱う。
// （個人ごとの資産にしたい場合はプレイヤーIDをキーにした辞書に作り直す必要あり）
public class MarketManager : MonoBehaviour
{
    public static MarketManager Instance { get; private set; }

    [Header("チーム共有資金")]
    public float playerMoney = 1000f;

    // ポジションは企業(companyId)ごとに独立して管理する。
    // グローバル単一の値にすると、複数企業を同時に売買した時に保有株数が混ざってしまうため。
    private class PositionData
    {
        public int boughtShares;
        public int shortShares;
        public float shortEntryPrice;
    }
    private readonly Dictionary<int, PositionData> positions = new Dictionary<int, PositionData>();

    private PositionData GetOrCreatePosition(int companyId)
    {
        if (!positions.TryGetValue(companyId, out PositionData pos))
        {
            pos = new PositionData();
            positions[companyId] = pos;
        }
        return pos;
    }

    public int GetBoughtShares(int companyId) => GetOrCreatePosition(companyId).boughtShares;
    public int GetShortShares(int companyId) => GetOrCreatePosition(companyId).shortShares;
    public float GetShortEntryPrice(int companyId) => GetOrCreatePosition(companyId).shortEntryPrice;

    [Header("ネットワーク")]
    public float walletBroadcastInterval = 0.5f;
    private float broadcastTimer = 0f;
    private float lastBroadcastMoney = float.NaN;

    private bool hostChangeHooked = false;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (hostChangeHooked && NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnHostChanged -= HandleHostChanged;
        }
    }

    // ホストが交代した瞬間、新ホストは待たずに直近の資金を全員へ即時再送する
    private void HandleHostChanged(bool isNowHost)
    {
        if (isNowHost)
        {
            broadcastTimer = walletBroadcastInterval;
            lastBroadcastMoney = float.NaN; // Approximatelyでスキップされないよう強制的に送信対象にする
        }
    }

    void Update()
    {
        // NetworkManagerはシーン読み込みタイミング次第でAwake時点ではまだ存在しないことがあるため、
        // ここで初回だけ遅延購読する
        if (!hostChangeHooked && NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnHostChanged += HandleHostChanged;
            hostChangeHooked = true;
        }

        if (!IsHost()) return;
        BroadcastWalletIfDue();
    }

    private bool IsHost() => NetworkManager.Instance == null || NetworkManager.Instance.IsHost;

    void BroadcastWalletIfDue()
    {
        if (NetworkManager.Instance == null) return;

        broadcastTimer += Time.deltaTime;
        if (broadcastTimer < walletBroadcastInterval) return;
        broadcastTimer = 0f;

        if (Mathf.Approximately(playerMoney, lastBroadcastMoney)) return;
        lastBroadcastMoney = playerMoney;

        NetworkManager.Instance.SendToAll(NetMessages.PackWalletState(playerMoney), reliable: true);
    }

    // クライアント入力の入り口。ホストなら即実行、クライアントならホストへ要求を送るだけ。
    public void RequestBuyStock(Company company, int amount)
    {
        if (IsHost()) { BuyStock(company, amount); return; }
        NetworkManager.Instance.SendToHost(
            NetMessages.PackTradeAction(NetMessageType.RequestBuyStock, company.companyId, amount), reliable: true);
    }

    public void RequestShortStock(Company company, int amount)
    {
        if (IsHost()) { ShortStock(company, amount); return; }
        NetworkManager.Instance.SendToHost(
            NetMessages.PackTradeAction(NetMessageType.RequestShortStock, company.companyId, amount), reliable: true);
    }

    public void RequestCloseShort(Company company)
    {
        if (IsHost()) { CloseShortPosition(company); return; }
        NetworkManager.Instance.SendToHost(
            NetMessages.PackTradeAction(NetMessageType.RequestCloseShort, company.companyId, 0), reliable: true);
    }

    public void RequestSellStock(Company company, int amount)
    {
        if (IsHost()) { SellStock(company, amount); return; }
        NetworkManager.Instance.SendToHost(
            NetMessages.PackTradeAction(NetMessageType.RequestSellStock, company.companyId, amount), reliable: true);
    }

    // 実処理。ホスト上、またはネットワーク未使用時にのみ呼ばれる想定。
    public void BuyStock(Company company, int amount)
    {
        float cost = company.currentPrice * amount;
        if (playerMoney >= cost)
        {
            playerMoney -= cost;
            GetOrCreatePosition(company.companyId).boughtShares += amount;
        }
    }

    public void ShortStock(Company company, int amount)
    {
        PositionData pos = GetOrCreatePosition(company.companyId);
        pos.shortShares += amount;
        pos.shortEntryPrice = company.currentPrice;
        playerMoney += company.currentPrice * amount;
    }

    public void CloseShortPosition(Company company)
    {
        PositionData pos = GetOrCreatePosition(company.companyId);
        if (pos.shortShares > 0)
        {
            float buyBackCost = company.currentPrice * pos.shortShares;
            playerMoney -= buyBackCost;
            pos.shortShares = 0;
        }
    }

    // 現物買いポジションを売却して利益を確定する。保有数を超える分は保有数に切り詰める。
    public void SellStock(Company company, int amount)
    {
        PositionData pos = GetOrCreatePosition(company.companyId);
        int sellAmount = Mathf.Min(amount, pos.boughtShares);
        if (sellAmount <= 0) return;

        pos.boughtShares -= sellAmount;
        playerMoney += company.currentPrice * sellAmount;
    }

    // クライアント側でのみ呼ばれる。ホストから届いた資金をそのまま反映する。
    public void ApplyNetworkWallet(float teamMoney)
    {
        playerMoney = teamMoney;
    }

    // クライアント側でのみ呼ばれる。ホストから届いたポジションをそのまま反映する。
    public void ApplyNetworkPosition(int companyId, int boughtShares, int shortShares, float shortEntryPrice)
    {
        PositionData pos = GetOrCreatePosition(companyId);
        pos.boughtShares = boughtShares;
        pos.shortShares = shortShares;
        pos.shortEntryPrice = shortEntryPrice;
    }
}
