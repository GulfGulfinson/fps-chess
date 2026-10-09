using Godot;

namespace FpsChess.Net;

public interface ILobbyServer
{
    void OnHello(int peer, int protocol, string name);
    void OnCreateRoom(int peer, string settingsJson);
    void OnJoinRoom(int peer, string code);
    void OnRoomListRequested(int peer);
    void OnRejoin(int peer, string code, string token);
}

public interface ILobbyClient
{
    void OnWelcome();
    void OnError(string message);
    void OnRoomJoined(string code, string color, string token);
    void OnRoomList(string json);
}

/// <summary>
/// The lobby RPC endpoint. The same class sits at "Lobby" under the server root and the client root,
/// because Godot only delivers RPCs between nodes with identical RPC methods at the same path.
/// </summary>
public partial class LobbyChannel : Node
{
    public ILobbyServer? Server { get; set; }
    public ILobbyClient? Client { get; set; }

    public LobbyChannel() => Name = "Lobby";

    private int Sender => Multiplayer.GetRemoteSenderId();

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void Hello(int protocol, string name) => Server?.OnHello(Sender, protocol, name);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void CreateRoom(string settingsJson) => Server?.OnCreateRoom(Sender, settingsJson);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void JoinRoom(string code) => Server?.OnJoinRoom(Sender, code);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void RequestRoomList() => Server?.OnRoomListRequested(Sender);

    /// <summary>Takes a seat back after a lost connection, proven by the token from <see cref="RoomJoined"/>.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void Rejoin(string code, string token) => Server?.OnRejoin(Sender, code, token);

    [Rpc]
    public void Welcome() => Client?.OnWelcome();

    [Rpc]
    public void Error(string message) => Client?.OnError(message);

    [Rpc]
    public void RoomJoined(string code, string color, string token) => Client?.OnRoomJoined(code, color, token);

    [Rpc]
    public void RoomList(string json) => Client?.OnRoomList(json);

    /// <summary>
    /// Sent every few seconds so proxies do not drop quiet connections
    /// (Cloudflare closes WebSockets after 100 s without traffic). Clients ignore it.
    /// </summary>
    [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 1)]
    public void Keepalive() { }
}
