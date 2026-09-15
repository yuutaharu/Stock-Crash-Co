using UnityEngine;
using UnityEngine.UI;

// プレイヤーがロケット砲の前でEキーを溜めている間だけ、画面にゲージを表示するHUD。
// シーン内に複数のRocketLauncherがあっても、現在チャージ中の1つだけを表示する。
public class RocketChargeGaugeHUD : MonoBehaviour
{
    public GameObject panel;
    public Image fillImage;

    private RocketLauncher[] launchers;

    void Start()
    {
        // MVP規模(発射台2基程度)なので、毎フレーム探すのではなく起動時に1回だけ集めておく。
        launchers = Object.FindObjectsByType<RocketLauncher>(FindObjectsSortMode.None);
    }

    void Update()
    {
        RocketLauncher charging = FindChargingLauncher();

        if (panel != null) panel.SetActive(charging != null);
        if (charging != null && fillImage != null)
        {
            fillImage.fillAmount = charging.ChargeProgress01;
        }
    }

    private RocketLauncher FindChargingLauncher()
    {
        if (launchers == null) return null;

        foreach (RocketLauncher launcher in launchers)
        {
            if (launcher != null && launcher.IsCharging) return launcher;
        }
        return null;
    }
}
