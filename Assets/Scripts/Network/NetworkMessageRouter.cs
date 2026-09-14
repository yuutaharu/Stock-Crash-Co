using Steamworks;
using UnityEngine;

// 受信したバイト列を種別ごとに正しいオブジェクトへ振り分ける。
// Request系はホストだけが処理し、State系はホスト以外だけが反映する（ホスト権威モデル）。
public static class NetworkMessageRouter
{
    public static void Dispatch(CSteamID sender, byte[] data)
    {
        if (data == null || data.Length == 0) return;

        switch (NetMessages.PeekType(data))
        {
            case NetMessageType.RequestAddDirt:
            case NetMessageType.RequestCleanDirt:
            case NetMessageType.RequestAddPopularity:
            case NetMessageType.RequestRocketStrike:
                HandleCompanyActionRequest(data);
                break;

            case NetMessageType.RequestBuyStock:
            case NetMessageType.RequestShortStock:
            case NetMessageType.RequestCloseShort:
            case NetMessageType.RequestSellStock:
                HandleTradeRequest(data);
                break;

            case NetMessageType.StateCompany:
                HandleCompanyState(data);
                break;

            case NetMessageType.StateWallet:
                HandleWalletState(data);
                break;

            case NetMessageType.StateMatchPhase:
                HandleMatchPhase(data);
                break;

            case NetMessageType.StateMatchTimer:
                HandleMatchTimer(data);
                break;

            case NetMessageType.PlayerTransform:
                HandlePlayerTransform(sender, data);
                break;
        }
    }

    private static void HandleCompanyActionRequest(byte[] data)
    {
        if (NetworkManager.Instance != null && !NetworkManager.Instance.IsHost) return;

        NetMessageType type = NetMessages.PeekType(data);
        NetMessages.UnpackCompanyAction(data, out int companyId, out float amount);
        if (!Company.Registry.TryGetValue(companyId, out Company company)) return;

        switch (type)
        {
            case NetMessageType.RequestAddDirt: company.AddDirt(amount); break;
            case NetMessageType.RequestCleanDirt: company.CleanDirt(amount); break;
            case NetMessageType.RequestAddPopularity: company.AddPopularity(amount); break;
            case NetMessageType.RequestRocketStrike: company.TriggerBankruptcy(); break;
        }
    }

    private static void HandleTradeRequest(byte[] data)
    {
        if (NetworkManager.Instance != null && !NetworkManager.Instance.IsHost) return;
        if (MarketManager.Instance == null) return;

        NetMessageType type = NetMessages.PeekType(data);
        NetMessages.UnpackTradeAction(data, out int companyId, out int shares);
        if (!Company.Registry.TryGetValue(companyId, out Company company)) return;

        switch (type)
        {
            case NetMessageType.RequestBuyStock: MarketManager.Instance.BuyStock(company, shares); break;
            case NetMessageType.RequestShortStock: MarketManager.Instance.ShortStock(company, shares); break;
            case NetMessageType.RequestCloseShort: MarketManager.Instance.CloseShortPosition(company); break;
            case NetMessageType.RequestSellStock: MarketManager.Instance.SellStock(company, shares); break;
        }
    }

    private static void HandleCompanyState(byte[] data)
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost) return;

        NetMessages.UnpackCompanyState(data, out int companyId, out float price, out float dirt, out float pop,
            out int boughtShares, out int shortShares, out float shortEntryPrice, out bool isBankrupt);
        if (Company.Registry.TryGetValue(companyId, out Company company))
        {
            company.ApplyNetworkState(price, dirt, pop, boughtShares, shortShares, shortEntryPrice, isBankrupt);
        }
    }

    private static void HandleWalletState(byte[] data)
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost) return;
        if (MarketManager.Instance == null) return;

        float teamMoney = NetMessages.UnpackWalletState(data);
        MarketManager.Instance.ApplyNetworkWallet(teamMoney);
    }

    private static void HandleMatchPhase(byte[] data)
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost) return;
        if (GameSessionManager.Instance == null) return;

        NetMessages.UnpackMatchPhase(data, out byte phase, out bool won, out float remainingTime);
        GameSessionManager.Instance.ApplyNetworkPhase(phase, won, remainingTime);
    }

    private static void HandleMatchTimer(byte[] data)
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost) return;
        if (GameSessionManager.Instance == null) return;

        float remainingTime = NetMessages.UnpackMatchTimer(data);
        GameSessionManager.Instance.ApplyNetworkTimer(remainingTime);
    }

    private static void HandlePlayerTransform(CSteamID sender, byte[] data)
    {
        NetMessages.UnpackTransform(data, out int slot, out Vector3 position, out float yaw);
        RemotePlayerRegistry.ApplyTransform(sender, position, yaw);
    }
}
