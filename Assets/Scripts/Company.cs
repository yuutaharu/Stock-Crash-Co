using System.Collections.Generic;
using UnityEngine;

public class Company : MonoBehaviour
{
    // companyId をキーに、ネットワークメッセージの宛先を解決するためのレジストリ
    public static readonly Dictionary<int, Company> Registry = new Dictionary<int, Company>();

    [Header("基本情報")]
    public int companyId = 0;
    public string companyName = "Burger Co.";
    public float basePrice = 100f;
    public float currentPrice = 100f;

    [Header("店舗パラメータ")]
    public float dirtiness = 0f;
    public float popularity = 0f;

    // 店ごとの経済性格。ブリーフィングでどちらを選ぶか意味を持たせるための非対称パラメータ
    // (例: バーガーは基準株価が高く値動きが緩やかな安定型、ピザは基準株価が安く値動きが激しい
    // ハイリスク型、といった味付けをSceneBuilder側で行う)。
    [Header("経済性格(店ごとに非対称)")]
    public float popularityMultiplier = 2f;
    public float dirtinessMultiplier = 3f;
    // ブリーフィング画面の企業選択ボタンに表示する、性格を一言で表すラベル(例:「安定型」)
    public string flavorLabel = "";

    // 敵工作員(EnemySaboteur)がこの企業を狙う時の激しさ。値動きの倍率と合わせて
    // ハイリスク型の店ほど敵も頻繁かつ強めに来る、という一貫した性格付けに使う。
    [Header("敵工作員の激しさ(店ごとに非対称)")]
    public float enemyStrikeIntervalMin = 10f;
    public float enemyStrikeIntervalMax = 18f;
    public float enemyDirtAmount = 8f;

    // 競合企業への参照。株価は自社の評判と相手の評判の"差"で決まるため、
    // 片方を汚す/掃除すると、その分だけもう片方の株価が逆向きに動く(合計が一定のゼロサム)。
    [Header("競合企業")]
    public Company rival;

    [Header("終盤兵器")]
    // ロケット砲で爆破されると true になり、株価は0のまま固定される（このマップ内では復活しない）
    public bool isBankrupt = false;

    [Header("ネットワーク")]
    public float stateBroadcastInterval = 0.2f;
    private float broadcastTimer = 0f;

    private bool hostChangeHooked = false;

    void OnEnable()
    {
        Registry[companyId] = this;
    }

    void OnDisable()
    {
        if (Registry.TryGetValue(companyId, out var existing) && existing == this)
        {
            Registry.Remove(companyId);
        }
        if (hostChangeHooked && NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnHostChanged -= HandleHostChanged;
        }
        hostChangeHooked = false;
    }

    // ホストが交代した瞬間、新ホストは待たずに直近の値を全員へ即時再送する
    private void HandleHostChanged(bool isNowHost)
    {
        if (isNowHost) broadcastTimer = stateBroadcastInterval;
    }

    void Update()
    {
        // NetworkManagerはシーン読み込みタイミング次第でOnEnable時点ではまだ存在しないことがあるため、
        // ここで初回だけ遅延購読する
        if (!hostChangeHooked && NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnHostChanged += HandleHostChanged;
            hostChangeHooked = true;
        }

        // ホストだけが実値を計算する。クライアントはホストからの同期を待つだけ。
        if (!IsHost()) return;

        CalculateStockPrice();
        BroadcastStateIfDue();
    }

    private bool IsHost() => NetworkManager.Instance == null || NetworkManager.Instance.IsHost;

    // dirtiness/popularityだけで決まる決定論的な価格。何も工作しなければ変動しない。
    // 自社の評判(popularity/dirtiness由来)と競合の評判の"差"で価格が決まるため、
    // 相手を汚す/自分を掃除するとその分だけ自分の株価が相対的に上がる(ゼロサム)。
    void CalculateStockPrice()
    {
        if (isBankrupt)
        {
            currentPrice = 0f;
            return;
        }

        float ownScore = (popularity * popularityMultiplier) - (dirtiness * dirtinessMultiplier);
        float rivalScore = rival != null ? (rival.popularity * rival.popularityMultiplier) - (rival.dirtiness * rival.dirtinessMultiplier) : 0f;
        float priceModifier = ownScore - rivalScore;
        currentPrice = Mathf.Max(1.0f, basePrice + priceModifier);
    }

    void BroadcastStateIfDue()
    {
        if (NetworkManager.Instance == null) return;

        broadcastTimer += Time.deltaTime;
        if (broadcastTimer < stateBroadcastInterval) return;
        broadcastTimer = 0f;

        int bought = MarketManager.Instance != null ? MarketManager.Instance.GetBoughtShares(companyId) : 0;

        byte[] msg = NetMessages.PackCompanyState(companyId, currentPrice, dirtiness, popularity, bought, isBankrupt);
        NetworkManager.Instance.SendToAll(msg, reliable: false);
    }

    // クライアント側でのみ呼ばれる。ホストから届いた状態をそのまま反映する。
    public void ApplyNetworkState(float price, float dirt, float pop, int boughtShares, bool bankrupt)
    {
        currentPrice = price;
        dirtiness = dirt;
        popularity = pop;
        isBankrupt = bankrupt;

        if (MarketManager.Instance != null)
        {
            MarketManager.Instance.ApplyNetworkPosition(companyId, boughtShares);
        }
    }

    // ローカル入力の入り口。ホストなら即実行、クライアントならホストへ要求を送るだけ。
    public void RequestAddDirt(float amount)
    {
        if (IsHost()) { AddDirt(amount); return; }
        NetworkManager.Instance.SendToHost(
            NetMessages.PackCompanyAction(NetMessageType.RequestAddDirt, companyId, amount), reliable: true);
    }

    public void RequestCleanDirt(float amount)
    {
        if (IsHost()) { CleanDirt(amount); return; }
        NetworkManager.Instance.SendToHost(
            NetMessages.PackCompanyAction(NetMessageType.RequestCleanDirt, companyId, amount), reliable: true);
    }

    public void RequestAddPopularity(float amount)
    {
        if (IsHost()) { AddPopularity(amount); return; }
        NetworkManager.Instance.SendToHost(
            NetMessages.PackCompanyAction(NetMessageType.RequestAddPopularity, companyId, amount), reliable: true);
    }

    // ローカル入力の入り口。ホストなら即実行、クライアントならホストへ要求を送るだけ。
    public void RequestRocketStrike()
    {
        if (IsHost()) { TriggerBankruptcy(); return; }
        NetworkManager.Instance.SendToHost(
            NetMessages.PackCompanyAction(NetMessageType.RequestRocketStrike, companyId, 0f), reliable: true);
    }

    // 実際の値変更。ホスト上、またはネットワーク未使用時にのみ呼ばれる想定。
    public void AddDirt(float amount) { dirtiness += amount; }
    public void CleanDirt(float amount) { dirtiness = Mathf.Max(0f, dirtiness - amount); }
    public void AddPopularity(float amount) { popularity += amount; }

    // ホスト権威モデルの実処理。
    public void TriggerBankruptcy()
    {
        if (isBankrupt) return;

        isBankrupt = true;
        currentPrice = 0f;
    }

    // もう一度遊ぶ際に呼ばれる。店舗の状態を初期値に戻し、投げつけられて張り付いたゴミも全て消す。
    public void ResetForNewMatch()
    {
        dirtiness = 0f;
        popularity = 0f;
        isBankrupt = false;
        currentPrice = basePrice;

        foreach (TrashMarker marker in GetComponentsInChildren<TrashMarker>())
        {
            Destroy(marker.gameObject);
        }
    }
}
