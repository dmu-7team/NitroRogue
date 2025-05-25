//  통합 CustomNetworkManager.cs
using UnityEngine;
using Mirror;
using System;
using System.Collections.Generic;

public struct RoomListSyncMessage : NetworkMessage
{
    public List<RoomInfo> roomList;
}

public struct EmptyMessage : NetworkMessage { }

public class CustomNetworkManager : NetworkManager
{
    private void Start()
    {
        string[] args = Environment.GetCommandLineArgs();

        foreach (var arg in args)
        {
            if (arg == "-mode=host")
            {
                Debug.Log("[AutoStart] StartHost() 자동 실행됨");
                StartHost();
            }
            else if (arg == "-mode=server")
            {
                Debug.Log("[AutoStart] StartServer() 자동 실행됨");
                StartServer();
            }
        }
    }

    public static string matchIdToJoin;

    private Dictionary<string, List<NetworkConnection>> matchRooms = new();
    private Dictionary<string, string> roomNames = new();
    private HashSet<string> activeMatchIds = new();
    private List<NetworkConnectionToClient> connectedClients = new();

    public void StartClientWithCustomPort()
    {
        string[] args = Environment.GetCommandLineArgs();

        foreach (string arg in args)
        {
            if (arg.StartsWith("-port="))
            {
                if (int.TryParse(arg.Substring(6), out int port))
                {
                    var transport = NetworkManager.singleton.transport as TelepathyTransport;
                    if (transport != null)
                        transport.port = (ushort)port;
                }
            }
            if (arg.StartsWith("-address="))
            {
                string ip = arg.Substring(9);
                NetworkManager.singleton.networkAddress = ip;
            }
        }

        StartClient();
    }

    public void StartClientManual(string address, ushort port)
    {
        networkAddress = address;
        var transport = NetworkManager.singleton.transport as TelepathyTransport;
        if (transport != null)
            transport.port = port;
        StartClient();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        NetworkServer.RegisterHandler<EmptyMessage>(OnRefreshRoomList);
    }

    private void OnRefreshRoomList(NetworkConnectionToClient conn, EmptyMessage msg)
    {
        SendRoomListToClient(conn);
    }

    private void SendRoomListToClient(NetworkConnection conn)
    {
        if (conn == null || conn.identity == null)
            return;

        List<RoomInfo> roomList = new();
        foreach (var matchId in activeMatchIds)
        {
            int playerCount = matchRooms.ContainsKey(matchId) ? matchRooms[matchId].Count : 0;
            string roomName = roomNames.ContainsKey(matchId) ? roomNames[matchId] : "NoName";

            roomList.Add(new RoomInfo
            {
                matchId = matchId,
                roomName = roomName,
                currentPlayers = playerCount,
                maxPlayers = maxConnections
            });
        }

        RoomListSyncMessage msg = new() { roomList = roomList };
        conn.Send(msg);
    }

    private void SendRoomListToAll()
    {
        foreach (var conn in connectedClients)
            SendRoomListToClient(conn);
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        connectedClients.Add(conn);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        connectedClients.Remove(conn);

        foreach (var match in matchRooms)
        {
            match.Value.Remove(conn);
            if (match.Value.Count == 0)
                activeMatchIds.Remove(match.Key);
        }

        SendRoomListToAll();
        base.OnServerDisconnect(conn);
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        GameObject player = Instantiate(playerPrefab);
        RoomPlayer roomPlayer = player.GetComponent<RoomPlayer>();

        string roomName = RoomListUI.Instance?.roomNameInputField?.text ?? "NoName";

        if (string.IsNullOrEmpty(matchIdToJoin))
            matchIdToJoin = Guid.NewGuid().ToString();

        roomPlayer.matchId = matchIdToJoin;
        roomPlayer.roomName = roomName;

        if (!matchRooms.ContainsKey(matchIdToJoin))
            matchRooms[matchIdToJoin] = new List<NetworkConnection>();
        matchRooms[matchIdToJoin].Add(conn);

        roomNames[matchIdToJoin] = roomName;
        activeMatchIds.Add(matchIdToJoin);

        NetworkServer.AddPlayerForConnection(conn, player);
        SendRoomListToAll();
    }

} // 끝