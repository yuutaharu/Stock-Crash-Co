using UnityEngine;

// GameSessionManagerのフェーズ変化に応じて、対応するUIパネルの表示/非表示を切り替えるだけの調整役。
// プレイ中はどちらのパネルも非表示になる想定（トレーディングUIはTradingPC側で別途開閉される）。
public class GamePhaseUIRouter : MonoBehaviour
{
    public GameObject briefingPanel;
    public GameObject resultPanel;

    private bool hooked = false;

    void Update()
    {
        // GameSessionManagerの生成タイミング次第でOnEnable時点ではまだ存在しないため、ここで遅延購読する
        if (!hooked && GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.OnPhaseChanged += HandlePhaseChanged;
            HandlePhaseChanged(GameSessionManager.Instance.CurrentPhase);
            hooked = true;
        }
    }

    void OnDestroy()
    {
        if (hooked && GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        }
    }

    private void HandlePhaseChanged(GameSessionManager.GamePhase phase)
    {
        if (briefingPanel != null) briefingPanel.SetActive(phase == GameSessionManager.GamePhase.Briefing);
        if (resultPanel != null) resultPanel.SetActive(phase == GameSessionManager.GamePhase.Result);
    }
}
