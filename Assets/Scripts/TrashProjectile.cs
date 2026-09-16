using UnityEngine;

// 投げたゴミの弾。物理で飛び、店舗の実体コライダーに当たると、当たった場所にそのまま汚れ(デカール)を
// 残して缶自体は消える(缶のオブジェクトが壁に張り付いたままにはしない)。何にも当たらなければ
// 一定時間で消える。
[RequireComponent(typeof(Rigidbody))]
public class TrashProjectile : MonoBehaviour
{
    public float dirtAmount = 5f;
    public float lifeSeconds = 8f;

    [Header("汚れデカール")]
    public Color decalColor = new Color(0.32f, 0.24f, 0.12f);
    public float decalMinSize = 0.28f;
    public float decalMaxSize = 0.4f;

    [Header("効果音")]
    public AudioClip splatSound;

    private bool hit = false;

    void Start()
    {
        Invoke(nameof(SelfDestruct), lifeSeconds);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hit) return;

        Company company = collision.collider.GetComponentInParent<Company>();
        if (company == null) return;

        hit = true;

        company.RequestAddDirt(dirtAmount);
        Sfx.Play(splatSound);

        ContactPoint contact = collision.GetContact(0);
        SpawnDecal(company, contact.point, contact.normal);

        Destroy(gameObject);
    }

    // 当たった場所にそのまま張り付く汚れの見た目。円盤を壁面に埋め込むように配置する
    // (外部アセット不要、プリミティブだけで表現)。モップで掃除する時はこのデカールを探して消す。
    private void SpawnDecal(Company company, Vector3 point, Vector3 normal)
    {
        GameObject decal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        decal.name = "DirtDecal";
        Destroy(decal.GetComponent<Collider>());

        decal.transform.SetParent(company.transform, true);
        decal.transform.position = point + normal * 0.03f;
        // 円盤の上面(ローカルY軸)を衝突面の法線に向け、さらに法線を軸にランダムへ回して単調さを消す。
        decal.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        float size = Random.Range(decalMinSize, decalMaxSize);
        decal.transform.localScale = new Vector3(size, 0.01f, size);

        Renderer renderer = decal.GetComponent<Renderer>();
        renderer.material.color = decalColor;

        TrashMarker marker = decal.AddComponent<TrashMarker>();
        marker.company = company;
    }

    private void SelfDestruct()
    {
        Destroy(gameObject);
    }
}
