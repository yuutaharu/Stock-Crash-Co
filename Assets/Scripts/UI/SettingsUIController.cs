using UnityEngine;
using UnityEngine.UI;

// SFX/BGM音量とマウス感度を調整するオプション画面。値はPlayerPrefsに保存され、次回起動時にも引き継がれる。
public class SettingsUIController : MonoBehaviour
{
    [Header("参照")]
    public MusicPlayer musicPlayer;
    public FirstPersonLook firstPersonLook;

    [Header("UI")]
    public Slider sfxVolumeSlider;
    public Slider bgmVolumeSlider;
    public Slider mouseSensitivitySlider;
    public Button closeButton;

    private const string SfxVolumeKey = "Settings_SfxVolume";
    private const string BgmVolumeKey = "Settings_BgmVolume";
    private const string MouseSensitivityKey = "Settings_MouseSensitivity";

    public const float MinSensitivity = 0.5f;
    public const float MaxSensitivity = 6f;

    void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }
    }

    void OnEnable()
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
