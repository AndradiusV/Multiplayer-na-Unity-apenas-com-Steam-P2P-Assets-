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
    internal Callback<LobbyChatMsg_t> lobbyChatMsg;
    protected Callback<LobbyMatchList_t> lobbyMatchList;
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
        lobbyChatMsg = Callback<LobbyChatMsg_t>.Create(OnLobbyChatMsg);
        lobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
        lobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested);
        connectionStatusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(P2P.instance.OnConnectionStatusChanged);
        #endregion

        SteamNetworkingUtils.InitRelayNetworkAccess();//Eu até li a documentação, mas lá só falava que é bom deixar isso aqui. não entendi de verdade.

        listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);
    }
    #region Comandos
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
    #endregion
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
    /// <summary> Define uma informação do lobby </summary>
    internal void SetLobbyData(string key, string valor)
    {
        SteamMatchmaking.SetLobbyData(currentLobbyId, key, valor);
    }
    #endregion
    #region Mensagens no Lobby
    // --------------------------------------------------------------------
    // Chamado quando alguém envia uma mensagem no chat do lobby
    // --------------------------------------------------------------------
    void OnLobbyChatMsg(LobbyChatMsg_t data)
    {
        byte[] buffer = new byte[4096];

        SteamMatchmaking.GetLobbyChatEntry(
            currentLobbyId,
            (int)data.m_iChatID,
            out CSteamID sender,
            buffer,
            buffer.Length,
            out EChatEntryType type
        );
        //Verifica se quem envou a mensagem é você mesmo, se for então irá ignora-la
        if (sender == SteamUser.GetSteamID())
            return;

        string msg = System.Text.Encoding.UTF8.GetString(buffer).TrimEnd('\0');
        //No meu caso eu estou separando as informações das mensagens por ":"
        //Exomplo: Mensagem recebida é "UPDATE_PLINFO:2:1:0"
        //Ao separar a mensagem teremos "UPDATE_PLINFO", "2","1", "0" onde esses valores podem ser a vida, dano e pontos do jogador
        string[] infos = msg.Split(':');

        if (infos[0] == "UPDATE_PLINFO")
        {

        }
        else if (infos[0] == "UPDATE_LOBBYINFO")
        {

        }
    }
    /// <summary>
    /// Envia uma mesangem no chat do host. tente usar isso com um padrão para trocar informações importantes.
    /// </summary>
    /// <param name="mensagem"></param>
    public void EnviarMensagemParaLobby(string mensagem)
    {
        byte[] msg = System.Text.Encoding.UTF8.GetBytes(mensagem);
        SteamMatchmaking.SendLobbyChatMsg(currentLobbyId, msg, msg.Length);
    }
    #endregion
    #region Lobby list
    public void ProcuraLobbies()
    {
        #region Filtro opcional
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            "NOME DO QUE VOCÊ QUER COMPARAR",
            "O QUE QUER COMPARAR",
            ELobbyComparison.k_ELobbyComparisonEqual
        );
        #endregion

        // Número máximo de resultados
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(20);

        SteamMatchmaking.RequestLobbyList();
    }
    /// <summary> É chamado quando a steam devolve uma lista de lobbys, apartir disso você decide o que é feito </summary>
    private void OnLobbyMatchList(LobbyMatchList_t result)
    {
        Debug.Log("Lobbies encontrados: " + result.m_nLobbiesMatching);

        #region Exemplo
        //Cicla entre os lobbys achados
        for (int i = 0; i < result.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);

            //Pega o nome do lobby
            string nomeDoLobby = SteamMatchmaking.GetLobbyData(lobbyID, "name");
            //Pega a quantidade de jogadores dentro do lobby
            int members = SteamMatchmaking.GetNumLobbyMembers(lobbyID);

            Debug.Log($"Lobby {i}: {nomeDoLobby} | Jogadores: {members}");

            /*Uma dica é para você criar uma lista com esses
             * lobbys achados e depois criar botões com a UI para
             * poder escolher o lobby para entrar */
        }
        #endregion
    }
    #endregion
}