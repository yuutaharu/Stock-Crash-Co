using TMPro;
using UnityEngine;

// 生成された数値ポップアップ本体。上に浮かびながらフェードして自動的に消える。
public class FloatingText : MonoBehaviour
{
    public float riseSpeed = 1.5f;
    public float lifetime = 1.2f;

    private TMP_Text text;
    private float elapsed = 0f;
    private Color baseColor;

    public void Setup(string message, Color color)
    {
        if (text == null) text = GetComponentInChildren<TMP_Text>();
        if (text == null) return;

        text.text = message;
        text.color = color;
        baseColor = color;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;

        if (text != null)
        {
            float alpha = Mathf.Clamp01(1f - (elapsed / lifetime));
            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }

        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
