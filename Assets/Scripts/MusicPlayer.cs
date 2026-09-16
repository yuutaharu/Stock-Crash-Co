using UnityEngine;

// BGMをループ再生し続けるだけの単純なコンポーネント。
public class MusicPlayer : MonoBehaviour
{
    private const string BgmVolumeKey = "Settings_BgmVolume";

    public AudioClip musicClip;
    public float volume = 0.5f; // このBGM自体のベース音量。設定画面のスライダーはこれに掛け算される。

    // 設定画面のBGM音量スライダー(0〜1)の現在値
    public float UserVolume { get; private set; } = 1f;

    private AudioSource source;

    void Start()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.clip = musicClip;
        source.loop = true;
        source.spatialBlend = 0f;
        source.playOnAwake = false;

        UserVolume = PlayerPrefs.GetFloat(BgmVolumeKey, 1f);
        source.volume = volume * UserVolume;
        if (musicClip != null) source.Play();
    }

    public void SetVolume(float userVolume01)
    {
        UserVolume = Mathf.Clamp01(userVolume01);
        if (source != null) source.volume = volume * UserVolume;
    }
}
