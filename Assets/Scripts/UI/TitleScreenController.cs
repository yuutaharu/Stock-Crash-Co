using UnityEngine;
using UnityEngine.UI;

// 起動直後に表示するタイトル画面。「プレイ開始」を押すとブリーフィング画面(既に裏でアクティブ)が見える状態になる。
// マルチプレイのロビー機能はまだシーンに配線されていないため、現状はソロプレイへの入口のみ。
public class TitleScreenController : MonoBehaviour
{
    [Header("UI")]
    public GameObject panelRoot;
    public Button startButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("参照")]
    public SettingsUIController settingsUI;

    [Header("効果音")]
    public AudioClip clickSound;

    // FirstPersonLook側がカーソルのロック可否を判断するために参照する。
    // (タイトル画面が出ている間はカーソルをロックしない)
    public static bool IsShowing { get; private set; } = true;

    void Awake()
    {
        IsShowing = panelRoot != null && panelRoot.activeSelf;

        if (startButton != null)
        {
            startButton.onClick.AddListener(HandleStart);
        }
        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(HandleOpenSettings);
        }
        if (quitButton != null)
        {
            quitButton.onClick.AddListener(HandleQuit);
        }
    }

    private void HandleStart()
    {
        Sfx.Play(clickSound);
        if (panelRoot != null) panelRoot.SetActive(false);
        IsShowing = false;
    }

    private void HandleOpenSettings()
    {
        Sfx.Play(clickSound);
        if (settingsUI != null) settingsUI.OpenPanel();
    }

    private void HandleQuit()
    {
        Sfx.Play(clickSound);
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
