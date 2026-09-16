using UnityEngine;

// プレイ中、一定間隔でプレイヤー側の担当企業(GameSessionManager.selectedCompany)を
// 汚しに歩いて来る簡易AI。放っておくと株価が押し戻されるので、プレイヤーはモップで
// 掃除しに戻る必要が出てくる(「敵」を作るための存在)。
// 判断(いつ・誰を狙うか)はホストだけが行い、実際の汚れ付与はCompany側の
// 既存のホスト権威パターン(RequestAddDirt)にそのまま乗る。
// 攻撃の間隔・量は狙っているCompany側の enemyStrikeIntervalMin/Max・enemyDirtAmount から
// 取得する(店ごとに激しさが違う、という性格付けのため)。
public class EnemySaboteur : MonoBehaviour
{
    public float moveSpeed = 2.2f;
    public float turnSpeed = 6f;
    public float arriveDistance = 1.4f;
    public Vector3 homePosition; // 手が空いている時にうろつく場所

    [Header("効果音")]
    public AudioClip throwSound;

    private Animator animator;
    private string walkClipName;
    private string idleClipName;
    private Company target;
    private float nextStrikeTime;
    private bool hasTarget = false;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;

            if (animator.runtimeAnimatorController != null)
            {
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (walkClipName == null && clip.name.Contains("basicWalk")) walkClipName = clip.name;
                    if (idleClipName == null && clip.name.Contains("idle_")) idleClipName = clip.name;
                }
            }
        }
    }

    void Update()
    {
        bool isHost = NetworkManager.Instance == null || NetworkManager.Instance.IsHost;
        if (!isHost) return; // 狙う相手・タイミングの判断はホストだけが行う

        bool isPlaying = GameSessionManager.Instance != null &&
            GameSessionManager.Instance.CurrentPhase == GameSessionManager.GamePhase.Playing;

        if (!isPlaying)
        {
            hasTarget = false;
            MoveToward(homePosition);
            return;
        }

        if (!hasTarget)
        {
            target = GameSessionManager.Instance.selectedCompany;
            if (target == null) { MoveToward(homePosition); return; }
            hasTarget = true;
            nextStrikeTime = Time.time + Random.Range(target.enemyStrikeIntervalMin, target.enemyStrikeIntervalMax);
        }

        if (target == null || target.isBankrupt)
        {
            MoveToward(homePosition);
            return;
        }

        if (Time.time < nextStrikeTime)
        {
            MoveToward(homePosition);
            return;
        }

        MoveToward(target.transform.position);
        if (Vector3.Distance(transform.position, target.transform.position) <= arriveDistance)
        {
            Strike();
        }
    }

    private void MoveToward(Vector3 destination)
    {
        Vector3 toTarget = destination - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude < 0.3f)
        {
            PlayClip(idleClipName);
            return;
        }

        Vector3 dir = toTarget.normalized;
        transform.forward = Vector3.Slerp(transform.forward, dir, Time.deltaTime * turnSpeed);
        transform.position += dir * moveSpeed * Time.deltaTime;
        PlayClip(walkClipName);
    }

    private void Strike()
    {
        target.RequestAddDirt(target.enemyDirtAmount);
        Sfx.Play(throwSound);
        nextStrikeTime = Time.time + Random.Range(target.enemyStrikeIntervalMin, target.enemyStrikeIntervalMax);
    }

    private void PlayClip(string clipName)
    {
        if (animator == null || string.IsNullOrEmpty(clipName)) return;
        animator.CrossFadeInFixedTime(clipName, 0.3f);
    }
}
