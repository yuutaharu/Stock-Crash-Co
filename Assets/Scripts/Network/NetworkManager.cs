using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using UnityEngine;

// SteamNetworkingMessages (ISteamNetworkingMessages) を使ったP2P送受信のラッパー。
// 注意: Steamworks.NETのバージョンによって型名/メソッド名が異なる場合があるため、
// 実際にパッケージを導入した後にコンパイルエラーが出た箇所は該当バージョンのAPIに合わせて調整すること。
public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    public const int Channel = 0;
    private const int MaxMessagesPerPoll = 32;

    public CSteamID LobbyId { get; private set; }

    public bool IsHost =>
        SteamManager.Initialized && LobbyId.IsValid() &&
        SteamMatchmaking.GetLobbyOwner(LobbyId) == SteamUser.GetSteamID();

    public CSteamID HostId => SteamMatchmaking.GetLobbyOwner(LobbyId);

    // ホストが(離脱等で)入れ替わった時に発火する。引数は「自分が新しくホストになったか」。
    public event Action<bool> OnHostChanged;

    private readonly List<CSteamID> members = new List<CSteamID>();
    private readonly IntPtr[] messageBuffer = new IntPtr[MaxMessagesPerPoll];
    private CSteamID lastKnownHost;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (!SteamManager.Initialized || !LobbyId.IsValid()) return;
        PollMessages();
        CheckHostMigration();
    }

    private void CheckHostMigration()
    {
        CSteamID currentHost = HostId;
        if (currentHost == lastKnownHost) return;

        bool isFirstCheck = !lastKnownHost.IsValid();
        lastKnownHost = currentHost;
        if (isFirstCheck) return; // 初回はロビー参加直後の初期値確定なので通知しない

        bool isNowHost = currentHost == SteamUser.GetSteamID();
        Debug.Log($"[NetworkManager] ホストが交代しました。自分がホスト: {isNowHost}");
        OnHostChanged?.Invoke(isNowHost);
    }

    public void SetLobby(CSteamID lobbyId)
    {
        LobbyId = lobbyId;
        RefreshMembers();
    }

    public void RefreshMembers()
    {
        members.Clear();
        int count = SteamMatchmaking.GetNumLobbyMembers(LobbyId);
        for (int i = 0; i < count; i++)
        {
            members.Add(SteamMatchmaking.GetLobbyMemberByIndex(LobbyId, i));
        }
    }

    // 退室したユーザーとのP2Pセッションを明示的に閉じ、リソースを解放する
    public void CloseSessionWith(CSteamID target)
    {
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID(target);
        SteamNetworkingMessages.CloseSessionWithUser(ref identity);
    }

    public void SendToHost(byte[] data, bool reliable)
    {
        if (IsHost)
        {
            NetworkMessageRouter.Dispatch(SteamUser.GetSteamID(), data);
            return;
        }
        SendTo(HostId, data, reliable);
    }

    public void SendToAll(byte[] data, bool reliable, bool includeSelf = false)
    {
        foreach (var member in members)
        {
            if (!includeSelf && member == SteamUser.GetSteamID()) continue;
            SendTo(member, data, reliable);
        }
    }

    public void SendTo(CSteamID target, byte[] data, bool reliable)
    {
        if (target == SteamUser.GetSteamID())
        {
            NetworkMessageRouter.Dispatch(target, data);
            return;
        }

        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID(target);

        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            int sendFlags = reliable
                ? Constants.k_nSteamNetworkingSend_Reliable
                : Constants.k_nSteamNetworkingSend_Unreliable;

            SteamNetworkingMessages.SendMessageToUser(
                ref identity, handle.AddrOfPinnedObject(), (uint)data.Length, sendFlags, Channel);
        }
        finally
        {
            handle.Free();
        }
    }

    private void PollMessages()
    {
        int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, messageBuffer, MaxMessagesPerPoll);
        for (int i = 0; i < count; i++)
        {
            var msg = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messageBuffer[i]);

            byte[] data = new byte[msg.m_cbSize];
            Marshal.Copy(msg.m_pData, data, 0, msg.m_cbSize);
            CSteamID senderId = msg.m_identityPeer.GetSteamID();

            SteamNetworkingMessage_t.Release(messageBuffer[i]);

            NetworkMessageRouter.Dispatch(senderId, data);
        }
    }
}
