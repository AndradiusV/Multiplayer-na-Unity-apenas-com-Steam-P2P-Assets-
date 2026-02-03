using GameServer;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;

public class P2P : MonoBehaviour
{
    internal static bool Conectado;
    internal const int MaxMessagges = 32;
    internal static P2P instance;
    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DefineFuncs();
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            SendPacket.Pingar();
        }
        try
        {
            foreach (var conn in P2PConns)
            {
                if (conn.Value.m_HSteamNetConnection != 0)
                {
                    IntPtr[] msgs = new IntPtr[MaxMessagges];
                    int msgCount;
                    do
                    {
                        msgCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(conn.Value, msgs, MaxMessagges);
                        if (msgs != null)
                        {
                            for (int i = 0; i < msgCount; i++)
                            {
                                #region Estrutura para pegar a data como bytes
                                SteamNetworkingMessage_t netMsg = SteamNetworkingMessage_t.FromIntPtr(msgs[i]);

                                byte[] buffer = new byte[netMsg.m_cbSize];
                                Marshal.Copy(netMsg.m_pData, buffer, 0, netMsg.m_cbSize);
                                #endregion

                                ProcessarMensagem(buffer);
                                SteamNetworkingMessage_t.Release(msgs[i]);
                            }
                        }

                    } while (msgCount == MaxMessagges);
                }
            }
        }
        catch(Exception e)
        {
            throw e;
        }
    }
    void ProcessarMensagem(byte[] buffer)
    {
        using (Packet p = new Packet(buffer))
        {
            int Fid = p.ReadInt();
            if (Fid == -1)
            {
                Fid = p.ReadInt();
                print($"[{DateTime.Now}] recebi um packet do tipo {((ServerPackets)Fid).ToString()} vind do host.");
                FuncsDoHost[Fid](p);
            }
            else
            {
                print($"[{DateTime.Now}] recebi um packet do tipo {((ClientPackets)Fid).ToString()}");
                ClientFuncs[Fid](p);
            }
        }
    }

    #region FuncsData Set
    /// <summary>
    /// São as funções que o cliente recebe do servidor
    /// </summary>
    internal static Dictionary<int, Action<Packet>> FuncsDoHost;
    /// <summary>
    /// São as funções que o cliente recebe de outro cliente
    /// </summary>
    internal static Dictionary<int, Action<Packet>> ClientFuncs;
    void DefineFuncs()
    {
        #region Host
        if (FuncsDoHost == null)
            FuncsDoHost = new();
        FuncsDoHost.Clear();
        FuncsDoHost.Add((int)ServerPackets.devolverSaudação, RecivePacket.ReceberSaudação_DoHost);
        #endregion


        #region Client
        if (ClientFuncs == null)
            ClientFuncs = new();
        ClientFuncs.Clear();

        ClientFuncs.Add((int)ClientPackets.saudação, RecivePacket.ReceberSaudação);
        ClientFuncs.Add((int)ClientPackets.pingar, RecivePacket.Pingar);
        #endregion
    }
    #endregion
    #region P2P Functions
    internal static Dictionary<CSteamID, HSteamNetConnection> P2PConns = new Dictionary<CSteamID, HSteamNetConnection>();
    /// <summary>
    /// Verifica todas as conexões que atualmente estão consolidadas.
    /// Caso todas estejam Ok, retorna (true, connexões_Problema[0])
    /// Caso Agluma tenha problema, retorna (false,connexões_Problema[quantidade com problema])
    /// </summary>
    /// <returns></returns>
    public (bool, CSteamID[]) VerificarTodasConexões()
    {
        SteamNetConnectionRealTimeStatus_t stats = new();
        SteamNetConnectionRealTimeLaneStatus_t lane = new();
        List<CSteamID> connexões_Problema = new List<CSteamID>();
        bool tudoBem = true;
        foreach (var item in P2PConns.Keys)
        {
            EResult r = SteamNetworkingSockets.GetConnectionRealTimeStatus(
                    P2PConns[item],     // HSteamNetConnection
                    ref stats,
                    0,         // nLanes (0 se não quiser info de lanes)
                    ref lane);
            if (r == EResult.k_EResultOK)
            {
                Debug.Log($"Ping: {stats.m_nPing} ms");
                Debug.Log($"Perda de pacotes: {stats.m_flInPacketsPerSec * 100f}%");
            }
            else
            {
                tudoBem = false;
                connexões_Problema.Add(item);
            }
        }
        return (tudoBem, connexões_Problema.ToArray());
    }
    public void ConnectTo(CSteamID idAlvo)
    {
        if (P2PConns.ContainsKey(idAlvo) || idAlvo == SteamUser.GetSteamID())
            return;
        SteamNetworkingIdentity id = new SteamNetworkingIdentity();
        id.SetSteamID(idAlvo);
        GuardarNovaCon(idAlvo, SteamNetworkingSockets.ConnectP2P(ref id, 0, 0, null));
        print($"Tentando conectar ao jogador {idAlvo.ToString()} ...");
    }
    public void ConnectToAll()
    {
        print("Conectando com todos!");
        int pCount = SteamMatchmaking.GetNumLobbyMembers(LobbyManager.currentLobbyId);
        for (int i = 0; i < pCount; i++)
        {
            ConnectTo(SteamMatchmaking.GetLobbyMemberByIndex(LobbyManager.currentLobbyId, i));
        }
    }
    internal void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t status)
    {
        ESteamNetworkingConnectionState state = status.m_info.m_eState;

        CSteamID remoteId = CSteamID.Nil;
        switch (state)
        {
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                SteamNetworkingSockets.AcceptConnection(status.m_hConn);
                remoteId = status.m_info.m_identityRemote.GetSteamID();
                GuardarNovaCon(remoteId, status.m_hConn);

                print("Tentando conexão...");
                break;

            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:

                remoteId = status.m_info.m_identityRemote.GetSteamID();

                SendPacket.Saudação(remoteId);

                Conectado = true;

                print("Conexão estabelecida!");
                break;

            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                Debug.LogWarning("Conexão encerrada pelo outro jogador.");

                //Desconecta do lobby e de todos os outros jadores caso tenha perdido a conexão com o Host

                /*remoteId = status.m_info.m_identityRemote.GetSteamID();

                if (remoteId == LobbyManager.idDoHost)
                {
                    LobbyManager.instance.LeaveLobby();
                }*/

                RemoverConexao(status.m_hConn);

                //Caso não hajam mais conexões dirá que o P2P não está conectado

                if (P2PConns.Count == 0)
                    Conectado = false;
                break;

            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                Debug.LogWarning("Problema detectado na conexão local (queda de rede?).");

                //Desconecta do lobby e de todos os outros jadores caso tenha perdido a conexão com o Host

                /*remoteId = status.m_info.m_identityRemote.GetSteamID();

                if (remoteId == LobbyManager.idDoHost)
                {
                    LobbyManager.instance.LeaveLobby();
                }*/

                RemoverConexao(status.m_hConn);

                //Caso não hajam mais conexões dirá que o P2P não está conectado

                if (P2PConns.Count == 0)
                    Conectado = false;
                break;
            default:
                break;
        }
    }
    void GuardarNovaCon(CSteamID id, HSteamNetConnection conn)
    {
        if (!P2PConns.ContainsKey(id))
            P2PConns.Add(id, conn);
    }
    internal void RemoverConexao(HSteamNetConnection conn)
    {
        CSteamID k = CSteamID.Nil;
        foreach (var item in P2PConns)
        {
            if (item.Value == conn)
            {
                k = item.Key;
                SteamNetworkingSockets.CloseConnection(conn, 0, "conexão encerranda", false);
                break;
            }
        }
        if (k != CSteamID.Nil)
        {
            P2PConns.Remove(k);
        }
    }
    internal void Clear()
    {
        Conectado = false;
        CSteamID[] keys = P2PConns.Keys.ToArray();
        for (int i = 0; i < keys.Length; i++)
        {
            RemoverConexao(P2PConns[keys[i]]);
        }
    }
    #endregion
}
