using TMPro;
using UnityEngine;

// プレイ中だけ残り時間をMM:SS形式で表示するだけのシンプルなHUD。
// 自分自身ではなくpanelの表示/非表示を切り替える
// （このスクリプト自身のGameObjectを無効化すると、次のUpdateが呼ばれず再表示できなくなるため）。
public class MatchTimerHUD : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text timerText;

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

        if (GameSessionManager.Instance == null) return;
        if (GameSessionManager.Instance.CurrentPhase != GameSessionManager.GamePhase.Playing) return;

        if (timerText != null)
        {
            float remaining = Mathf.Max(0f, GameSessionManager.Instance.RemainingTime);
            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
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
        if (panel != null) panel.SetActive(phase == GameSessionManager.GamePhase.Playing);
    }
}
