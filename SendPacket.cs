using GameServer;
using Steamworks;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

internal static class SendPacket
{
    #region Host
    internal static void DevolverSaudação(CSteamID alvo)
    {
        Debug.Log("Devolvi a saudação");
        using (Packet p = new Packet(-1))
        {
            p.Write((int)ServerPackets.devolverSaudação);

            SendPacketTo(p, P2P.P2PConns[alvo]);
        }
    }
    #endregion
    #region Client
    internal static void Saudação(CSteamID alvo)
    {
        Debug.Log("Enviei saudação");
        using (Packet p = new Packet((int)ClientPackets.saudação))
        {
            p.Write(SteamUser.GetSteamID());

            SendPacketTo(p, P2P.P2PConns[alvo]);
        }
    }
    internal static void Pingar()
    {
        Debug.Log("Pinguei");
        using (Packet p = new Packet((int)ClientPackets.pingar))
        {
            p.Write(SteamUser.GetSteamID());

            SendPacketToAll(p);
        }
    }
    #endregion
    #region Send
    internal static void SendPacketTo(Packet p, HSteamNetConnection conn)
    {
        byte[] data = p.ToArray();
        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);

        try
        {
            IntPtr ptr = handle.AddrOfPinnedObject();
            SteamNetworkingSockets.SendMessageToConnection(conn, ptr, (uint)data.Length,
                Constants.k_nSteamNetworkingSend_Reliable, out long _);
        }
        finally
        {
            handle.Free();
        }
    }
    internal static void SendPacketToAll(Packet p)
    {
        byte[] data = p.ToArray();
        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);

        try
        {
            IntPtr ptr = handle.AddrOfPinnedObject();
            foreach (var item in P2P.P2PConns)
            {
                SteamNetworkingSockets.SendMessageToConnection(item.Value, ptr, (uint)data.Length,
                    Constants.k_nSteamNetworkingSend_Reliable, out _);
            }
        }
        finally
        {
            handle.Free();
        }
    }
    internal static void SendPacketTo_Fast(Packet p, HSteamNetConnection conn)
    {
        byte[] data = p.ToArray();
        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);

        try
        {
            IntPtr ptr = handle.AddrOfPinnedObject();
            SteamNetworkingSockets.SendMessageToConnection(conn, ptr, (uint)data.Length,
                Constants.k_nSteamNetworkingSend_Unreliable, out _);
        }
        finally
        {
            handle.Free();
        }
    }
    internal static void SendPacketToAll_Fast(Packet p)
    {
        byte[] data = p.ToArray();
        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);

        try
        {
            IntPtr ptr = handle.AddrOfPinnedObject();
            foreach (var item in P2P.P2PConns)
            {
                SteamNetworkingSockets.SendMessageToConnection(item.Value, ptr, (uint)data.Length,
                    Constants.k_nSteamNetworkingSend_Unreliable, out _);
            }
        }
        finally
        {
            handle.Free();
        }
    }
    #endregion
}
