using UnityEngine;

// 街を歩き回るだけの簡易NPC。NavMeshは使わず、指定した矩形の範囲内でランダムな目的地へ直進する。
// City PeopleアセットのAnimator(Walk/Idle系クリップを含む)をそのまま利用する。
public class NPCWanderer : MonoBehaviour
{
    public float moveSpeed = 1.2f;
    public float turnSpeed = 5f;
    public float arriveDistance = 0.3f;
    public float waitSecondsMin = 1f;
    public float waitSecondsMax = 4f;
    public Vector2 areaMinXZ = new Vector2(-5f, -5f);
    public Vector2 areaMaxXZ = new Vector2(5f, 5f);

    private Animator animator;
    private string walkClipName;
    private string idleClipName;
    private Vector3 destination;
    private float waitTimer;
    private bool waiting;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false; // 移動はこのスクリプトのTransform操作だけで行う

            if (animator.runtimeAnimatorController != null)
            {
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (walkClipName == null && clip.name.Contains("basicWalk")) walkClipName = clip.name;
                    if (idleClipName == null && clip.name.Contains("idle_")) idleClipName = clip.name;
                }
            }
        }

        PickNewDestination();
        PlayClip(walkClipName);
    }

    void Update()
    {
        if (waiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                waiting = false;
                PickNewDestination();
                PlayClip(walkClipName);
            }
            return;
        }

        Vector3 toTarget = destination - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude < arriveDistance)
        {
            waiting = true;
            waitTimer = Random.Range(waitSecondsMin, waitSecondsMax);
            PlayClip(idleClipName);
            return;
        }

        Vector3 dir = toTarget.normalized;
        transform.forward = Vector3.Slerp(transform.forward, dir, Time.deltaTime * turnSpeed);
        transform.position += dir * moveSpeed * Time.deltaTime;
    }

    private void PickNewDestination()
    {
        destination = new Vector3(
            Random.Range(areaMinXZ.x, areaMaxXZ.x),
            transform.position.y,
            Random.Range(areaMinXZ.y, areaMaxXZ.y));
    }

    private void PlayClip(string clipName)
    {
        if (animator == null || string.IsNullOrEmpty(clipName)) return;
        animator.CrossFadeInFixedTime(clipName, 0.3f);
    }
}
