using UnityEngine;
using Steamworks;

public class SteamLobbyManager : MonoBehaviour
{
    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;
    protected Callback<LobbyChatUpdate_t> lobbyChatUpdate;

    private const string HostAddressKey = "HostAddress";

    private void Start()
    {
        if (!SteamManager.Initialized) return;

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
    }

    // ロビー作成（ホスト）
    public void CreateLobby()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK) return;

        CSteamID lobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(lobbyID, HostAddressKey, SteamUser.GetSteamID().ToString());
        Debug.Log("ロビーが正常に作成されました。");
    }

    // Steamフレンドからの参加リクエスト
    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        CSteamID lobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        NetworkManager.Instance.SetLobby(lobbyID);
        Debug.Log("ロビーに入室しました。");
        // ここでゲームシーンへの遷移処理を行う
    }

    // メンバーの入退室があったら宛先リストを更新し、
    // 退室(離脱/切断/キック/BAN)の場合はそのプレイヤーのアバターを消す
    private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
    {
        CSteamID lobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        if (NetworkManager.Instance.LobbyId != lobbyID) return;

        NetworkManager.Instance.RefreshMembers();

        const uint leftFlags =
            (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft |
            (uint)EChatMemberStateChange.k_EChatMemberStateChangeDisconnected |
            (uint)EChatMemberStateChange.k_EChatMemberStateChangeKicked |
            (uint)EChatMemberStateChange.k_EChatMemberStateChangeBanned;

        if ((callback.m_rgfChatMemberStateChange & leftFlags) == 0) return;

        CSteamID leftUser = new CSteamID(callback.m_ulSteamIDUserChanged);
        Debug.Log($"[SteamLobbyManager] プレイヤーが退室しました: {leftUser}");

        if (PlayerSpawner.Instance != null)
        {
            PlayerSpawner.Instance.DespawnPlayer(leftUser);
        }
        NetworkManager.Instance.CloseSessionWith(leftUser);
    }

    // 「ゲームをやめる」ボタン等から呼ぶ想定の明示的な退室処理
    public void LeaveLobby()
    {
        if (!NetworkManager.Instance.LobbyId.IsValid()) return;
        SteamMatchmaking.LeaveLobby(NetworkManager.Instance.LobbyId);
    }
}
