using Chess.Core;
using Godot;

namespace FpsChess.Net.Server;

/// <summary>
/// Root of the server branch: accepts connections, checks the protocol version and hands players to rooms.
/// Runs embedded in the game for computer opponents and stand-alone (headless, --server) on gulfinson.
/// </summary>
public partial class LobbyServer : Node, ILobbyServer
{
    // No 0/O or 1/I so codes can be read out loud without confusion.
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    private readonly Dictionary<int, string> _names = [];
    private readonly Dictionary<string, MatchRoom> _rooms = [];
    private LobbyChannel _lobby = null!;
    private Node _roomsNode = null!;

    public LobbyServer() => Name = "Server";

    /// <summary>Adds a server under <paramref name="parent"/> and starts listening.</summary>
    public static LobbyServer Start(Node parent, int port, string bindAddress, out Error error)
    {
        var server = new LobbyServer();
        parent.AddChild(server);
        error = NetHost.Listen(server, port, bindAddress);
        if (error == Error.Ok)
        {
            server.Multiplayer.PeerDisconnected += server.OnPeerDisconnected;
            GD.Print($"[server] listening on {bindAddress}:{port}");
        }
        return server;
    }

    public override void _Ready()
    {
        _lobby = new LobbyChannel { Server = this };
        AddChild(_lobby);
        _roomsNode = new Node { Name = "Rooms" };
        AddChild(_roomsNode);
    }

    public override void _ExitTree() => NetHost.Close(this);

    public void OnHello(int peer, int protocol, string name)
    {
        if (protocol != NetConfig.ProtocolVersion)
        {
            _lobby.RpcId(peer, LobbyChannel.MethodName.Error,
                $"Versionen passen nicht zusammen (Spiel {protocol}, Server {NetConfig.ProtocolVersion}). Bitte das Spiel aktualisieren.");
            // Give the error message time to arrive; a node-owned tween dies with the server.
            var tween = CreateTween();
            tween.TweenInterval(0.5);
            tween.TweenCallback(Callable.From(() => Multiplayer.MultiplayerPeer?.DisconnectPeer(peer)));
            return;
        }
        _names[peer] = string.IsNullOrWhiteSpace(name) ? "Spieler" : name.Trim()[..Math.Min(name.Trim().Length, 24)];
        _lobby.RpcId(peer, LobbyChannel.MethodName.Welcome);
    }

    public void OnCreateRoom(int peer, string settingsJson)
    {
        if (!_names.TryGetValue(peer, out var name)) return;

        RoomSettings settings;
        try
        {
            settings = NetJson.Read<RoomSettings>(settingsJson);
        }
        catch (InvalidDataException e)
        {
            GD.PushWarning($"[server] peer {peer} sent bad room settings: {e.Message}");
            _lobby.RpcId(peer, LobbyChannel.MethodName.Error, "Ungültige Spieleinstellungen.");
            return;
        }

        var code = NewCode();
        var channel = new MatchChannel { Name = code };
        _roomsNode.AddChild(channel);
        var room = new MatchRoom();
        channel.AddChild(room);

        string? error = room.Init(code, settings, channel, this);
        if (error is not null)
        {
            channel.QueueFree();
            _lobby.RpcId(peer, LobbyChannel.MethodName.Error, error);
            return;
        }

        _rooms[code] = room;
        Join(peer, name, room);
    }

    public void OnJoinRoom(int peer, string code)
    {
        if (!_names.TryGetValue(peer, out var name)) return;
        code = code.Trim().ToUpperInvariant();
        if (!_rooms.TryGetValue(code, out var room) || !room.HasFreeSeat)
        {
            _lobby.RpcId(peer, LobbyChannel.MethodName.Error, $"Kein offenes Spiel mit dem Code {code}.");
            return;
        }
        Join(peer, name, room);
    }

    public void OnRoomListRequested(int peer)
    {
        var entries = _rooms.Values.Where(r => r.IsListed).Select(r => r.ListEntry()).ToList();
        _lobby.RpcId(peer, LobbyChannel.MethodName.RoomList, NetJson.Write(entries));
    }

    public void OnRejoin(int peer, string code, string token)
    {
        if (!_names.ContainsKey(peer)) return;
        if (!_rooms.TryGetValue(code.Trim().ToUpperInvariant(), out var room) || room.TryReclaim(peer, token) is not { } color)
        {
            _lobby.RpcId(peer, LobbyChannel.MethodName.Error, "Die Partie gibt es nicht mehr.");
            return;
        }
        _lobby.RpcId(peer, LobbyChannel.MethodName.RoomJoined, room.Code, color.ToString(), room.TokenFor(color));
        room.OnReclaimed(peer, color);
    }

    private void Join(int peer, string name, MatchRoom room)
    {
        var color = room.Seat(peer, name);
        // RoomJoined makes the client create its channel node before the room sends anything to it.
        _lobby.RpcId(peer, LobbyChannel.MethodName.RoomJoined, room.Code, color.ToString(), room.TokenFor(color));
        room.OnSeated(peer);
    }

    private void OnPeerDisconnected(long id)
    {
        var peer = (int)id;
        _names.Remove(peer);
        foreach (var room in _rooms.Values.ToList()) room.OnPeerLeft(peer);
    }

    /// <summary>Called by a room once nobody is left in it.</summary>
    public void CloseRoom(MatchRoom room)
    {
        _rooms.Remove(room.Code);
        room.GetParent().QueueFree();
    }

    public bool IsConnected(int peer) => _names.ContainsKey(peer);

    private string NewCode()
    {
        string code;
        do
        {
            code = string.Concat(Enumerable.Range(0, 4).Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)]));
        } while (_rooms.ContainsKey(code));
        return code;
    }
}
