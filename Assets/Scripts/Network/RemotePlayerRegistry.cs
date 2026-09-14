using System.Collections.Generic;
using Steamworks;
using UnityEngine;

// リモートプレイヤーのアバターTransformを保持し、受信した位置情報を反映する。
// アバターの生成（Spawn）自体はロビーメンバーに応じて別途スポーン処理を作る必要がある（未実装）。
public static class RemotePlayerRegistry
{
    private static readonly Dictionary<CSteamID, Transform> remoteAvatars = new Dictionary<CSteamID, Transform>();

    public static void Register(CSteamID id, Transform avatar) => remoteAvatars[id] = avatar;
    public static void Unregister(CSteamID id) => remoteAvatars.Remove(id);

    public static void ApplyTransform(CSteamID sender, Vector3 position, float yaw)
    {
        if (remoteAvatars.TryGetValue(sender, out Transform avatar) && avatar != null)
        {
            avatar.position = position;
            avatar.eulerAngles = new Vector3(0f, yaw, 0f);
        }
    }
}
