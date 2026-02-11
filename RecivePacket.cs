using GameServer;
using Steamworks;
using UnityEngine;

internal static class RecivePacket
{
    #region Recebidos do host
    internal static void ReceberSaudação_DoHost(Packet p)
    {
        Debug.Log("O host me pediu para que eu conectasse a todos");
        
    }
    #endregion
    #region Recebido de clientes
    internal static void ReceberSaudação(Packet p)
    {
        Debug.Log("Recebi saudação!");
        CSteamID id = p.ReadCSteamID();

    }
    internal static void Pingar(Packet p)
    {
        Debug.Log("pingado");
    }
    #endregion
}
