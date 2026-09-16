using System.Collections.Generic;
using UnityEngine;

// マップに置かれた「拾えるアイテム」。プレイヤーが触れると持ち物として入手し、この物自体は
// (ゴミもモップも)しばらく非表示になった後、同じ場所に再出現する。
public class PickupItem : MonoBehaviour
{
    public enum ItemType { Trash, Mop }

    public ItemType itemType;
    public float respawnSeconds = 8f; // 0以下なら再出現しない
    public AudioClip pickupSound;

    private static readonly List<PickupItem> All = new List<PickupItem>();

    private Collider[] colliders;
    private Renderer[] renderers;

    void Awake()
    {
        colliders = GetComponentsInChildren<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    private void OnTriggerEnter(Collider other)
    {
        if (!IsAvailable()) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (player.TryPickup(itemType))
        {
            Sfx.Play(pickupSound);
            SetAvailable(false);
            if (respawnSeconds > 0f) Invoke(nameof(Respawn), respawnSeconds);
        }
    }

    private void Respawn() => SetAvailable(true);

    private bool IsAvailable() => renderers.Length == 0 || renderers[0].enabled;

    private void SetAvailable(bool available)
    {
        foreach (Collider c in colliders) c.enabled = available;
        foreach (Renderer r in renderers) r.enabled = available;
    }

    // 周回リプレイ(GameSessionManager.ResetForNewMatch)時に、拾われた状態のアイテムを全て出し直す。
    public static void ResetAll()
    {
        foreach (PickupItem item in All)
        {
            if (item == null) continue;
            item.CancelInvoke(nameof(Respawn));
            item.SetAvailable(true);
        }
    }
}
