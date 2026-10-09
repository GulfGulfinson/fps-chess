using Godot;

namespace FpsChess.Net;

public interface ILobbyServer
{
    void OnHello(int peer, int protocol, string name);
    void OnCreateRoom(int peer, string settingsJson);
    void OnJoinRoom(int peer, string code);
    void OnRoomListRequested(int peer);
}

public interface ILobbyClient
{
    void OnWelcome();
    void OnError(string message);
    void OnRoomJoined(string code, string color);
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

    [Rpc]
    public void Welcome() => Client?.OnWelcome();

    [Rpc]
    public void Error(string message) => Client?.OnError(message);

    [Rpc]
    public void RoomJoined(string code, string color) => Client?.OnRoomJoined(code, color);

    [Rpc]
    public void RoomList(string json) => Client?.OnRoomList(json);
}
