using GameServer;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    private const int MaxPlayers = 10;
    private HSteamListenSocket listenSocket;

    #region Callbacks
    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<LobbyEnter_t> lobbyEntered;
    protected Callback<GameLobbyJoinRequested_t> lobbyJoinRequested;
    protected Callback<SteamNetConnectionStatusChangedCallback_t> connectionStatusChanged;
    #endregion

    internal static CSteamID currentLobbyId;
    internal static CSteamID idDoHost;
    internal static bool Host;
    internal static bool Conectado;

    internal static LobbyManager instance;

    void Start()
    {
        //Verifica se a Steam está Inicializada
        if (!SteamManager.Initialized || instance != null)
        {
            Debug.LogError("Steam não foi inicializado!");
            return;
        }

        instance = this;

        #region Callbacks
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        lobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested);
        connectionStatusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(P2P.instance.OnConnectionStatusChanged);
        #endregion

        SteamNetworkingUtils.InitRelayNetworkAccess();//Eu até li a documentação, mas lá só falava que é bom deixar isso aqui. não entendi de verdade.

        listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);
    }
    // Criar um lobby (host)
    public void CreateLobby()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxPlayers);
        Debug.Log("Criando lobby...");
    }

    // Entrar em um lobby específico (por ID)
    public void JoinLobby(CSteamID lobbyId)
    {
        SteamMatchmaking.JoinLobby(lobbyId);
        Debug.Log("Tentando entrar no lobby " + lobbyId);
    }

    // Sair / fechar o lobby
    public void LeaveLobby()
    {
        /*Lembre-se está função é tanto para o host quanto para os clientes.
         * Faça uma boa adiministração desta parte caso contrário o seu jogo pode quebrar a qualquer momento!*/

        if (currentLobbyId.IsValid())
        {
            SteamMatchmaking.LeaveLobby(currentLobbyId);
            Debug.Log("Saiu / fechou o lobby: " + currentLobbyId);
            currentLobbyId = CSteamID.Nil;
        }
        Host = false;
        Conectado = false;
        P2P.instance.Clear();
    }
    #region Callbacks
    // Callback quando o lobby é criado
    private void OnLobbyCreated(LobbyCreated_t result)
    {
        if (result.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("Falha ao criar lobby: " + result.m_eResult);
            return;
        }

        Host = true;
        currentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
        Debug.Log("Lobby criado com sucesso! ID: " + currentLobbyId);

        // Define nome visível do lobby
        SteamMatchmaking.SetLobbyData(currentLobbyId, "name", SteamFriends.GetPersonaName() + "'s Lobby");
    }

    // Callback quando entra em um lobby
    private void OnLobbyEntered(LobbyEnter_t result)
    {
        currentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
        Debug.Log("Entrou no lobby: " + currentLobbyId);

        // Verifica se o jogador é o host
        CSteamID owner = SteamMatchmaking.GetLobbyOwner(currentLobbyId);
        idDoHost = owner;
        bool isOwner = (owner == SteamUser.GetSteamID());
        if (!isOwner)
            P2P.instance.ConnectTo(owner);

        Conectado = true;

        print(isOwner ? "Você é o host!" : "Você é um cliente!");
    }

    // Callback quando há convite / join pedido
    private void OnLobbyJoinRequested(GameLobbyJoinRequested_t request)
    {
        Debug.Log("Convite recebido para lobby: " + request.m_steamIDLobby);
        JoinLobby(request.m_steamIDLobby);
    }
    #endregion

    #region Alteração do LOBBY
    /// <summary> Esta função define se outros jogadores ainda podementrar ou não no seu LOBBY </summary>
    internal void LobbyEntraceChange(bool valor)
    {
        SteamMatchmaking.SetLobbyJoinable(currentLobbyId, valor);
    }
    /// <summary> Esta função define se o tipo do LOBBY </summary>
    internal void LobbyTypeChange(ELobbyType valor)
    {
        SteamMatchmaking.SetLobbyType(currentLobbyId, valor);
    }
    #endregion
}