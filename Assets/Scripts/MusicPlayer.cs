using UnityEngine;

// BGMをループ再生し続けるだけの単純なコンポーネント。
public class MusicPlayer : MonoBehaviour
{
    public AudioClip musicClip;
    public float volume = 0.5f;

    private AudioSource source;

    void Start()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.clip = musicClip;
        source.loop = true;
        source.volume = volume;
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        if (musicClip != null) source.Play();
    }
}
