using System;
using UnityEngine;

// コアループの進行(ブリーフィング→プレイ→リザルト)を管理するホスト権威のステートマシン。
// フェーズの決定はホストだけが行い、結果をStateMatchPhaseメッセージで全員に配る。
public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance { get; private set; }

    public enum GamePhase : byte { Briefing = 0, Playing = 1, Result = 2 }

    [Header("目標設定")]
    public float targetAmount = 5000f;
    public Company[] targetCompanies;

    // ブリーフィングで選んだ「自分たちが取引する企業」と、その裏で破壊対象になる「相手企業」。
    // SelectCompany()で確定するまではどちらもnull。
    public Company selectedCompany;
    public Company rivalCompany;

    [Header("時間制限")]
    // この時間内に目標金額へ届かなければ敗北になる
    public float matchDurationSeconds = 300f;
    public float timerBroadcastInterval = 0.5f;

    public GamePhase CurrentPhase { get; private set; } = GamePhase.Briefing;
    public bool MatchWon { get; private set; } = false;
    public float RemainingTime { get; private set; } = 0f;

    // 目標金額に到達済みかどうか。到達しても即勝利にはせず、相手企業側にロケット砲を出現させるだけ。
    // 実際の勝利はrivalCompanyを爆破した時点。
    public bool MoneyGoalReached { get; private set; } = false;

    // UI側はこれを購読してパネルの表示/非表示を切り替える
    public event Action<GamePhase> OnPhaseChanged;

    private bool hostChangeHooked = false;
    private float timerBroadcastTimer = 0f;

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

    void Update()
    {
        // NetworkManagerの生成タイミング次第でAwake時点ではまだ存在しないため、ここで遅延購読する
        if (!hostChangeHooked && NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnHostChanged += HandleHostChanged;
            hostChangeHooked = true;
        }

        if (CurrentPhase != GamePhase.Playing) return;

        // 残り時間はホスト/クライアント問わず毎フレーム減らして滑らかに表示しつつ、
        // ホストからの定期ブロードキャストでズレを補正する。
        RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);

        if (!IsHost()) return;

        if (!MoneyGoalReached && MarketManager.Instance != null && MarketManager.Instance.playerMoney >= targetAmount)
        {
            MoneyGoalReached = true;
        }

        if (rivalCompany != null && rivalCompany.isBankrupt)
        {
            EndMatch(won: true);
        }
        else if (RemainingTime <= 0f)
        {
            EndMatch(won: false);
        }
        else
        {
            BroadcastTimerIfDue();
        }
    }

    private void BroadcastTimerIfDue()
    {
        if (NetworkManager.Instance == null) return;

        timerBroadcastTimer += Time.deltaTime;
        if (timerBroadcastTimer < timerBroadcastInterval) return;
        timerBroadcastTimer = 0f;

        NetworkManager.Instance.SendToAll(NetMessages.PackMatchTimer(RemainingTime), reliable: false);
    }

    // クライアント側でのみ呼ばれる。ホストから届いた残り時間でローカルのズレを補正する。
    public void ApplyNetworkTimer(float remainingTime)
    {
        RemainingTime = remainingTime;
    }

    private bool IsHost() => NetworkManager.Instance == null || NetworkManager.Instance.IsHost;

    // ホストが交代した時、新ホストは今の状態を全員へ即時再送する
    private void HandleHostChanged(bool isNowHost)
    {
        if (isNowHost) BroadcastPhase();
    }

    // ブリーフィング画面の企業選択ボタンから呼ぶ想定。ホスト、かつブリーフィング中のみ選び直せる。
    public void SelectCompany(Company company)
    {
        if (!IsHost()) return;
        if (CurrentPhase != GamePhase.Briefing) return;
        if (company == null || targetCompanies == null) return;

        selectedCompany = company;
        rivalCompany = null;
        foreach (Company candidate in targetCompanies)
        {
            if (candidate != null && candidate != company)
            {
                rivalCompany = candidate;
                break;
            }
        }
    }

    // ブリーフィング画面の「開始」ボタンから呼ぶ想定。ホストのみ実行できる。
    public void StartMatch()
    {
        if (!IsHost()) return;
        if (selectedCompany == null) return;

        MoneyGoalReached = false;
        RemainingTime = matchDurationSeconds;
        SetPhase(GamePhase.Playing, won: false);
        BroadcastPhase();
    }

    private void EndMatch(bool won)
    {
        SetPhase(GamePhase.Result, won);
        BroadcastPhase();
    }

    private void SetPhase(GamePhase phase, bool won)
    {
        CurrentPhase = phase;
        MatchWon = won;
        OnPhaseChanged?.Invoke(phase);
    }

    private void BroadcastPhase()
    {
        if (NetworkManager.Instance == null) return;
        byte[] msg = NetMessages.PackMatchPhase((byte)CurrentPhase, MatchWon, RemainingTime);
        NetworkManager.Instance.SendToAll(msg, reliable: true);
    }

    // クライアント側でのみ呼ばれる。ホストから届いたフェーズをそのまま反映する。
    public void ApplyNetworkPhase(byte phase, bool won, float remainingTime)
    {
        RemainingTime = remainingTime;
        SetPhase((GamePhase)phase, won);
    }
}
