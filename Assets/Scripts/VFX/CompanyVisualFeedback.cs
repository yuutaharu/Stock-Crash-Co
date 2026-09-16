using UnityEngine;

// dirtiness/popularity/破産状態の「数値の変化」を「見た目」に変換するだけの演出レイヤー。
// Company.cs側のゲームロジックには一切書き込まない（疎結合にするため、Update()で値を監視する方式）。
// ネットワーク経由でも(ApplyNetworkStateで値が変わった時も)同じUpdate()が検知するので、
// ホスト/クライアントどちらでも同じように演出が出る。
// 注意: クライアント側では状態同期の間隔(Company.stateBroadcastInterval)でしか値が届かないため、
// 短時間に連続で工作された場合、演出が1回にまとまって見えることがある。
[RequireComponent(typeof(Company))]
public class CompanyVisualFeedback : MonoBehaviour
{
    [Header("参照")]
    public Company company;

    [Header("汚れ演出（dirtinessが閾値を超えるごとに1つずつ有効化される）")]
    public GameObject[] dirtStageProps;
    public float[] dirtStageThresholds = { 10f, 30f, 60f, 100f };

    [Header("人気演出（popularityが閾値を超えるごとに1つずつ有効化される）")]
    public GameObject[] popularityStageProps;
    public float[] popularityStageThresholds = { 10f, 30f, 60f, 100f };

    [Header("ワンショット演出")]
    public ParticleSystem dirtBurstEffect;
    public ParticleSystem cleanBurstEffect;
    public ParticleSystem popularityBurstEffect;
    public GameObject bankruptEffect;

    [Header("数値ポップアップ")]
    public FloatingTextSpawner floatingTextSpawner;
    public Transform popupOrigin;

    [Header("効果音")]
    public AudioClip dirtSound;
    public AudioClip cleanSound;
    public AudioClip popularitySound;
    public AudioClip bankruptSound;

    private float lastDirtiness;
    private float lastPopularity;
    private bool lastBankrupt;
    private bool initialized = false;

    void Reset()
    {
        company = GetComponent<Company>();
    }

    void Start()
    {
        if (company == null) company = GetComponent<Company>();
        lastDirtiness = company.dirtiness;
        lastPopularity = company.popularity;
        lastBankrupt = company.isBankrupt;
        initialized = true;

        RefreshDirtStages();
        RefreshPopularityStages();
        if (bankruptEffect != null) bankruptEffect.SetActive(company.isBankrupt);
    }

    void Update()
    {
        if (company == null || !initialized) return;

        if (!Mathf.Approximately(company.dirtiness, lastDirtiness))
        {
            HandleDirtinessChanged(company.dirtiness - lastDirtiness);
            lastDirtiness = company.dirtiness;
        }

        if (!Mathf.Approximately(company.popularity, lastPopularity))
        {
            HandlePopularityChanged(company.popularity - lastPopularity);
            lastPopularity = company.popularity;
        }

        if (company.isBankrupt != lastBankrupt)
        {
            lastBankrupt = company.isBankrupt;
            if (lastBankrupt) HandleBankrupt();
        }
    }

    private void HandleDirtinessChanged(float delta)
    {
        RefreshDirtStages();

        if (delta > 0f)
        {
            PlayBurst(dirtBurstEffect);
            SpawnPopup($"+{delta:F0} 汚れ", Color.red);
            Sfx.Play(dirtSound);
        }
        else if (delta < 0f)
        {
            PlayBurst(cleanBurstEffect);
            SpawnPopup($"{delta:F0} 汚れ", Color.green);
            Sfx.Play(cleanSound);
        }
    }

    private void HandlePopularityChanged(float delta)
    {
        RefreshPopularityStages();

        if (delta > 0f)
        {
            PlayBurst(popularityBurstEffect);
            SpawnPopup($"+{delta:F0} 人気", Color.yellow);
            Sfx.Play(popularitySound);
        }
    }

    private void HandleBankrupt()
    {
        if (bankruptEffect != null) bankruptEffect.SetActive(true);
        SpawnPopup("破産！", Color.red);
        Sfx.Play(bankruptSound);
    }

    private void RefreshDirtStages() => RefreshStageProps(dirtStageProps, dirtStageThresholds, company.dirtiness);
    private void RefreshPopularityStages() => RefreshStageProps(popularityStageProps, popularityStageThresholds, company.popularity);

    private void RefreshStageProps(GameObject[] props, float[] thresholds, float value)
    {
        if (props == null) return;
        for (int i = 0; i < props.Length; i++)
        {
            if (props[i] == null) continue;
            float threshold = i < thresholds.Length ? thresholds[i] : float.MaxValue;
            props[i].SetActive(value >= threshold);
        }
    }

    private void PlayBurst(ParticleSystem effect)
    {
        if (effect != null) effect.Play();
    }

    private void SpawnPopup(string message, Color color)
    {
        if (floatingTextSpawner == null) return;
        Vector3 origin = popupOrigin != null ? popupOrigin.position : transform.position;
        floatingTextSpawner.Spawn(origin, message, color);
    }
}
