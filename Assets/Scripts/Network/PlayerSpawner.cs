using System.Collections.Generic;
using Steamworks;
using UnityEngine;

// ロビーメンバー全員分のプレイヤーをスポーンする。
// 誰が何番目のスポーン地点に立つかは、全クライアントでSteamID昇順にソートした
// 同じ並び順から決定するため、ホストからの追加のブロードキャストなしで
// 全員が同じ結果にたどり着ける（決定論的スポーン）。
public class PlayerSpawner : MonoBehaviour
{
    public static PlayerSpawner Instance { get; private set; }

    public GameObject playerPrefab;
    public Transform[] spawnPoints;

    private readonly Dictionary<CSteamID, GameObject> spawnedPlayers = new Dictionary<CSteamID, GameObject>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        SpawnAllPlayers();
    }

    public void SpawnAllPlayers()
    {
        if (playerPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[PlayerSpawner] playerPrefab / spawnPoints が設定されていません。");
            return;
        }

        bool networked = NetworkManager.Instance != null && NetworkManager.Instance.LobbyId.IsValid();
        if (!networked)
        {
            // ネットワーク未使用（ソロプレイ・動作確認用）は自分だけをスロット0でスポーンする
            CSteamID localId = SteamManager.Initialized ? SteamUser.GetSteamID() : new CSteamID();
            SpawnPlayer(localId, 0, isLocal: true);
            return;
        }

        List<CSteamID> members = GetDeterministicMemberOrder();
        for (int i = 0; i < members.Count; i++)
        {
            bool isLocal = members[i] == SteamUser.GetSteamID();
            SpawnPlayer(members[i], i, isLocal);
        }
    }

    private List<CSteamID> GetDeterministicMemberOrder()
    {
        CSteamID lobbyId = NetworkManager.Instance.LobbyId;
        int count = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
        var members = new List<CSteamID>(count);
        for (int i = 0; i < count; i++)
        {
            members.Add(SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i));
        }

        members.Sort(); // CSteamIDはIComparableを実装しているため、全クライアントで同じ順序になる
        return members;
    }

    private void SpawnPlayer(CSteamID steamId, int slot, bool isLocal)
    {
        if (spawnedPlayers.ContainsKey(steamId)) return;

        Transform spawnPoint = spawnPoints[slot % spawnPoints.Length];
        GameObject playerObject = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        playerObject.name = isLocal ? "Player_Local" : $"Player_Remote_{steamId.m_SteamID}";

        PlayerController controller = playerObject.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.playerSlot = slot;
            controller.SetIsLocalPlayer(isLocal);
        }

        if (!isLocal)
        {
            // リモートプレイヤーは自分では動かさず、受信した座標で位置だけ更新する
            RemotePlayerRegistry.Register(steamId, playerObject.transform);
        }

        spawnedPlayers[steamId] = playerObject;
    }

    public void DespawnPlayer(CSteamID steamId)
    {
        if (!spawnedPlayers.TryGetValue(steamId, out GameObject playerObject)) return;

        RemotePlayerRegistry.Unregister(steamId);
        if (playerObject != null) Destroy(playerObject);
        spawnedPlayers.Remove(steamId);
    }
}
