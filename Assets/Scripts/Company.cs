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

    [Header("終盤兵器")]
    // ロケット砲で爆破されると true になり、株価は0のまま固定される（このマップ内では復活しない）
    public bool isBankrupt = false;
    // trueの場合、この企業に空売りポジションを持っていないとロケットで爆破できない
    // （ホスト側で検証するため、発射条件はここに置く。UI/ワールド上のランチャー側の見た目チェックはこの値を参照する）
    public bool requireShortPositionToFire = true;

    [Header("市場ノイズ")]
    // 毎フレーム再抽選すると数字がチラついて読めないため、一定間隔でのみ再抽選する
    public float noiseRefreshInterval = 1.0f;
    private float noiseTimer = 0f;
    private float currentNoise = 0f;

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

    void CalculateStockPrice()
    {
        if (isBankrupt)
        {
            currentPrice = 0f;
            return;
        }

        noiseTimer += Time.deltaTime;
        if (noiseTimer >= noiseRefreshInterval)
        {
            noiseTimer = 0f;
            currentNoise = Random.Range(-0.5f, 0.5f);
        }

        float priceModifier = (popularity * 2.0f) - (dirtiness * 3.0f);
        currentPrice = Mathf.Max(1.0f, basePrice + priceModifier + currentNoise);
    }

    void BroadcastStateIfDue()
    {
        if (NetworkManager.Instance == null) return;

        broadcastTimer += Time.deltaTime;
        if (broadcastTimer < stateBroadcastInterval) return;
        broadcastTimer = 0f;

        int bought = 0, shorted = 0;
        float shortEntry = 0f;
        if (MarketManager.Instance != null)
        {
            bought = MarketManager.Instance.GetBoughtShares(companyId);
            shorted = MarketManager.Instance.GetShortShares(companyId);
            shortEntry = MarketManager.Instance.GetShortEntryPrice(companyId);
        }

        byte[] msg = NetMessages.PackCompanyState(companyId, currentPrice, dirtiness, popularity, bought, shorted, shortEntry, isBankrupt);
        NetworkManager.Instance.SendToAll(msg, reliable: false);
    }

    // クライアント側でのみ呼ばれる。ホストから届いた状態をそのまま反映する。
    public void ApplyNetworkState(float price, float dirt, float pop, int boughtShares, int shortShares, float shortEntryPrice, bool bankrupt)
    {
        currentPrice = price;
        dirtiness = dirt;
        popularity = pop;
        isBankrupt = bankrupt;

        if (MarketManager.Instance != null)
        {
            MarketManager.Instance.ApplyNetworkPosition(companyId, boughtShares, shortShares, shortEntryPrice);
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

    // ホスト権威モデルの実処理。発射条件はここで最終検証する
    // （ワールド上のRocketLauncher側のチェックは見た目上のガードに過ぎない）。
    public void TriggerBankruptcy()
    {
        if (isBankrupt) return;

        if (requireShortPositionToFire)
        {
            bool hasShortPosition = MarketManager.Instance != null && MarketManager.Instance.GetShortShares(companyId) > 0;
            if (!hasShortPosition)
            {
                Debug.Log($"[Company] {companyName} への空売りポジションがないためロケットを発射できません。");
                return;
            }
        }

        isBankrupt = true;
        currentPrice = 0f;
    }
}
