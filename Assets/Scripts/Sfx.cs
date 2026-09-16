using UnityEngine;

// 効果音を2D(非空間)で単発再生するための小さなヘルパー。
// 呼び出し側は個別にAudioSourceを用意しなくてよい。
public static class Sfx
{
    private const string VolumeKey = "Settings_SfxVolume";

    private static AudioSource s_source;
    private static float s_volume = -1f; // 負の値=PlayerPrefsからまだ読み込んでいない印

    // 設定画面のSFX音量スライダー(0〜1)。未読み込み時は初回アクセスでPlayerPrefsから復元する。
    public static float Volume
    {
        get
        {
            if (s_volume < 0f) s_volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            return s_volume;
        }
        set => s_volume = Mathf.Clamp01(value);
    }

    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        if (s_source == null)
        {
            GameObject go = new GameObject("SfxPlayer");
            Object.DontDestroyOnLoad(go);
            s_source = go.AddComponent<AudioSource>();
            s_source.playOnAwake = false;
            s_source.spatialBlend = 0f; // UI/2Dサウンドとして扱う
        }
        s_source.PlayOneShot(clip, volume * Volume);
    }
}
