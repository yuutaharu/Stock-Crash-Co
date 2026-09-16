using UnityEngine;

// 投げたゴミの弾。物理で飛び、店舗の実体コライダーに当たると汚れを追加して
// その場(当たった座標)に留まる。何にも当たらなければ一定時間で消える。
[RequireComponent(typeof(Rigidbody))]
public class TrashProjectile : MonoBehaviour
{
    public float dirtAmount = 5f;
    public float lifeSeconds = 8f;

    [Header("効果音")]
    public AudioClip splatSound;

    private bool stuck = false;

    void Start()
    {
        Invoke(nameof(SelfDestruct), lifeSeconds);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (stuck) return;

        Company company = collision.collider.GetComponentInParent<Company>();
        if (company == null) return;

        stuck = true;
        company.RequestAddDirt(dirtAmount);
        Sfx.Play(splatSound);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        ContactPoint contact = collision.GetContact(0);
        transform.position = contact.point + contact.normal * 0.05f;
        transform.SetParent(company.transform, true);

        TrashMarker marker = gameObject.AddComponent<TrashMarker>();
        marker.company = company;

        CancelInvoke(nameof(SelfDestruct)); // 張り付いたゴミは自動では消さない
    }

    private void SelfDestruct()
    {
        Destroy(gameObject);
    }
}
