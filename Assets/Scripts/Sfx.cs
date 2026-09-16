using UnityEngine;

// 効果音を2D(非空間)で単発再生するための小さなヘルパー。
// 呼び出し側は個別にAudioSourceを用意しなくてよい。
public static class Sfx
{
    private static AudioSource s_source;

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
        s_source.PlayOneShot(clip, volume);
    }
}
