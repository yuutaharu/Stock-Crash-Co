using UnityEngine;
using UnityEngine.UI;

// SFX/BGM音量とマウス感度を調整するオプション画面。値はPlayerPrefsに保存され、次回起動時にも引き継がれる。
// 注意: このスクリプト自身が付いたオブジェクトは常時アクティブにしておき、表示/非表示は子のpanelRootだけ
// 切り替える(MatchTimerHUD/RocketChargeGaugeHUDと同じ方式)。スクリプト自身を無効化すると
// Awake/OnEnableが二度と呼ばれなくなり、開くボタンのイベント登録ごと消えてしまうため。
public class SettingsUIController : MonoBehaviour
{
    [Header("参照")]
    public MusicPlayer musicPlayer;
    public FirstPersonLook firstPersonLook;

    [Header("UI")]
    public GameObject panelRoot;
    public Button openButton;
    public Button closeButton;
    public Slider sfxVolumeSlider;
    public Slider bgmVolumeSlider;
    public Slider mouseSensitivitySlider;

    [Header("効果音")]
    public AudioClip openSound;

    private const string SfxVolumeKey = "Settings_SfxVolume";
    private const string BgmVolumeKey = "Settings_BgmVolume";
    private const string MouseSensitivityKey = "Settings_MouseSensitivity";

    public const float MinSensitivity = 0.5f;
    public const float MaxSensitivity = 6f;

    // FirstPersonLook側がカーソルのロック可否を判断するために参照する。
    // (設定パネルが開いている間はプレイ中でもカーソルをロックしない)
    public static bool IsOpen { get; private set; } = false;

    void Awake()
    {
        if (openButton != null)
        {
            openButton.onClick.AddListener(OpenPanel);
        }
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
        IsOpen = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (panelRoot != null && panelRoot.activeSelf) ClosePanel();
            else OpenPanel();
        }
    }

    // タイトル画面など、他のUIの「設定」ボタンからも呼べるように公開しておく。
    public void OpenPanel()
    {
        if (panelRoot == null || panelRoot.activeSelf) return;

        Sfx.Play(openSound);
        panelRoot.SetActive(true);
        RefreshSliders();
        IsOpen = true;
    }

    private void ClosePanel()
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;

        panelRoot.SetActive(false);
        IsOpen = false;
    }

    private void RefreshSliders()
    {
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
            sfxVolumeSlider.value = Sfx.Volume;
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.minValue = 0f;
            bgmVolumeSlider.maxValue = 1f;
            bgmVolumeSlider.value = musicPlayer != null ? musicPlayer.UserVolume : 1f;
            bgmVolumeSlider.onValueChanged.RemoveAllListeners();
            bgmVolumeSlider.onValueChanged.AddListener(SetBgmVolume);
        }

        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.minValue = MinSensitivity;
            mouseSensitivitySlider.maxValue = MaxSensitivity;
            mouseSensitivitySlider.value = firstPersonLook != null ? firstPersonLook.mouseSensitivity : MinSensitivity;
            mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
            mouseSensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);
        }
    }

    private void SetSfxVolume(float value)
    {
        Sfx.Volume = value;
        PlayerPrefs.SetFloat(SfxVolumeKey, value);
    }

    private void SetBgmVolume(float value)
    {
        if (musicPlayer != null) musicPlayer.SetVolume(value);
        PlayerPrefs.SetFloat(BgmVolumeKey, value);
    }

    private void SetMouseSensitivity(float value)
    {
        if (firstPersonLook != null) firstPersonLook.mouseSensitivity = value;
        PlayerPrefs.SetFloat(MouseSensitivityKey, value);
    }
}
