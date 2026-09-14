using System.IO;
using UnityEngine;

public enum NetMessageType : byte
{
    RequestAddDirt = 1,
    RequestCleanDirt = 2,
    RequestAddPopularity = 3,
    RequestBuyStock = 4,
    RequestShortStock = 5,
    RequestCloseShort = 6,
    RequestRocketStrike = 7,
    RequestSellStock = 8,
    StateCompany = 10,
    StateWallet = 11,
    StateMatchPhase = 12,
    StateMatchTimer = 13,
    PlayerTransform = 20,
}

// P2Pで送るバイト列の組み立て/分解だけを担当する。
// フォーマット: 先頭1バイト = NetMessageType、以降はメッセージごとの固定長ペイロード。
public static class NetMessages
{
    public static NetMessageType PeekType(byte[] data) => (NetMessageType)data[0];

    public static byte[] PackCompanyAction(NetMessageType type, int companyId, float amount)
    {
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write((byte)type);
            w.Write(companyId);
            w.Write(amount);
            return ms.ToArray();
        }
    }

    public static void UnpackCompanyAction(byte[] data, out int companyId, out float amount)
    {
        using (var ms = new MemoryStream(data, 1, data.Length - 1))
        using (var r = new BinaryReader(ms))
        {
            companyId = r.ReadInt32();
            amount = r.ReadSingle();
        }
    }

    public static byte[] PackTradeAction(NetMessageType type, int companyId, int shares)
    {
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write((byte)type);
            w.Write(companyId);
            w.Write(shares);
            return ms.ToArray();
        }
    }

    public static void UnpackTradeAction(byte[] data, out int companyId, out int shares)
    {
        using (var ms = new MemoryStream(data, 1, data.Length - 1))
        using (var r = new BinaryReader(ms))
        {
            companyId = r.ReadInt32();
            shares = r.ReadInt32();
        }
    }

    public static byte[] PackCompanyState(
        int companyId, float currentPrice, float dirtiness, float popularity,
        int boughtShares, int shortShares, float shortEntryPrice, bool isBankrupt)
    {
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write((byte)NetMessageType.StateCompany);
            w.Write(companyId);
            w.Write(currentPrice);
            w.Write(dirtiness);
            w.Write(popularity);
            w.Write(boughtShares);
            w.Write(shortShares);
            w.Write(shortEntryPrice);
            w.Write(isBankrupt);
            return ms.ToArray();
        }
    }

    public static void UnpackCompanyState(
        byte[] data, out int companyId, out float currentPrice, out float dirtiness, out float popularity,
        out int boughtShares, out int shortShares, out float shortEntryPrice, out bool isBankrupt)
    {
        using (var ms = new MemoryStream(data, 1, data.Length - 1))
        using (var r = new BinaryReader(ms))
        {
            companyId = r.ReadInt32();
            currentPrice = r.ReadSingle();
            dirtiness = r.ReadSingle();
            popularity = r.ReadSingle();
            boughtShares = r.ReadInt32();
            shortShares = r.ReadInt32();
            shortEntryPrice = r.ReadSingle();
            isBankrupt = r.ReadBoolean();
        }
    }

    public static byte[] PackWalletState(float teamMoney)
    {
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write((byte)NetMessageType.StateWallet);
            w.Write(teamMoney);
            return ms.ToArray();
        }
    }

    public static float UnpackWalletState(byte[] data)
    {
        using (var ms = new MemoryStream(data, 1, data.Length - 1))
        using (var r = new BinaryReader(ms))
        {
            return r.ReadSingle();
        }
    }

    public static byte[] PackMatchPhase(byte phase, bool won, float remainingTime)
    {
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write((byte)NetMessageType.StateMatchPhase);
            w.Write(phase);
            w.Write(won);
            w.Write(remainingTime);
            return ms.ToArray();
        }
    }

    public static void UnpackMatchPhase(byte[] data, out byte phase, out bool won, out float remainingTime)
    {
        using (var ms = new MemoryStream(data, 1, data.Length - 1))
        using (var r = new BinaryReader(ms))
        {
            phase = r.ReadByte();
            won = r.ReadBoolean();
            remainingTime = r.ReadSingle();
        }
    }

    public static byte[] PackMatchTimer(float remainingTime)
    {
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write((byte)NetMessageType.StateMatchTimer);
            w.Write(remainingTime);
            return ms.ToArray();
        }
    }

    public static float UnpackMatchTimer(byte[] data)
    {
        using (var ms = new MemoryStream(data, 1, data.Length - 1))
        using (var r = new BinaryReader(ms))
        {
            return r.ReadSingle();
        }
    }

    public static byte[] PackTransform(int playerSlot, Vector3 position, float yaw)
    {
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write((byte)NetMessageType.PlayerTransform);
            w.Write(playerSlot);
            w.Write(position.x);
            w.Write(position.y);
            w.Write(position.z);
            w.Write(yaw);
            return ms.ToArray();
        }
    }

    public static void UnpackTransform(byte[] data, out int playerSlot, out Vector3 position, out float yaw)
    {
        using (var ms = new MemoryStream(data, 1, data.Length - 1))
        using (var r = new BinaryReader(ms))
        {
            playerSlot = r.ReadInt32();
            float x = r.ReadSingle();
            float y = r.ReadSingle();
            float z = r.ReadSingle();
            position = new Vector3(x, y, z);
            yaw = r.ReadSingle();
        }
    }
}
