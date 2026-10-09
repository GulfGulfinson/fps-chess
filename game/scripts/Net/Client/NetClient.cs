using Chess.Core;
using Godot;

namespace FpsChess.Net.Client;

/// <summary>
/// Root of the client branch: connects to a server (embedded or remote), introduces itself and joins rooms.
/// Raises plain C# events that the app turns into screens.
/// </summary>
public partial class NetClient : Node, ILobbyClient
{
    private LobbyChannel _lobby = null!;
    private Node _rooms = null!;
    private string _playerName = "";

    public NetClient() => Name = "Client";

    public MatchChannel? Channel { get; private set; }

    /// <summary>Room code and seat token of the current room, used to come back after a lost connection.</summary>
    public string? RoomCode { get; private set; }
    public string? SeatToken { get; private set; }

    public event Action? Welcomed;
    public event Action<string>? Failed;
    public event Action<string, PieceColor>? RoomJoined;
    public event Action<List<RoomListEntry>>? RoomListReceived;
    public event Action? Disconnected;

    public static NetClient Connect(Node parent, string url, string playerName)
    {
        var client = new NetClient { _playerName = playerName };
        parent.AddChild(client);
        var error = NetHost.Connect(client, url);
        if (error != Error.Ok)
        {
            client.CallDeferred(MethodName.Fail, $"Verbindung zu {url} fehlgeschlagen ({error}).");
            return client;
        }
        client.Multiplayer.ConnectedToServer += client.OnConnected;
        client.Multiplayer.ConnectionFailed += () => client.Fail($"Der Server {url} ist nicht erreichbar.");
        client.Multiplayer.ServerDisconnected += () => client.Disconnected?.Invoke();
        return client;
    }

    public override void _Ready()
    {
        _lobby = new LobbyChannel { Client = this };
        AddChild(_lobby);
        _rooms = new Node { Name = "Rooms" };
        AddChild(_rooms);
    }

    public override void _ExitTree() => NetHost.Close(this);

    private void OnConnected() => _lobby.RpcId(1, LobbyChannel.MethodName.Hello, NetConfig.ProtocolVersion, _playerName);

    private void Fail(string message) => Failed?.Invoke(message);

    public void CreateRoom(RoomSettings settings) => _lobby.RpcId(1, LobbyChannel.MethodName.CreateRoom, NetJson.Write(settings));

    public void JoinRoom(string code) => _lobby.RpcId(1, LobbyChannel.MethodName.JoinRoom, code);

    public void RequestRoomList() => _lobby.RpcId(1, LobbyChannel.MethodName.RequestRoomList);

    public void Rejoin(string code, string token) => _lobby.RpcId(1, LobbyChannel.MethodName.Rejoin, code, token);

    public void OnWelcome() => Welcomed?.Invoke();

    public void OnError(string message) => Fail(message);

    public void OnRoomJoined(string code, string color, string token)
    {
        RoomCode = code;
        SeatToken = token;
        Channel?.QueueFree();
        Channel = new MatchChannel { Name = code };
        _rooms.AddChild(Channel);
        // Handlers must attach the screen synchronously: the room's first message follows immediately.
        RoomJoined?.Invoke(code, Enum.Parse<PieceColor>(color));
    }

    public void OnRoomList(string json)
    {
        try
        {
            RoomListReceived?.Invoke(NetJson.Read<List<RoomListEntry>>(json));
        }
        catch (InvalidDataException e)
        {
            GD.PushWarning($"[client] bad room list: {e.Message}");
        }
    }
}
