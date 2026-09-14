using UnityEngine;

// 数値ポップアップ(FloatingText)をワールド上に生成するだけのユーティリティ。
public class FloatingTextSpawner : MonoBehaviour
{
    public FloatingText floatingTextPrefab;
    public Vector3 spawnOffset = new Vector3(0f, 2f, 0f);

    public void Spawn(Vector3 worldPosition, string message, Color color)
    {
        if (floatingTextPrefab == null) return;

        FloatingText instance = Instantiate(floatingTextPrefab, worldPosition + spawnOffset, Quaternion.identity);
        instance.Setup(message, color);
    }
}
